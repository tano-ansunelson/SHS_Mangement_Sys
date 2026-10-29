using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Admin.Controllers;


public record AssessmentRow(int Id, string Title, AssessmentType Type, bool IsOpen, int Results);
public record SemesterRow(int Id, int Number, List<AssessmentRow> Assessments);
public record YearRow(int Id, string Name, bool IsCurrent, List<SemesterRow> Semesters, int Classes, int Students);
public class YearsController(AppDbContext db, AcademicSetupService setup) : AdminBase(db)
{
    public async Task<IActionResult> Index()
    {
        var years = await Db.AcademicYears.OrderByDescending(y => y.Name).ToListAsync();
        var sems = await Db.Semesters.OrderBy(s => s.Number).ToListAsync();
        var asms = await Db.Assessments.Select(a => new { a.Id, a.SemesterId, a.Title, a.Type, a.IsOpen, Results = Db.Results.Count(r => r.AssessmentId == a.Id) }).ToListAsync();
        var rows = new List<YearRow>();
        foreach (var y in years)
            rows.Add(new YearRow(y.Id, y.Name, y.IsCurrent,
                sems.Where(s => s.AcademicYearId == y.Id).Select(s => new SemesterRow(s.Id, s.Number,
                    asms.Where(a => a.SemesterId == s.Id).OrderBy(a => a.Type).Select(a => new AssessmentRow(a.Id, a.Title, a.Type, a.IsOpen, a.Results)).ToList())).ToList(),
                await Db.ClassYears.CountAsync(c => c.AcademicYearId == y.Id), await Db.Enrollments.CountAsync(e => e.AcademicYearId == y.Id)));
        return View(rows);
    }

    public IActionResult Create() => View("Form", new YearVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(YearVm vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        if (!await Run(() => setup.CreateAcademicYearAsync(vm.Name, vm.MakeCurrent), "Academic year created with two semesters and a record for every class.")) return View("Form", vm);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCurrent(int id)
    {
        await Run(() => setup.SetCurrentYearAsync(id), "Current academic year changed.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> CreateAssessment(int? semesterId)
    {
        await LoadSemesters(semesterId);
        return View("AssessmentForm", new AssessmentVm { SemesterId = semesterId ?? 0 });
    }




    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssessment(AssessmentVm vm)
    {
        if (vm.SemesterId == 0) ModelState.AddModelError(nameof(vm.SemesterId), "Select a semester.");
        if (ModelState.IsValid && await Run(() => setup.CreateAssessmentAsync(vm.SemesterId, vm.Type, vm.Title), "Examination created. Teachers and students can now see it.")) return RedirectToAction(nameof(Index));
        await LoadSemesters(vm.SemesterId);
        return View("AssessmentForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAssessment(int id)
    {
        var a = await Db.Assessments.FindAsync(id);
        if (a == null) return NotFound();
        a.IsOpen = !a.IsOpen;
        await Db.SaveChangesAsync();
        Flash(a.IsOpen ? "Assessment re-opened for score entry." : "Assessment closed.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DeleteAssessment(int id)
    {
        var a = await Db.Assessments.FindAsync(id);
        return a == null ? NotFound() : Confirm("Delete examination", $"Delete “{a.Title}”? Only possible while no scores exist.", Url.Action(nameof(DeleteAssessmentConfirmed))!, id, Url.Action(nameof(Index))!);
    }





    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAssessmentConfirmed(int id)
    {
        if (await Db.Results.AnyAsync(r => r.AssessmentId == id)) Fail("Scores already exist for this examination.");
        else { Db.Assessments.Remove((await Db.Assessments.FindAsync(id))!); await Db.SaveChangesAsync(); Flash("Examination deleted."); }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadSemesters(int? selected) =>
        ViewBag.Semesters = new SelectList((await Db.Semesters.OrderByDescending(s => s.AcademicYear.Name).ThenBy(s => s.Number)
            .Select(s => new { s.Id, Name = s.AcademicYear.Name + " · Semester " + s.Number }).ToListAsync()), "Id", "Name", selected);










}

