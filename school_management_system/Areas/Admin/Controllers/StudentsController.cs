using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Admin.Controllers;

public record StudentRow(int Id, string Number, string Name, string? Class, string Course, ResidenceType Residence, string? Hall, string? Room, StudentStatus Status);

public class StudentsController(AppDbContext db, StudentService students, AccountAdminService accounts, ResultWorkflowService results) : AdminBase(db)
{
    private const int PageSize = 25;

    public async Task<IActionResult> Index(string? q, int? courseId, int? classYearId, ResidenceType? residence, StudentStatus? status, int page = 1)
    {
        var yearId = await CurrentYearIdAsync();
        var query = Db.Students.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(s => s.User.FullName.Contains(q) || s.StudentNumber.Contains(q));
        if (courseId != null) query = query.Where(s => s.CourseId == courseId);
        if (classYearId != null) query = query.Where(s => Db.Enrollments.Any(e => e.StudentId == s.Id && e.ClassYearId == classYearId));
        if (residence != null) query = query.Where(s => s.Residence == residence);
        query = query.Where(s => s.Status == (status ?? StudentStatus.Active));

        var total = await query.CountAsync();
        var rows = await query.OrderBy(s => s.User.FullName).Skip((page - 1) * PageSize).Take(PageSize).Select(s => new StudentRow(s.Id, s.StudentNumber, s.User.FullName,
            Db.Enrollments.Where(e => e.StudentId == s.Id && e.AcademicYearId == yearId).Select(e => "Form " + e.ClassYear.SchoolClass.FormLevel + " " + e.ClassYear.SchoolClass.Group).FirstOrDefault(),
            s.Course.Name, s.Residence,
            Db.HallRegistrations.Where(h => h.StudentId == s.Id && h.AcademicYearId == yearId).Select(h => h.Hall.Name).FirstOrDefault(),
            Db.RoomAllocations.Where(a => a.StudentId == s.Id && a.AcademicYearId == yearId).Select(a => a.Room.Number).FirstOrDefault(), s.Status)).ToListAsync();

        ViewBag.Courses = new SelectList(await Db.Courses.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", courseId);
        ViewBag.Classes = new SelectList(await ClassOptions(yearId), "Id", "Name", classYearId);
        ViewData["Q"] = q; ViewData["Residence"] = residence; ViewData["Status"] = status ?? StudentStatus.Active;
        ViewData["Page"] = page; ViewData["Pages"] = (int)Math.Ceiling(total / (double)PageSize); ViewData["Total"] = total;
        return View(rows);
    }


    public async Task<IActionResult> Create() => View("Form", await Load(new StudentVm()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentVm vm)
    {
        if (vm.ClassYearId == 0) ModelState.AddModelError(nameof(vm.ClassYearId), "Select a class.");
        if (vm.Residence == ResidenceType.Boarding && vm.HallId == null) ModelState.AddModelError(nameof(vm.HallId), "Select a hall for a boarding student.");
        if (!ModelState.IsValid) return View("Form", await Load(vm));
        AccountCredentials? creds = null;
        var ok = await Run(async () => creds = await students.RegisterAsync(new RegisterStudentRequest(vm.FullName, vm.Gender, vm.DateOfBirth!.Value,
            vm.GuardianName, vm.GuardianPhone, vm.ClassYearId, vm.Residence, vm.Residence == ResidenceType.Boarding ? vm.HallId : null)));
        if (!ok) return View("Form", await Load(vm));
        ViewData["Back"] = Url.Action(nameof(Index));
        return View("Credentials", creds);
    }

    public async Task<IActionResult> Details(int id)
    {
        var s = await Db.Students.Include(x => x.User).Include(x => x.Course).SingleOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        ViewData["Title"] = s.User.FullName;
        ViewData["History"] = await Db.Enrollments.Where(e => e.StudentId == id).OrderBy(e => e.AcademicYear.Name)
            .Select(e => new EnrollmentRow(e.AcademicYear.Name, "Form " + e.ClassYear.SchoolClass.FormLevel + " " + e.ClassYear.SchoolClass.Course.Name + " " + e.ClassYear.SchoolClass.Group, e.Outcome,
                Db.HallRegistrations.Where(h => h.StudentId == id && h.AcademicYearId == e.AcademicYearId).Select(h => h.Hall.Name).FirstOrDefault(),
                Db.RoomAllocations.Where(a => a.StudentId == id && a.AcademicYearId == e.AcademicYearId).Select(a => a.Room.Number).FirstOrDefault())).ToListAsync();
        ViewData["Results"] = await results.GetPublishedForStudentAsync(id);
        return View(s);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var s = await Db.Students.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        var yearId = await CurrentYearIdAsync();
        var vm = new StudentVm
        {
            Id = s.Id,
            FullName = s.User.FullName,
            Gender = s.Gender,
            DateOfBirth = s.DateOfBirth,
            GuardianName = s.GuardianName,
            GuardianPhone = s.GuardianPhone,
            Residence = s.Residence,
            HallId = await Db.HallRegistrations.Where(h => h.StudentId == id && h.AcademicYearId == yearId).Select(h => (int?)h.HallId).FirstOrDefaultAsync()
        };
        return View("Form", await Load(vm));
    }



    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StudentVm vm)
    {
        var s = await Db.Students.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == vm.Id);
        if (s == null) return NotFound();
        ModelState.Remove(nameof(vm.ClassYearId));
        if (vm.Residence == ResidenceType.Boarding && vm.HallId == null) ModelState.AddModelError(nameof(vm.HallId), "Select a hall for a boarding student.");
        if (!ModelState.IsValid) return View("Form", await Load(vm));

        s.User.FullName = vm.FullName.Trim(); s.Gender = vm.Gender; s.DateOfBirth = vm.DateOfBirth!.Value;
        s.GuardianName = vm.GuardianName; s.GuardianPhone = vm.GuardianPhone; s.Residence = vm.Residence;

        var yearId = await CurrentYearIdAsync();
        if (yearId != null)
        {
            var reg = await Db.HallRegistrations.SingleOrDefaultAsync(h => h.StudentId == s.Id && h.AcademicYearId == yearId);
            var alloc = await Db.RoomAllocations.Include(a => a.Room).SingleOrDefaultAsync(a => a.StudentId == s.Id && a.AcademicYearId == yearId);
            var newHall = vm.Residence == ResidenceType.Boarding ? vm.HallId : null;
            if (reg != null && reg.HallId != newHall) { Db.HallRegistrations.Remove(reg); reg = null; }
            if (alloc != null && alloc.Room.HallId != newHall) Db.RoomAllocations.Remove(alloc);
            if (reg == null && newHall != null) Db.HallRegistrations.Add(new HallRegistration { StudentId = s.Id, HallId = newHall.Value, AcademicYearId = yearId.Value });
        }
        await Db.SaveChangesAsync();
        Flash("Student updated.");
        return RedirectToAction(nameof(Details), new { id = s.Id });
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var s = await Db.Students.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        string? pw = null;
        if (!await Run(async () => pw = await accounts.ResetPasswordAsync(s.User))) return RedirectToAction(nameof(Details), new { id });
        ViewData["Back"] = Url.Action(nameof(Details), new { id });
        return View("Credentials", new AccountCredentials(s.User.FullName, s.User.Email!, pw!, s.StudentNumber));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var s = await Db.Students.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        return s == null ? NotFound() : Confirm("Withdraw student", $"Withdraw {s.User.FullName}? Their login is disabled but all records are kept.", Url.Action(nameof(DeleteConfirmed))!, id, Url.Action(nameof(Details), new { id })!, "Withdraw");
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var s = await Db.Students.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        s.Status = StudentStatus.Withdrawn; s.User.IsActive = false;
        foreach (var e in await Db.Enrollments.Where(e => e.StudentId == id && e.Outcome == EnrollmentOutcome.Pending).ToListAsync()) e.Outcome = EnrollmentOutcome.Withdrawn;
        var yearId = await CurrentYearIdAsync();
        Db.RoomAllocations.RemoveRange(Db.RoomAllocations.Where(a => a.StudentId == id && a.AcademicYearId == yearId));
        await Db.SaveChangesAsync();
        Flash("Student withdrawn.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<ClassOptionRow>> ClassOptions(int? yearId) =>
        await Db.ClassYears.Where(c => c.AcademicYearId == yearId).OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
            .Select(c => new ClassOptionRow(c.Id, "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group, c.SchoolClass.CourseId)).ToListAsync();

    private async Task<StudentVm> Load(StudentVm vm)
    {
        var yearId = await CurrentYearIdAsync();
        ViewData["ClassData"] = await Db.ClassYears.Where(c => c.AcademicYearId == yearId).OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
            .Select(c => new ClassOptionRow(c.Id, "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group, c.SchoolClass.CourseId)).ToListAsync();
        ViewBag.Courses = new SelectList(await Db.Courses.OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
        ViewBag.Halls = new SelectList(await Db.Halls.OrderBy(h => h.Name).ToListAsync(), "Id", "Name", vm.HallId);
        return vm;
    }



}

public record EnrollmentRow(string Year, string Class, EnrollmentOutcome Outcome, string? Hall, string? Room);
public record ClassOptionRow(int Id, string Name, int CourseId);