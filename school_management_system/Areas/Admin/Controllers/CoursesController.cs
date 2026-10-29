using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;
using school_management_system.Service;
using Microsoft.EntityFrameworkCore;

namespace school_management_system.Areas.Admin.Controllers;

public class CoursesController(AppDbContext db, AcademicSetupService setup) : AdminBase(db)
{
    public async Task<IActionResult> Index()
    {
        var rows = await Db.Courses.OrderBy(c => c.Name).Select(c => new CourseRow(c.Id, c.Name,
            Db.CourseSubjects.Count(x => x.CourseId == c.Id), Db.SchoolClasses.Count(x => x.CourseId == c.Id),
            Db.Students.Count(s => s.CourseId == c.Id && s.Status == StudentStatus.Active))).ToListAsync();
        return View(rows);
    }



    public async Task<IActionResult> Create() => View("Form", await Load(new CourseVm()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseVm vm)
    {
        if (await Db.Courses.AnyAsync(c => c.Name == vm.Name)) ModelState.AddModelError(nameof(vm.Name), "A course with this name already exists.");
        if (!ModelState.IsValid) return View("Form", await Load(vm));
        var c = new Course { Name = vm.Name.Trim() };
        Db.Courses.Add(c);
        await Db.SaveChangesAsync();
        await setup.SetCourseSubjectsAsync(c.Id, vm.SubjectIds);
        Flash("Course created.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await Db.Courses.FindAsync(id);
        if (c == null) return NotFound();
        var vm = new CourseVm { Id = c.Id, Name = c.Name, SubjectIds = await Db.CourseSubjects.Where(x => x.CourseId == id).Select(x => x.SubjectId).ToListAsync() };
        return View("Form", await Load(vm));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseVm vm)
    {
        var c = await Db.Courses.FindAsync(vm.Id);
        if (c == null) return NotFound();
        if (await Db.Courses.AnyAsync(x => x.Name == vm.Name && x.Id != vm.Id)) ModelState.AddModelError(nameof(vm.Name), "A course with this name already exists.");
        if (!ModelState.IsValid) return View("Form", await Load(vm));
        c.Name = vm.Name.Trim();
        await Db.SaveChangesAsync();
        await setup.SetCourseSubjectsAsync(c.Id, vm.SubjectIds);
        Flash("Course updated. Existing classes keep their own subject lists.");
        return RedirectToAction(nameof(Index));
    }


    public async Task<IActionResult> Delete(int id)
    {
        var c = await Db.Courses.FindAsync(id);
        return c == null ? NotFound() : Confirm("Delete course", $"Delete “{c.Name}”? This cannot be undone.", Url.Action(nameof(DeleteConfirmed))!, id, Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await Db.SchoolClasses.AnyAsync(c => c.CourseId == id) || await Db.Students.AnyAsync(s => s.CourseId == id))
        { Fail("This course has classes or students and cannot be deleted."); return RedirectToAction(nameof(Index)); }
        Db.CourseSubjects.RemoveRange(Db.CourseSubjects.Where(x => x.CourseId == id));
        Db.Courses.Remove((await Db.Courses.FindAsync(id))!);
        await Db.SaveChangesAsync();
        Flash("Course deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<CourseVm> Load(CourseVm vm) { vm.AllSubjects = await Db.Subjects.OrderBy(s => s.Name).ToListAsync(); return vm; }
}