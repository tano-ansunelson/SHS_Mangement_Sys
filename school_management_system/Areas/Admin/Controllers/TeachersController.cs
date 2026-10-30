using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Domain;
using school_management_system.Service;
//using static System.Net.WebRequestMethods;

namespace school_management_system.Areas.Admin.Controllers;


public record TeacherRow(int Id, string StaffId, string Name, string Email, string? Phone, bool IsActive, int Classes, bool IsHallMaster, string? PhotoPath);

public class TeachersController(AppDbContext db, TeacherService teachers, AccountAdminService accounts, UserManager<ApplicationUser> users, FileStorageService files) : AdminBase(db)
{
    public async Task<IActionResult> Index()
    {
        var yearId = await CurrentYearIdAsync();
        var rows = await Db.Teachers.OrderBy(t => t.User.FullName).Select(t => new TeacherRow(t.Id, t.StaffId, t.User.FullName, t.User.Email!, t.Phone, t.IsActive,
            Db.TeachingAssignments.Count(a => a.TeacherId == t.Id && a.ClassYear.AcademicYearId == yearId),
            Db.HallMasterAssignments.Any(h => h.TeacherId == t.Id && h.AcademicYearId == yearId), t.PhotoPath)).ToListAsync();
        return View(rows);
    }

    public async Task<IActionResult> Create() => View("Form", await Load(new TeacherVm()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeacherVm vm)
    {
        if (!ModelState.IsValid) return View("Form", await Load(vm));
        var saved = new List<string>();
        AccountCredentials? creds = null;
        var ok = await Run(async () =>
        {
            var profile = await BuildProfileAsync(vm, saved);
            creds = await teachers.CreateAsync(new CreateTeacherRequest(vm.FullName, vm.StaffId, profile, new(), vm.CoordinatorClassYearId));
        });
        if (!ok) { foreach (var p in saved) files.Delete(p); return View("Form", await Load(vm)); }
        ViewData["Back"] = Url.Action(nameof(Index));
        return View("Credentials", creds);
    }

    public async Task<IActionResult> Details(int id)
    {
        var t = await Db.Teachers.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        ViewData["Title"] = t.User.FullName;
        ViewData["Assignments"] = await Db.TeachingAssignments.Where(a => a.TeacherId == id).OrderByDescending(a => a.ClassYear.AcademicYear.Name)
            .Select(a => new AssignmentRow(a.Id, a.ClassYear.AcademicYear.Name, a.ClassYear.AcademicYear.IsCurrent, a.Subject.Name,
                "Form " + a.ClassYear.SchoolClass.FormLevel + " " + a.ClassYear.SchoolClass.Course.Name + " " + a.ClassYear.SchoolClass.Group)).ToListAsync();
        ViewData["Coordinates"] = await Db.ClassYears.Where(c => c.CoordinatorTeacherId == id).OrderByDescending(c => c.AcademicYear.Name)
            .Select(c => c.AcademicYear.Name + " · Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group).ToListAsync();
        ViewData["Halls"] = await Db.HallMasterAssignments.Where(h => h.TeacherId == id).OrderByDescending(h => h.AcademicYear.Name)
            .Select(h => h.AcademicYear.Name + " · " + h.Hall.Name).ToListAsync();
        return View(t);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var t = await Db.Teachers.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        return View("Form", await Load(ToVm(t)));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TeacherVm vm)
    {
        var t = await Db.Teachers.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == vm.Id);
        if (t == null) return NotFound();
        ModelState.Remove(nameof(vm.StaffId));
        if (!ModelState.IsValid) { vm.StaffId = t.StaffId; vm.PhotoPath = t.PhotoPath; return View("Form", await Load(vm)); }

        var saved = new List<string>(); var oldPhoto = t.PhotoPath;
        TeacherProfile profile;
        try { profile = await BuildProfileAsync(vm, saved); }
        catch (DomainException e) { foreach (var p in saved) files.Delete(p); ModelState.AddModelError("", e.Message); vm.StaffId = t.StaffId; vm.PhotoPath = t.PhotoPath; return View("Form", await Load(vm)); }

        t.User.FullName = vm.FullName.Trim(); t.IsActive = vm.IsActive; t.User.IsActive = vm.IsActive;
        profile.ApplyTo(t);
        await Db.SaveChangesAsync();
        if (profile.PhotoPath != null) files.Delete(oldPhoto);
        Flash("Teacher updated.");
        return RedirectToAction(nameof(Details), new { id = t.Id });
    }
    /// <summary>Choose a subject, then pick the classes (checkboxes) that teacher will take it in.</summary>
    public async Task<IActionResult> Assign(int id, int? subjectId)
    {
        var t = await Db.Teachers.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        var year = await CurrentYearIdAsync();
        if (year == null) { Fail("Create an academic year first."); return RedirectToAction(nameof(Details), new { id }); }
        ViewData["Teacher"] = t;
        ViewBag.Subjects = new SelectList(await Db.Subjects.OrderBy(s => s.Name).ToListAsync(), "Id", "Name", subjectId);
        ViewData["SubjectId"] = subjectId;
        if (subjectId != null)
            ViewData["Options"] = await Db.ClassYears.Where(c => c.AcademicYearId == year && Db.ClassSubjects.Any(s => s.SchoolClassId == c.SchoolClassId && s.SubjectId == subjectId))
                .OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
                .Select(c => new ClassOption(c.Id, "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group,
                    Db.TeachingAssignments.Where(a => a.ClassYearId == c.Id && a.SubjectId == subjectId).Select(a => a.Teacher.User.FullName).FirstOrDefault())).ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, int subjectId, List<int> classYearIds)
    {
        if (classYearIds.Count == 0) { Fail("Select at least one class."); return RedirectToAction(nameof(Assign), new { id, subjectId }); }
        var ok = await Run(() => teachers.AddAssignmentsAsync(id, classYearIds.Select(c => new TeachingSlot(subjectId, c)).ToList()), "Teaching assignments saved.");
        return ok ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(Assign), new { id, subjectId });
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(int id, int teacherId)
    {
        await Run(() => teachers.RemoveAssignmentAsync(id), "Assignment removed.");
        return RedirectToAction(nameof(Details), new { id = teacherId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var t = await Db.Teachers.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        string? pw = null;
        if (!await Run(async () => pw = await accounts.ResetPasswordAsync(t.User))) return RedirectToAction(nameof(Details), new { id });
        ViewData["Back"] = Url.Action(nameof(Details), new { id });
        return View("Credentials", new AccountCredentials(t.User.FullName, t.User.Email!, pw!, t.StaffId));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var t = await Db.Teachers.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        return t == null ? NotFound() : Confirm("Delete teacher", $"Delete {t.User.FullName}? Teachers with teaching history cannot be deleted; deactivate them instead.", Url.Action(nameof(DeleteConfirmed))!, id, Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var t = await Db.Teachers.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        if (await Db.TeachingAssignments.AnyAsync(a => a.TeacherId == id) || await Db.HallMasterAssignments.AnyAsync(a => a.TeacherId == id) || await Db.ClassYears.AnyAsync(c => c.CoordinatorTeacherId == id))
        { Fail("This teacher has teaching history. Deactivate the account instead (Edit)."); return RedirectToAction(nameof(Details), new { id }); }
        var user = t.User; var photo = t.PhotoPath;
        Db.Teachers.Remove(t);
        await Db.SaveChangesAsync();
        await users.DeleteAsync(user);
        files.Delete(photo);
        Flash("Teacher deleted.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- helpers ----------
    private async Task<TeacherProfile> BuildProfileAsync(TeacherVm vm, List<string> saved)
    {
        var photo = await files.SavePhotoAsync(vm.Photo, "teachers");
        if (photo != null) saved.Add(photo);
        return new TeacherProfile
        {
            Phone = vm.Phone,
            Gender = vm.Gender,
            DateOfBirth = vm.DateOfBirth,
            FatherName = vm.FatherName,
            MotherName = vm.MotherName,
            MaritalStatus = vm.MaritalStatus,
            ContractType = vm.ContractType,
            Shift = vm.Shift,
            WorkLocation = vm.WorkLocation,
            JoinDate = vm.JoinDate,
            ContactEmail = vm.ContactEmail,
            ExperienceYears = vm.ExperienceYears,
            Qualification = vm.Qualification,
            PhotoPath = photo
        };
    }



    private static TeacherVm ToVm(Models.Teacher t) => new()
    {
        Id = t.Id,
        StaffId = t.StaffId,
        FullName = t.User.FullName,
        Gender = t.Gender,
        DateOfBirth = t.DateOfBirth,
        FatherName = t.FatherName,
        MotherName = t.MotherName,
        MaritalStatus = t.MaritalStatus,
        ContractType = t.ContractType,
        Shift = t.Shift,
        WorkLocation = t.WorkLocation,
        JoinDate = t.JoinDate,
        Phone = t.Phone,
        ContactEmail = t.ContactEmail,
        ExperienceYears = t.ExperienceYears,
        Qualification = t.Qualification,
        IsActive = t.IsActive,
        PhotoPath = t.PhotoPath
    };

    private async Task<TeacherVm> Load(TeacherVm vm)
    {
        var year = await CurrentYearIdAsync();
        ViewBag.Classes = new SelectList(await Db.ClassYears.Where(c => c.AcademicYearId == year && c.CoordinatorTeacherId == null)
            .OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
            .Select(c => new { c.Id, Name = "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group }).ToListAsync(), "Id", "Name", vm.CoordinatorClassYearId);
        return vm;
    }






}

public record AssignmentRow(int Id, string Year, bool Current, string Subject, string Class);
public record ClassOption(int ClassYearId, string Name, string? TakenBy);
