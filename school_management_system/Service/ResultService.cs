using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;

namespace school_management_system.Service;


public record StudentResultRow(
    string AcademicYear, int FormLevel, int Semester, AssessmentType Assessment, string Subject,
    string? Teacher, decimal ClassScore, decimal ExamScore, decimal Total, string Grade);
public class GradingService(AppDbContext db, IOptions<GradingOptions> opt)
{

    public async Task<(decimal Total, string Grade)> ComputeAsync(decimal classScore, decimal examScore)
    {
        var o = opt.Value;
        if (classScore < 0 || classScore > o.MaxClassScore)
            throw new DomainException($"Class score must be between 0 and {o.MaxClassScore}.");
        if (examScore < 0 || examScore > o.MaxExamScore)
            throw new DomainException($"Exam score must be between 0 and {o.MaxExamScore}.");
        var total = classScore + examScore;
        var grade = await db.GradeBoundaries.
            Where(g => g.MinScore <= total)
            .OrderByDescending(g => g.MinScore).Select(g => g.Grade).FirstOrDefaultAsync() ?? "F9";
        return (total, grade);
    }

}


/// <summary>Draft -> Submitted (teacher) -> Reviewed (coordinator) -> Approved (admin) -> Published (students see it).
/// The coordinator can send a subject back (Returned).</summary>
public class ResultWorkflowService(AppDbContext db, GradingService grading)
{

    // ---------- Teacher ----------
    public async Task SaveDraftAsync(int teacherId, int enrollmentId, int subjectId, int assessmentId,
        decimal classScore, decimal examScore, string actorUserId)
    {
        var enr = await db.Enrollments.Include(e => e.ClassYear).ThenInclude(c => c.SchoolClass)
                      .SingleOrDefaultAsync(e => e.Id == enrollmentId) ?? throw new DomainException("Enrollment not found.");
        var asm = await db.Assessments.Include(a => a.Semester)
                      .SingleOrDefaultAsync(a => a.Id == assessmentId) ?? throw new DomainException("Assessment not found.");

        if (asm.Semester.AcademicYearId != enr.AcademicYearId) throw new DomainException("Assessment belongs to a different academic year.");
        if (enr.ClassYear.SchoolClass.FormLevel == 3 && asm.Semester.Number == 2)
            throw new DomainException("Form 3 students have no second-semester exams (WASSCE).");
        if (!asm.IsOpen) throw new DomainException("This assessment is closed.");
        if (!await db.TeachingAssignments.AnyAsync(t => t.TeacherId == teacherId && t.SubjectId == subjectId && t.ClassYearId == enr.ClassYearId))
            throw new DomainException("You are not assigned to teach this subject in this class.");

        var (total, grade) = await grading.ComputeAsync(classScore, examScore);
        var r = await db.Results.SingleOrDefaultAsync(x => x.EnrollmentId == enrollmentId && x.SubjectId == subjectId && x.AssessmentId == assessmentId);
        if (r == null)
        {
            r = new Result { EnrollmentId = enrollmentId, SubjectId = subjectId, AssessmentId = assessmentId, Status = ResultStatus.Draft };
            db.Results.Add(r);
        }
        else if (r.Status is not (ResultStatus.Draft or ResultStatus.Returned))
            throw new DomainException("Results already submitted cannot be edited.");

        db.ResultAuditLogs.Add(new ResultAuditLog
        {
            Result = r,
            FromStatus = r.Status,
            ToStatus = r.Status,
            ActorUserId = actorUserId,
            Note = $"Scores set: class {classScore}, exam {examScore}"
        });
        r.ClassScore = classScore; r.ExamScore = examScore; r.Total = total; r.Grade = grade;
        await db.SaveChangesAsync();
    }

