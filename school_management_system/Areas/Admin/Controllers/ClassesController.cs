using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Admin.Controllers;
public class ClassesController(AppDbContext db, AcademicSetupService setup) : AdminBase(db) 
{


    public async Task<IActionResult> Index() =>
        View(
            await Db.SchoolClasses.OrderBy(c => c.Course.Name)
            .ThenBy(c => c.FormLevel).ThenBy(c => c.Group)
            .Select(c => new ClassRow(c.Id, c.Course.Name, c.FormLevel, c.Group,
                Db.ClassSubjects.Count(s => s.SchoolClassId == c.Id),
                Db.Enrollments.Count(e => e.ClassYear.SchoolClassId == c.Id && e.AcademicYear.IsCurrent && e.Outcome == EnrollmentOutcome.Pending))).ToListAsync());




    public async Task<IActionResult> Create() => View("Form", await Load(new ClassVm()));


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClassVm vm)
    {
        if (!ModelState.IsValid)
            return View("Form", await Load(vm));
        try
        {
            await setup.CreateClassAsync(vm.CourseId, vm.FormLevel, vm.Group, vm.SubjectIds);
        }
        catch(DomainException e)
        {
            ModelState.AddModelError("", e.Message);
            return View("Form", await Load(vm));
        }
        Flash("Class created.");
        return RedirectToAction(nameof(Index));
    }



    public async Task<IActionResult> Edit(int id)
    {
        var c = await Db.SchoolClasses.FindAsync(id);
        if (c == null) return NotFound();

        var vm = new ClassVm
        { 
            Id = c.Id,
            CourseId = c.CourseId,
            FormLevel = c.FormLevel,
            Group = c.Group,
            SubjectIds = await Db.ClassSubjects.Where(x => x.SchoolClassId == id).Select(x => x.SubjectId).ToListAsync()
        };
        return View("Form", await Load(vm));
    }


    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClassVm vm)
    {
        if (!await Db.SchoolClasses.AnyAsync(c => c.Id == vm.Id)) 
            return NotFound();
        try
        {
            await setup.SetClassSubjectsAsync(vm.Id, vm.SubjectIds);
        }catch(DomainException e)
        {
            ModelState.AddModelError("", e.Message); 
            return View("Form", await Load(vm));
        }

        Flash("Class subjects updated.");
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }




    public async Task<IActionResult> Details(int id)
    {
        var c = await Db.SchoolClasses.Include(x => x.Course).SingleOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        ViewData["Title"] = ClassLabel(c);
        var cy = await Db.ClassYears.Include(x => x.CoordinatorTeacher!.User)
            .SingleOrDefaultAsync(x => x.SchoolClassId == id && x.AcademicYear.IsCurrent);
        ViewData["Class"] = c;
        ViewData["ClassYear"] = cy;
        var cyId = cy?.Id ?? 0;
        ViewData["Subjects"] = await Db.ClassSubjects.Where(s => s.SchoolClassId == id).OrderBy(s => s.Subject.Name)
          .Select(s => new SubjectTeacherRow(s.Subject.Name, s.Subject.Code,
              Db.TeachingAssignments.Where(t => t.ClassYearId == cyId
              && t.SubjectId == s.SubjectId)
              .Select(t => t.Teacher.User.FullName).FirstOrDefault()))
          .ToListAsync();
        ViewData["Students"] = cy == null ? new List<PersonRow>() : await Db.Enrollments.Where(e => e.ClassYearId == cy.Id && e.Outcome == EnrollmentOutcome.Pending)
           .OrderBy(e => e.Student.User.FullName).Select(e => new PersonRow(e.Id, e.StudentId, e.Student.User.FullName, e.Student.StudentNumber, e.Student.Residence)).ToListAsync();
        ViewBag.Teachers = new SelectList(await Db.Teachers.Where(t => t.IsActive).OrderBy(t => t.User.FullName).Select(t => new { t.Id, t.User.FullName }).ToListAsync(), "Id", "FullName", cy?.CoordinatorTeacherId);


        return View(c);
    }





    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCoordinator(int clasYearId, int? teacherId)
    {
        var cy = await Db.ClassYears.FindAsync(clasYearId);
        if (cy == null) return NotFound();
        cy.CoordinatorTeacherId = teacherId;
        await Db.SaveChangesAsync();
        Flash(teacherId == null ? "Coordinator removed." : "Coordinator updated.");

        return RedirectToAction(nameof(Details), new { id = cy.SchoolClassId });
    }



    public async Task<IActionResult> Delete(int id)
    {
        var c = await Db.SchoolClasses.Include(x => x.Course).SingleOrDefaultAsync(x => x.Id == id);
        return c == null ? NotFound() : Confirm("Delete class", $"Delete{ClassLabel(c)}? only empty classes can be deleted.",
            Url.Action(nameof(DeleteConfirmed))!, id, Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await Db.Enrollments.AnyAsync(e => e.ClassYear.SchoolClassId == id) || await Db.TeachingAssignments.AnyAsync(t => t.ClassYear.SchoolClassId == id))
        { Fail("This class has students or teaching records and is kept for history."); return RedirectToAction(nameof(Index)); }
        Db.ClassYears.RemoveRange(Db.ClassYears.Where(x => x.SchoolClassId == id));
        Db.ClassSubjects.RemoveRange(Db.ClassSubjects.Where(x => x.SchoolClassId == id));
        Db.SchoolClasses.Remove((await Db.SchoolClasses.FindAsync(id))!);
        await Db.SaveChangesAsync();
        Flash("Class deleted.");
        return RedirectToAction(nameof(Index));
    }



    private async Task<ClassVm> Load(ClassVm vm)
    {
        vm.AllSubjects = await Db.Subjects.OrderBy(s => s.Name).ToListAsync();
        vm.Courses = await Db.Courses.OrderBy(c => c.Name).ToListAsync();
        vm.CourseSubjects = (await Db.CourseSubjects.ToListAsync()).GroupBy(x => x.CourseId).ToDictionary(g => g.Key, g => g.Select(x => x.SubjectId).ToList());
        return vm;
    }
}

