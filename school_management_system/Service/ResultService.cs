using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;

namespace school_management_system.Service;


/// <summary>One subject in one semester as the student sees it: midterm + final = total.</summary>
public record SemesterResultRow(
    string AcademicYear, int FormLevel, int Semester, string Subject, string? Teacher,
    decimal? Midterm, decimal? Final, decimal? Total, string Grade);
public class GradingService(AppDbContext db, IOptions<GradingOptions> opt)
{

    /// <summary>A midterm exam collects the "class score"; a final exam collects the "exam score".</summary>
    public decimal MaxFor(AssessmentType type) => type == AssessmentType.Midterm ? opt.Value.MaxClassScore : opt.Value.MaxExamScore;

    public void Validate(AssessmentType type, decimal score)
    {
        var max = MaxFor(type);
        if (score < 0 || score > max)
            throw new DomainException($"{(type == AssessmentType.Midterm ? "Midterm" : "Final exam")} score must be between 0 and {max:0.#}.");
    }

    public async Task<List<GradeBoundary>> GetBoundariesAsync() =>
        await db.GradeBoundaries.OrderByDescending(g => g.MinScore).ToListAsync();

    public static string GradeFor(IReadOnlyList<GradeBoundary> boundaries, decimal total) =>
        boundaries.FirstOrDefault(g => g.MinScore <= total)?.Grade ?? "F9";

}


/// <summary>Draft -> Submitted (teacher) -> Reviewed (coordinator) -> Approved (admin) -> Published (students see it).
/// The coordinator can send a subject back (Returned).</summary>
public class ResultWorkflowService(AppDbContext db, GradingService grading)
{

    // ---------- Teacher ----------
    /// <summary>Saves the one score this exam collects: the midterm score on a midterm, the exam score on a final.</summary>
    public async Task SaveDraftAsync(int teacherId, int enrollmentId, int subjectId, int assessmentId, decimal? score, string actorUserId)
    {
        if (score == null) throw new DomainException("Enter a score.");
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
        grading.Validate(asm.Type, score.Value);

        var r = await db.Results.SingleOrDefaultAsync(x => x.EnrollmentId == enrollmentId && x.SubjectId == subjectId && x.AssessmentId == assessmentId);
        if (r == null)
        {
            r = new Result { EnrollmentId = enrollmentId, SubjectId = subjectId, AssessmentId = assessmentId, Status = ResultStatus.Draft };
            db.Results.Add(r);
        }
        else if (r.Status is not (ResultStatus.Draft or ResultStatus.Returned))
            throw new DomainException("Results already submitted cannot be edited.");

        if (asm.Type == AssessmentType.Midterm) { r.ClassScore = score; r.ExamScore = null; }
        else { r.ExamScore = score; r.ClassScore = null; }

        db.ResultAuditLogs.Add(new ResultAuditLog
        {
            Result = r,
            FromStatus = r.Status,
            ToStatus = r.Status,
            ActorUserId = actorUserId,
            Note = $"{(asm.Type == AssessmentType.Midterm ? "Midterm" : "Final exam")} score set to {score:0.##}"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Teacher pushes one subject's scores for a whole class. Every student must have a score.</summary>
    public async Task SubmitAsync(int teacherId, int classYearId, int subjectId, int assessmentId, string actorUserId)
    {
        if (!await db.TeachingAssignments.AnyAsync(t => t.TeacherId == teacherId && t.SubjectId == subjectId && t.ClassYearId == classYearId))
            throw new DomainException("You are not assigned to this subject and class.");

        var type = await db.Assessments.Where(a => a.Id == assessmentId).Select(a => a.Type).SingleAsync();
        var expected = await ActiveEnrollments(classYearId).CountAsync();
        var rs = await db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.SubjectId == subjectId && r.AssessmentId == assessmentId).ToListAsync();
        var scored = rs.Count(r => (type == AssessmentType.Midterm ? r.ClassScore : r.ExamScore) != null);
        if (scored < expected) throw new DomainException($"Scores are missing for {expected - scored} student(s).");
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



    /// <summary>Only now can students see this exam's scores.</summary>
    public async Task PublishAsync(int classYearId, int assessmentId, string adminUserId)
    {
        var rs = await ClassResults(classYearId, assessmentId).ToListAsync();
        if (rs.Count == 0 || rs.Any(r => r.Status != ResultStatus.Approved))
            throw new DomainException("All results must be approved before publishing.");
        await MoveAsync(rs, ResultStatus.Published, adminUserId);
    }

    // ---------- Student ----------
    /// <summary>Published scores, joined into one row per subject per semester. The total and grade appear once both
    /// the midterm and the final are published.</summary>
    public async Task<List<SemesterResultRow>> GetPublishedForStudentAsync(int studentId)
    {
        var boundaries = await grading.GetBoundariesAsync();
        var rows = await db.Results
            .Where(r => r.Enrollment.StudentId == studentId && r.Status == ResultStatus.Published)
            .Select(r => new
            {
                r.EnrollmentId,
                r.SubjectId,
                r.Assessment.SemesterId,
                Semester = r.Assessment.Semester.Number,
                r.Assessment.Type,
                Year = r.Enrollment.AcademicYear.Name,
                Form = r.Enrollment.ClassYear.SchoolClass.FormLevel,
                Subject = r.Subject.Name,
                r.ClassScore,
                r.ExamScore,
                Teacher = db.TeachingAssignments.Where(t => t.SubjectId == r.SubjectId && t.ClassYearId == r.Enrollment.ClassYearId)
                    .Select(t => t.Teacher.User.FullName).FirstOrDefault()
            }).ToListAsync();

        return rows.GroupBy(r => new { r.EnrollmentId, r.SemesterId, r.SubjectId })
            .Select(g =>
            {
                var first = g.First();
                var midterm = g.FirstOrDefault(x => x.Type == AssessmentType.Midterm)?.ClassScore;
                var final = g.FirstOrDefault(x => x.Type == AssessmentType.Final)?.ExamScore;
                decimal? total = midterm != null && final != null ? midterm + final : null;
                return new SemesterResultRow(first.Year, first.Form, first.Semester, first.Subject, first.Teacher, midterm, final, total,
                    total != null ? GradingService.GradeFor(boundaries, total.Value) : "");
            })
            .OrderBy(r => r.AcademicYear).ThenBy(r => r.Semester).ThenBy(r => r.Subject).ToList();
    }



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