    /// <summary>Teacher pushes one subject's results for a whole class. Every student must have scores.</summary>
    public async Task SubmitAsync(int teacherId, int classYearId, int subjectId, int assessmentId, string actorUserId)
    {
        if (!await db.TeachingAssignments.AnyAsync(t => t.TeacherId == teacherId && t.SubjectId == subjectId && t.ClassYearId == classYearId))
            throw new DomainException("You are not assigned to this subject and class.");

        var expected = await ActiveEnrollments(classYearId).CountAsync();
        var rs = await db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.SubjectId == subjectId && r.AssessmentId == assessmentId).ToListAsync();
        if (rs.Count < expected) throw new DomainException($"Scores are missing for {expected - rs.Count} student(s).");
        var open = rs.Where(r => r.Status is ResultStatus.Draft or ResultStatus.Returned).ToList();
        if (open.Count == 0) throw new DomainException("Nothing to submit.");
        await MoveAsync(open, ResultStatus.Submitted, actorUserId);
    }



    // ---------- Coordinator ----------
    public async Task ReturnAsync(int coordinatorTeacherId, int classYearId, int subjectId, int assessmentId, string note, string actorUserId)
    {
        await EnsureCoordinatorAsync(coordinatorTeacherId, classYearId);
        var rs = await db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.SubjectId == subjectId
                                              && r.AssessmentId == assessmentId && r.Status == ResultStatus.Submitted).ToListAsync();
        if (rs.Count == 0) throw new DomainException("No submitted results to return.");
        await MoveAsync(rs, ResultStatus.Returned, actorUserId, note);
    }



    /// <summary>Coordinator confirms the whole class (all subjects) and sends it to admin.</summary>
    public async Task ReviewAsync(int coordinatorTeacherId, int classYearId, int assessmentId, string actorUserId)
    {
        await EnsureCoordinatorAsync(coordinatorTeacherId, classYearId);
        var rs = await ClassResults(classYearId, assessmentId).ToListAsync();
        await EnsureCompleteAsync(classYearId, rs, ResultStatus.Submitted, "submitted by every subject teacher");
        await MoveAsync(rs, ResultStatus.Reviewed, actorUserId);
    }

    // ---------- Admin ----------
    public async Task ApproveAsync(int classYearId, int assessmentId, string adminUserId)
    {
        var rs = await ClassResults(classYearId, assessmentId).ToListAsync();
        await EnsureCompleteAsync(classYearId, rs, ResultStatus.Reviewed, "reviewed by the coordinator");
        await MoveAsync(rs, ResultStatus.Approved, adminUserId);
    }



    /// <summary>Only now can students see the results.</summary>
    public async Task PublishAsync(int classYearId, int assessmentId, string adminUserId)
    {
        var rs = await ClassResults(classYearId, assessmentId).ToListAsync();
        if (rs.Count == 0 || rs.Any(r => r.Status != ResultStatus.Approved))
            throw new DomainException("All results must be approved before publishing.");
        await MoveAsync(rs, ResultStatus.Published, adminUserId);
    }

    // ---------- Student ----------
    public Task<List<StudentResultRow>> GetPublishedForStudentAsync(int studentId) =>
        (from r in db.Results
         where r.Enrollment.StudentId == studentId && r.Status == ResultStatus.Published
         orderby r.Enrollment.AcademicYear.Name, r.Assessment.Semester.Number, r.Assessment.Type, r.Subject.Name
         select new StudentResultRow(
             r.Enrollment.AcademicYear.Name, r.Enrollment.ClassYear.SchoolClass.FormLevel,
             r.Assessment.Semester.Number, r.Assessment.Type, r.Subject.Name,
             db.TeachingAssignments.Where(t => t.SubjectId == r.SubjectId && t.ClassYearId == r.Enrollment.ClassYearId)
                 .Select(t => t.Teacher.User.FullName).FirstOrDefault(),
             r.ClassScore, r.ExamScore, r.Total, r.Grade)).ToListAsync();



    // ---------- helpers ----------
    private IQueryable<Enrollment> ActiveEnrollments(int classYearId) =>
        db.Enrollments.Where(e => e.ClassYearId == classYearId && e.Outcome == EnrollmentOutcome.Pending && e.Student.Status == StudentStatus.Active);

    private IQueryable<Result> ClassResults(int classYearId, int assessmentId) =>
        db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.AssessmentId == assessmentId);

    private async Task EnsureCoordinatorAsync(int teacherId, int classYearId)
    {
        if (!await db.ClassYears.AnyAsync(c => c.Id == classYearId && c.CoordinatorTeacherId == teacherId))
            throw new DomainException("You are not the coordinator of this class.");
    }

    private async Task EnsureCompleteAsync(int classYearId, List<Result> rs, ResultStatus required, string what)
    {
        var students = await ActiveEnrollments(classYearId).CountAsync();
        var classSubjects = await db.ClassYears.Where(c => c.Id == classYearId)
            .Select(c => db.ClassSubjects.Count(s => s.SchoolClassId == c.SchoolClassId)).SingleAsync();
        if (rs.Count != students * classSubjects || rs.Any(r => r.Status != required))
            throw new DomainException($"Every subject's results must be {what} first.");
    }




    private async Task MoveAsync(List<Result> rs, ResultStatus to, string actor, string? note = null)
    {
        foreach (var r in rs)
        {
            db.ResultAuditLogs.Add(new ResultAuditLog { ResultId = r.Id, FromStatus = r.Status, ToStatus = to, ActorUserId = actor, Note = note });
            r.Status = to;
            if (note != null) r.ReviewNote = note;
        }
        await db.SaveChangesAsync();
    }
}



