using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Admin.Controllers;

public record StudentRow(int Id, string Number, string Name, string? Class, string Course, ResidenceType Residence, string? Hall, string? Room, StudentStatus Status, string? PhotoPath);

public class StudentsController(AppDbContext db, StudentService students, AccountAdminService accounts, ResultWorkflowService results, FileStorageService files) : AdminBase(db)
{
    private const int PageSize = 25;

    public async Task<IActionResult> Index(string? q, int? courseId, int? classYearId, ResidenceType? residence, StudentStatus? status, int page = 1)
    {
        var yearId = await CurrentYearIdAsync();
        var query = Db.Students.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(s => s.User.FullName.Contains(q) || s.StudentNumber.Contains(q) || (s.AdmissionNumber != null && s.AdmissionNumber.Contains(q)));
        if (courseId != null) query = query.Where(s => s.CourseId == courseId);
        if (classYearId != null) query = query.Where(s => Db.Enrollments.Any(e => e.StudentId == s.Id && e.ClassYearId == classYearId));
        if (residence != null) query = query.Where(s => s.Residence == residence);
        query = query.Where(s => s.Status == (status ?? StudentStatus.Active));

        var total = await query.CountAsync();
        var rows = await query.OrderBy(s => s.User.FullName).Skip((page - 1) * PageSize).Take(PageSize).Select(s => new StudentRow(s.Id, s.StudentNumber, s.User.FullName,
            Db.Enrollments.Where(e => e.StudentId == s.Id && e.AcademicYearId == yearId).Select(e => "Form " + e.ClassYear.SchoolClass.FormLevel + " " + e.ClassYear.SchoolClass.Group).FirstOrDefault(),
            s.Course.Name, s.Residence,
            Db.HallRegistrations.Where(h => h.StudentId == s.Id && h.AcademicYearId == yearId).Select(h => h.Hall.Name).FirstOrDefault(),
            Db.RoomAllocations.Where(a => a.StudentId == s.Id && a.AcademicYearId == yearId).Select(a => a.Room.Number).FirstOrDefault(), s.Status, s.PhotoPath)).ToListAsync();

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
        if (!vm.HasContact) ModelState.AddModelError("", "Enter at least one parent or guardian with a name and phone number.");
        if (!ModelState.IsValid) return View("Form", await Load(vm));

        var saved = new List<string>();
        AccountCredentials? creds = null;
        var ok = await Run(async () =>
        {
            var profile = await BuildProfileAsync(vm, saved);
            creds = await students.RegisterAsync(new RegisterStudentRequest(vm.FullName, vm.ClassYearId, vm.Residence,
                vm.Residence == ResidenceType.Boarding ? vm.HallId : null, profile));
        });
        if (!ok) { foreach (var p in saved) files.Delete(p); return View("Form", await Load(vm)); }
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
            Residence = s.Residence,
            RollNumber = s.RollNumber,
            AdmissionNumber = s.AdmissionNumber,
            Category = s.Category,
            Phone = s.Phone,
            ContactEmail = s.ContactEmail,
            FatherName = s.FatherName,
            FatherPhone = s.FatherPhone,
            FatherOccupation = s.FatherOccupation,
            MotherName = s.MotherName,
            MotherPhone = s.MotherPhone,
            MotherOccupation = s.MotherOccupation,
            GuardianName = s.GuardianName,
            GuardianPhone = s.GuardianPhone,
            HallId = await Db.HallRegistrations.Where(h => h.StudentId == id && h.AcademicYearId == yearId).Select(h => (int?)h.HallId).FirstOrDefaultAsync()
        };
        CopyPaths(vm, s);
        return View("Form", await Load(vm));
    }



    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StudentVm vm)
    {
        var s = await Db.Students.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == vm.Id);
        if (s == null) return NotFound();
        ModelState.Remove(nameof(vm.ClassYearId)); ModelState.Remove(nameof(vm.AcademicYearId));
        if (vm.Residence == ResidenceType.Boarding && vm.HallId == null) ModelState.AddModelError(nameof(vm.HallId), "Select a hall for a boarding student.");
        if (!vm.HasContact) ModelState.AddModelError("", "Enter at least one parent or guardian with a name and phone number.");
        var admission = vm.AdmissionNumber?.Trim();
        if (!string.IsNullOrEmpty(admission) && await Db.Students.AnyAsync(x => x.AdmissionNumber == admission && x.Id != vm.Id))
            ModelState.AddModelError(nameof(vm.AdmissionNumber), "This admission number is used by another student.");
        if (!ModelState.IsValid) { CopyPaths(vm, s); return View("Form", await Load(vm)); }

        var saved = new List<string>();
        var (oldPhoto, oldFather, oldMother) = (s.PhotoPath, s.FatherPhotoPath, s.MotherPhotoPath);
        StudentProfile profile;
        try { profile = await BuildProfileAsync(vm, saved); }
        catch (DomainException e) { foreach (var p in saved) files.Delete(p); ModelState.AddModelError("", e.Message); CopyPaths(vm, s); return View("Form", await Load(vm)); }

        s.User.FullName = vm.FullName.Trim();
        profile.ApplyTo(s);
        s.Residence = vm.Residence;

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
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { foreach (var p in saved) files.Delete(p); Fail("Could not save. The admission number may already be used."); return RedirectToAction(nameof(Edit), new { id = s.Id }); }

        if (profile.PhotoPath != null) files.Delete(oldPhoto);
        if (profile.FatherPhotoPath != null) files.Delete(oldFather);
        if (profile.MotherPhotoPath != null) files.Delete(oldMother);
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

    // ---------- helpers ----------
    private async Task<StudentProfile> BuildProfileAsync(StudentVm vm, List<string> saved)
    {
        async Task<string?> Save(IFormFile? f, string folder)
        {
            var path = await files.SavePhotoAsync(f, folder);
            if (path != null) saved.Add(path);
            return path;
        }
        return new StudentProfile
        {
            Gender = vm.Gender,
            DateOfBirth = vm.DateOfBirth!.Value,
            RollNumber = vm.RollNumber,
            AdmissionNumber = vm.AdmissionNumber,
            Category = vm.Category,
            Phone = vm.Phone,
            ContactEmail = vm.ContactEmail,
            FatherName = vm.FatherName,
            FatherPhone = vm.FatherPhone,
            FatherOccupation = vm.FatherOccupation,
            MotherName = vm.MotherName,
            MotherPhone = vm.MotherPhone,
            MotherOccupation = vm.MotherOccupation,
            GuardianName = vm.GuardianName,
            GuardianPhone = vm.GuardianPhone,
            PhotoPath = await Save(vm.Photo, "students"),
            FatherPhotoPath = await Save(vm.FatherPhoto, "parents"),
            MotherPhotoPath = await Save(vm.MotherPhoto, "parents")
        };
    }





    private static void CopyPaths(StudentVm vm, Models.Student s) { vm.PhotoPath = s.PhotoPath; vm.FatherPhotoPath = s.FatherPhotoPath; vm.MotherPhotoPath = s.MotherPhotoPath; }

    private async Task<List<ClassOptionRow>> ClassOptions(int? yearId) =>
        await Db.ClassYears.Where(c => c.AcademicYearId == yearId).OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
            .Select(c => new ClassOptionRow(c.Id, "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group, c.SchoolClass.CourseId, c.AcademicYearId)).ToListAsync();

    private async Task<StudentVm> Load(StudentVm vm)
    {
        var years = await Db.AcademicYears.OrderByDescending(y => y.Name).ToListAsync();
        var selectedYear = vm.AcademicYearId != 0 ? vm.AcademicYearId : years.FirstOrDefault(y => y.IsCurrent)?.Id ?? years.FirstOrDefault()?.Id;
        if (vm.AcademicYearId == 0 && selectedYear != null) vm.AcademicYearId = selectedYear.Value;
        ViewBag.Years = new SelectList(years, "Id", "Name", selectedYear);
        ViewData["ClassData"] = await Db.ClassYears.OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
            .Select(c => new ClassOptionRow(c.Id, "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group, c.SchoolClass.CourseId, c.AcademicYearId)).ToListAsync();
        ViewBag.Courses = new SelectList(await Db.Courses.OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
        ViewBag.Halls = new SelectList(await Db.Halls.OrderBy(h => h.Name).ToListAsync(), "Id", "Name", vm.HallId);
        return vm;
    }



}

public record EnrollmentRow(string Year, string Class, EnrollmentOutcome Outcome, string? Hall, string? Room);
public record ClassOptionRow(int Id, string Name, int CourseId, int YearId);
