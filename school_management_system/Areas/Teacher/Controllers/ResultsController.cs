using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using school_management_system.Data;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Teacher.Controllers;

public record EntryProgress(int ClassYearId, int SubjectId, string Subject, string ClassName, int Students, Dictionary<ResultStatus, int> Counts)
{
    public int Entered => Counts.Values.Sum();
    public bool Submitted => Entered > 0 && Counts.Keys.All(k => k != ResultStatus.Draft && k != ResultStatus.Returned);
    public bool HasReturned => Counts.ContainsKey(ResultStatus.Returned);
}
public class ResultsController(AppDbContext db, DashboardQueries q, ResultWorkflowService workflow, IOptions<GradingOptions> grading) : TeacherBase(db, q)
{
    public async Task<IActionResult> Index(int? assessmentId)
    {
        ViewData["Title"] = "Enter results";
        var y = await CurrentYearIdAsync();
        var list = await Db.Assessments.Where(a => a.Semester.AcademicYearId == y).OrderBy(a => a.Semester.Number).ThenBy(a => a.Type)
            .Select(a => new { a.Id, Name = "Semester " + a.Semester.Number + " · " + a.Title + (a.IsOpen ? "" : " (closed)") }).ToListAsync();
        assessmentId ??= list.Select(a => (int?)a.Id).FirstOrDefault();
        ViewBag.Assessments = new SelectList(list, "Id", "Name", assessmentId);
        ViewData["AssessmentId"] = assessmentId;
        var rows = new List<EntryProgress>();
        if (assessmentId != null)
        {
            var sem = await Db.Assessments.Where(a => a.Id == assessmentId).Select(a => a.Semester.Number).FirstAsync();
            var mine = await Db.TeachingAssignments.Where(t => t.TeacherId == TeacherId && t.ClassYear.AcademicYearId == y && !(sem == 2 && t.ClassYear.SchoolClass.FormLevel == 3))
                .OrderBy(t => t.Subject.Name)
                .Select(t => new {
                    t.ClassYearId,
                    t.SubjectId,
                    Subject = t.Subject.Name,
                    ClassName = "Form " + t.ClassYear.SchoolClass.FormLevel + " " + t.ClassYear.SchoolClass.Course.Name + " " + t.ClassYear.SchoolClass.Group,
                    Students = Db.Enrollments.Count(e => e.ClassYearId == t.ClassYearId && e.Outcome == EnrollmentOutcome.Pending && e.Student.Status == StudentStatus.Active)
                }).ToListAsync();
            var stats = await Db.Results.Where(r => r.AssessmentId == assessmentId && Db.TeachingAssignments.Any(t => t.TeacherId == TeacherId && t.ClassYearId == r.Enrollment.ClassYearId && t.SubjectId == r.SubjectId))
                .GroupBy(r => new { r.Enrollment.ClassYearId, r.SubjectId, r.Status }).Select(g => new { g.Key.ClassYearId, g.Key.SubjectId, g.Key.Status, N = g.Count() }).ToListAsync();
            foreach (var m in mine)
                rows.Add(new EntryProgress(m.ClassYearId, m.SubjectId, m.Subject, m.ClassName, m.Students,
                    stats.Where(s => s.ClassYearId == m.ClassYearId && s.SubjectId == m.SubjectId).ToDictionary(s => s.Status, s => s.N)));
        }
        return View(rows);
    }

    public async Task<IActionResult> Entry(int classYearId, int subjectId, int assessmentId)
    {
        if (!await TeacherAsync(classYearId, subjectId)) return Forbid();
        var asm = await Db.Assessments.Include(a => a.Semester).SingleOrDefaultAsync(a => a.Id == assessmentId);
        var cy = await Db.ClassYears.Include(c => c.SchoolClass).ThenInclude(s => s.Course).SingleAsync(c => c.Id == classYearId);
        if (asm == null) return NotFound();
        if (cy.SchoolClass.FormLevel == 3 && asm.Semester.Number == 2) { Fail("Form 3 has no second-semester examinations (WASSCE)."); return RedirectToAction(nameof(Index)); }

        var students = await Db.Enrollments.Where(e => e.ClassYearId == classYearId && e.Outcome == EnrollmentOutcome.Pending && e.Student.Status == StudentStatus.Active)
            .OrderBy(e => e.Student.User.FullName).Select(e => new { e.Id, Name = e.Student.User.FullName, Number = e.Student.StudentNumber }).ToListAsync();
        var existing = await Db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.SubjectId == subjectId && r.AssessmentId == assessmentId).ToListAsync();
        var vm = new EntryVm
        {
            ClassYearId = classYearId,
            SubjectId = subjectId,
            AssessmentId = assessmentId,
            ClassName = $"Form {cy.SchoolClass.FormLevel} {cy.SchoolClass.Course.Name} {cy.SchoolClass.Group}",
            SubjectName = (await Db.Subjects.FindAsync(subjectId))!.Name,
            AssessmentTitle = $"Semester {asm.Semester.Number} · {asm.Title}",
            MaxClass = grading.Value.MaxClassScore,
            MaxExam = grading.Value.MaxExamScore
        };
        foreach (var s in students)
        {
            var r = existing.FirstOrDefault(x => x.EnrollmentId == s.Id);
            vm.Rows.Add(new EntryRow { EnrollmentId = s.Id, Name = s.Name, Number = s.Number, ClassScore = r?.ClassScore, ExamScore = r?.ExamScore, Status = r?.Status });
        }
        vm.Locked = !asm.IsOpen || (existing.Count > 0 && existing.All(r => r.Status is not (ResultStatus.Draft or ResultStatus.Returned)));
        ViewData["Title"] = $"{vm.SubjectName} · {vm.ClassName}";
        ViewData["ReturnNote"] = existing.FirstOrDefault(r => r.Status == ResultStatus.Returned)?.ReviewNote;
        return View(vm);
    }





    public async Task<IActionResult> Save(EntryVm vm)
    {
        var errors = new List<string>(); var saved = 0;
        foreach (var r in vm.Rows.Where(r => r.ClassScore != null || r.ExamScore != null))
        {
            if (r.ClassScore == null || r.ExamScore == null) { errors.Add("Enter both class and exam score for every student you fill in."); continue; }
            try { await workflow.SaveDraftAsync(TeacherId, r.EnrollmentId, vm.SubjectId, vm.AssessmentId, r.ClassScore.Value, r.ExamScore.Value, UserId); saved++; }
            catch (DomainException e) { errors.Add(e.Message); }
        }
        if (errors.Count > 0) Fail(string.Join(" ", errors.Distinct())); else Flash($"Saved {saved} score(s).");
        return RedirectToAction(nameof(Entry), new { vm.ClassYearId, vm.SubjectId, vm.AssessmentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int classYearId, int subjectId, int assessmentId)
    {
        await Run(() => workflow.SubmitAsync(TeacherId, classYearId, subjectId, assessmentId, UserId), "Results submitted to the class coordinator.");
        return RedirectToAction(nameof(Entry), new { classYearId, subjectId, assessmentId });
    }






}

