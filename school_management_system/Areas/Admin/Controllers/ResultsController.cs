using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Admin.Controllers;

public record ClassProgress(int ClassYearId, string Name, int Students, int Subjects, Dictionary<ResultStatus, int> Counts)
{
    public int Total => Counts.Values.Sum();
    public int Expected => Students * Subjects;
    public int Of(ResultStatus s) => Counts.GetValueOrDefault(s);
    public bool CanApprove => Expected > 0 && Of(ResultStatus.Reviewed) == Expected;
    public bool CanPublish => Expected > 0 && Of(ResultStatus.Approved) == Expected;
}
public class ResultsController(AppDbContext db, ResultWorkflowService workflow, MatrixService matrix) : AdminBase(db)
{
    public async Task<IActionResult> Index(int? assessmentId)
    {
        var y = await CurrentYearIdAsync();
        var list = await Db.Assessments.Where(a => a.Semester.AcademicYearId == y)
            .OrderBy(a => a.Semester.Number)
            .ThenBy(a => a.Type)
            .Select(a => new { a.Id, Name = "Semester" + a.Semester.Number + " . " + a.Title }).ToListAsync();
        ViewBag.Assessments = new SelectList(list, "Id", "Name", assessmentId);
        ViewData["AssessmentId"] = assessmentId;
        var rows = new List<ClassProgress>();

        if (assessmentId != null)
        {
            var sem = await Db.Assessments.Where(a => a.Id == assessmentId).Select(a => a.Semester.Number).FirstAsync();
            var cys = await Db.ClassYears.Where(c => c.AcademicYearId == y && !(sem == 2 && c.SchoolClass.FormLevel == 3))
                .OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
                .Select(c => new
                {
                    c.Id,
                    Name = "Form" + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group,
                    Students = Db.Enrollments.Count(e => e.ClassYearId == c.Id && e.Outcome == EnrollmentOutcome.Pending && e.Student.Status == StudentStatus.Active),
                    Subjects = Db.ClassSubjects.Count(s => s.SchoolClassId == c.SchoolClassId)

                }).ToListAsync();

            var stats = await Db.Results.Where(r => r.AssessmentId == assessmentId)
              .GroupBy(r => new { r.Enrollment.ClassYearId, r.Status }).Select(g => new { g.Key.ClassYearId, g.Key.Status, N = g.Count() }).ToListAsync();
            foreach (var c in cys)
                rows.Add(new ClassProgress(c.Id, c.Name, c.Students, c.Subjects, stats.Where(s => s.ClassYearId == c.Id).ToDictionary(s => s.Status, s => s.N)));

        }
        return View(rows);
    }


    public async Task<IActionResult> Class(int classYearId, int assessmentId)
    {
        ViewData["Title"] = "Class results";
        try { return View(await matrix.BuildAsync(classYearId, assessmentId)); }
        catch (DomainException e) { Fail(e.Message); return RedirectToAction(nameof(Index)); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int classYearId, int assessmentId)
    {
        await Run(() => workflow.ApproveAsync(classYearId, assessmentId, UserId), "Results approved.");
        return RedirectToAction(nameof(Index), new { assessmentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int classYearId, int assessmentId)
    {
        await Run(() => workflow.PublishAsync(classYearId, assessmentId, UserId), "Results published. Students can now see them.");
        return RedirectToAction(nameof(Index), new { assessmentId });
    }




}

