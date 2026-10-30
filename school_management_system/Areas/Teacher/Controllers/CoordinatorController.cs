using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Teacher.Controllers;


public record SubjectStatus(int SubjectId, string Subject, string? Teacher, int Students, Dictionary<ResultStatus, int> Counts)
{
    public int Entered => Counts.Values.Sum();
    public bool AllSubmitted => Students > 0 && Counts.GetValueOrDefault(ResultStatus.Submitted) == Students;
}

public record PromotionRow(int EnrollmentId, string Name, string Number, decimal? Average, PromotionDecision? Decision);

public class CoordinatorController(AppDbContext db, DashboardQueries q, ResultWorkflowService workflow, MatrixService matrix, PromotionService promotion) : TeacherBase(db, q)
{
    public IActionResult Index() => RedirectToAction("Index", "Home");

    public async Task<IActionResult> Class(int classYearId, int? assessmentId)
    {
        if (!await IsCoordinatorAsync(classYearId)) return Forbid();
        var cy = await Db.ClassYears.Include(c => c.SchoolClass).ThenInclude(s => s.Course).SingleAsync(c => c.Id == classYearId);
        ViewData["Title"] = ClassLabel(cy.SchoolClass);
        ViewData["ClassName"] = ClassLabel(cy.SchoolClass);
        ViewData["ClassYearId"] = classYearId;

        var list = await Db.Assessments.Where(a => a.Semester.AcademicYearId == cy.AcademicYearId && !(a.Semester.Number == 2 && cy.SchoolClass.FormLevel == 3))
            .OrderBy(a => a.Semester.Number).ThenBy(a => a.Type).Select(a => new { a.Id, Name = "Semester " + a.Semester.Number + " · " + a.Title }).ToListAsync();
        assessmentId ??= list.Select(a => (int?)a.Id).FirstOrDefault();
        ViewBag.Assessments = new SelectList(list, "Id", "Name", assessmentId);
        ViewData["AssessmentId"] = assessmentId;
        if (assessmentId == null) return View((MatrixVm?)null);

        var students = await Db.Enrollments.CountAsync(e => e.ClassYearId == classYearId && e.Outcome == EnrollmentOutcome.Pending && e.Student.Status == StudentStatus.Active);
        var stats = await Db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.AssessmentId == assessmentId)
            .GroupBy(r => new { r.SubjectId, r.Status }).Select(g => new { g.Key.SubjectId, g.Key.Status, N = g.Count() }).ToListAsync();
        ViewData["Subjects"] = await Db.ClassSubjects.Where(s => s.SchoolClassId == cy.SchoolClassId).OrderBy(s => s.Subject.Name)
            .Select(s => new { s.SubjectId, s.Subject.Name, Teacher = Db.TeachingAssignments.Where(t => t.ClassYearId == classYearId && t.SubjectId == s.SubjectId).Select(t => t.Teacher.User.FullName).FirstOrDefault() })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(s => new SubjectStatus(s.SubjectId, s.Name, s.Teacher, students,
                stats.Where(x => x.SubjectId == s.SubjectId).ToDictionary(x => x.Status, x => x.N))).ToList());
        return View(await matrix.BuildAsync(classYearId, assessmentId.Value));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int classYearId, int assessmentId)
    {
        await Run(() => workflow.ReviewAsync(TeacherId, classYearId, assessmentId, UserId), "Reviewed and sent to the admin for approval.");
        return RedirectToAction(nameof(Class), new { classYearId, assessmentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(int classYearId, int assessmentId, int subjectId, string note)
    {
        if (string.IsNullOrWhiteSpace(note)) Fail("Add a short note telling the teacher what to correct.");
        else await Run(() => workflow.ReturnAsync(TeacherId, classYearId, subjectId, assessmentId, note.Trim(), UserId), "Sent back to the subject teacher.");
        return RedirectToAction(nameof(Class), new { classYearId, assessmentId });
    }

    public async Task<IActionResult> Promotion(int classYearId)
    {
        if (!await IsCoordinatorAsync(classYearId)) return Forbid();
        var cy = await Db.ClassYears.Include(c => c.SchoolClass).ThenInclude(s => s.Course).SingleAsync(c => c.Id == classYearId);
        ViewData["Title"] = "Promotion · " + ClassLabel(cy.SchoolClass);
        ViewData["ClassYearId"] = classYearId; ViewData["ClassName"] = ClassLabel(cy.SchoolClass); ViewData["Form"] = cy.SchoolClass.FormLevel;
        var rows = await Db.Enrollments.Where(e => e.ClassYearId == classYearId && e.Outcome == EnrollmentOutcome.Pending && e.Student.Status == StudentStatus.Active)
            .OrderBy(e => e.Student.User.FullName)
            .Select(e => new PromotionRow(e.Id, e.Student.User.FullName, e.Student.StudentNumber,
                Db.Results.Where(r => r.EnrollmentId == e.Id).Average(r => (decimal?)r.Total),
                Db.PromotionRecommendations.Where(p => p.EnrollmentId == e.Id).Select(p => (PromotionDecision?)p.Decision).FirstOrDefault())).ToListAsync();
        ViewData["Submitted"] = await Db.PromotionRecommendations.AnyAsync(p => p.Enrollment.ClassYearId == classYearId && p.SubmittedToAdmin);
        ViewData["Approved"] = await Db.PromotionRecommendations.AnyAsync(p => p.Enrollment.ClassYearId == classYearId && p.AdminDecision == AdminDecision.Approved);
        ViewData["Note"] = await Db.PromotionRecommendations.Where(p => p.Enrollment.ClassYearId == classYearId && p.AdminDecision == AdminDecision.Rejected).Select(p => p.AdminNote).FirstOrDefaultAsync();
        return View(rows);
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePromotion(int classYearId, Dictionary<int, PromotionDecision> decisions, bool submit = false)
    {
        var ok = await Run(() => promotion.RecommendAsync(TeacherId, classYearId, decisions.Select(d => (d.Key, d.Value))), submit ? null : "Recommendations saved.");
        if (ok && submit) await Run(() => promotion.SubmitToAdminAsync(TeacherId, classYearId), "Promotion list sent to the admin.");
        return RedirectToAction(nameof(Promotion), new { classYearId });
    }




}


