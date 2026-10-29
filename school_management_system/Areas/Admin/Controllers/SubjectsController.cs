using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace school_management_system.Areas.Admin.Controllers;

public class SubjectsController(AppDbContext db) : AdminBase(db)
{
    public async Task<IActionResult> Index() =>
        View(await Db.Subjects.OrderBy(s => s.Name).Select(s => new SubjectRow(s.Id, s.Name, s.Code,
            Db.CourseSubjects.Count(x => x.SubjectId == s.Id), Db.ClassSubjects.Count(x => x.SubjectId == s.Id))).ToListAsync());

    public IActionResult Create() => View("Form", new SubjectVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubjectVm vm)
    {
        vm.Code = vm.Code?.Trim().ToUpperInvariant() ?? "";
        if (await Db.Subjects.AnyAsync(s => s.Code == vm.Code)) ModelState.AddModelError(nameof(vm.Code), "This code is already used.");
        if (!ModelState.IsValid) return View("Form", vm);
        Db.Subjects.Add(new Subject { Name = vm.Name.Trim(), Code = vm.Code });
        await Db.SaveChangesAsync();
        Flash("Subject added.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var s = await Db.Subjects.FindAsync(id);
        return s == null ? NotFound() : View("Form", new SubjectVm { Id = s.Id, Name = s.Name, Code = s.Code });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SubjectVm vm)
    {
        var s = await Db.Subjects.FindAsync(vm.Id);
        if (s == null) return NotFound();
        vm.Code = vm.Code?.Trim().ToUpperInvariant() ?? "";
        if (await Db.Subjects.AnyAsync(x => x.Code == vm.Code && x.Id != vm.Id)) ModelState.AddModelError(nameof(vm.Code), "This code is already used.");
        if (!ModelState.IsValid) return View("Form", vm);
        s.Name = vm.Name.Trim(); s.Code = vm.Code;
        await Db.SaveChangesAsync();
        Flash("Subject updated.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var s = await Db.Subjects.FindAsync(id);
        return s == null ? NotFound() : Confirm("Delete subject", $"Delete “{s.Name}”?", Url.Action(nameof(DeleteConfirmed))!, id, Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await Db.ClassSubjects.AnyAsync(x => x.SubjectId == id) || await Db.TeachingAssignments.AnyAsync(x => x.SubjectId == id) || await Db.Results.AnyAsync(x => x.SubjectId == id))
        { Fail("This subject is used by classes, teachers or results and cannot be deleted."); return RedirectToAction(nameof(Index)); }
        Db.CourseSubjects.RemoveRange(Db.CourseSubjects.Where(x => x.SubjectId == id));
        Db.Subjects.Remove((await Db.Subjects.FindAsync(id))!);
        await Db.SaveChangesAsync();
        Flash("Subject deleted.");
        return RedirectToAction(nameof(Index));
    }




}

