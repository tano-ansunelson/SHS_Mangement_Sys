using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Admin.Controllers;

public record ClassProgress(int ClassYearId, string Name, int Students, int Subjects, Dictionary<ResultStatus, int> Counts, int GroupSize = 1)
{
    public int Total => Counts.Values.Sum();
    public int Expected => Students * Subjects * GroupSize;
    public int Of(ResultStatus s) => Counts.GetValueOrDefault(s);
    public bool CanApprove => Expected > 0 && Of(ResultStatus.Reviewed) == Expected;
    public bool CanPublish => Expected > 0 && Of(ResultStatus.Approved) == Expected;
}
public class ResultsController(AppDbContext db, ResultWorkflowService workflow, MatrixService matrix) : AdminBase(db)
{
    /// <summary>Pick an academic year and a semester; the semester's final exam (with its midterm) is found automatically.
    /// Earlier years work the same way, so past results can still be approved, published or looked at.</summary>
    public async Task<IActionResult> Index(int? yearId, int? semester, int? assessmentId)
    {
        // links from the class page and the approve/publish buttons still pass an exam id: turn it into a year and semester
        if (assessmentId != null && yearId == null)
        {
            var from = await Db.Assessments.Where(a => a.Id == assessmentId).Select(a => new { a.Semester.AcademicYearId, a.Semester.Number }).FirstOrDefaultAsync();
            if (from != null) { yearId = from.AcademicYearId; semester = from.Number; }
        }
        var years = await Db.AcademicYears.OrderByDescending(y => y.Name).ToListAsync();
        yearId ??= years.FirstOrDefault(y => y.IsCurrent)?.Id ?? years.FirstOrDefault()?.Id;
        semester ??= yearId == null ? 1 : await Db.Semesters.Where(s => s.AcademicYearId == yearId && s.IsCurrent).Select(s => (int?)s.Number).FirstOrDefaultAsync() ?? 1;
        ViewBag.Years = new SelectList(years, "Id", "Name", yearId);
        ViewData["YearId"] = yearId; ViewData["Semester"] = semester;

        var rows = new List<ClassProgress>();
        assessmentId = yearId == null ? null : await Db.Assessments
            .Where(a => a.Semester.AcademicYearId == yearId && a.Semester.Number == semester && a.Type == AssessmentType.Final)
            .Select(a => (int?)a.Id).FirstOrDefaultAsync();
        ViewData["AssessmentId"] = assessmentId;

        // approving or publishing a final also covers the same semester's midterm scores
        if (assessmentId != null)
        {
            var picked = await Db.Assessments.Where(a => a.Id == assessmentId).Select(a => new { a.Semester.Number, a.SemesterId }).FirstAsync();
            var sem = picked.Number;
            var groupIds = await Db.Assessments.Where(a => a.SemesterId == picked.SemesterId && (a.Id == assessmentId || a.Type == AssessmentType.Midterm)).Select(a => a.Id).ToListAsync();
            var cys = await Db.ClassYears.Where(c => c.AcademicYearId == yearId && !(sem == 2 && c.SchoolClass.FormLevel == 3))
                .OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
                .Select(c => new
                {
                    c.Id,
                    Name = "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group,
                    Students = Db.Enrollments.Count(e => e.ClassYearId == c.Id && e.Outcome != EnrollmentOutcome.Withdrawn && e.Student.Status != StudentStatus.Withdrawn),
                    Subjects = Db.ClassSubjects.Count(s => s.SchoolClassId == c.SchoolClassId)
                }).ToListAsync();
            var stats = await Db.Results.Where(r => groupIds.Contains(r.AssessmentId))
                .GroupBy(r => new { r.Enrollment.ClassYearId, r.Status }).Select(g => new { g.Key.ClassYearId, g.Key.Status, N = g.Count() }).ToListAsync();
            foreach (var c in cys)
                rows.Add(new ClassProgress(c.Id, c.Name, c.Students, c.Subjects, stats.Where(s => s.ClassYearId == c.Id).GroupBy(s => s.Status).ToDictionary(g => g.Key, g => g.Sum(x => x.N)), groupIds.Count));
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

