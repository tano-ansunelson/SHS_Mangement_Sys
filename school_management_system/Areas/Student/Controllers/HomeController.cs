using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;
using school_management_system.Service;
namespace school_management_system.Areas.Student.Controllers;

public record StudentDash(string Name, string Number, string Course, string? ClassName, string? Coordinator, List<HistoryRow> History, int YearId,
    List<StudentSubjectCard> Subjects, bool Boarding, string? Hall, string? Room, int PublishedResults);
public record ProfileVm(Models.Student Student, string? Hall, string? Room, string? HallMaster);


public class HomeController(AppDbContext db, DashboardQueries q) : StudentBase(db)
{
    public async Task<IActionResult> Index(int? yearId)
    {
        ViewData["Title"] = "Dashboard";
        var s = await Db.Students.Include(x => x.User).Include(x => x.Course).SingleAsync(x => x.Id == StudentId);
        var history = await q.StudentHistoryAsync(StudentId);
        var current = await CurrentYearIdAsync();
        yearId ??= history.Any(h => h.AcademicYearId == current) ? current : history.LastOrDefault()?.AcademicYearId;
        var enr = yearId == null ? null : await Db.Enrollments.Where(e => e.StudentId == StudentId && e.AcademicYearId == yearId)
            .Select(e => new
            {
                Name = "Form " + e.ClassYear.SchoolClass.FormLevel + " " + e.ClassYear.SchoolClass.Course.Name + " " + e.ClassYear.SchoolClass.Group,
                Coordinator = e.ClassYear.CoordinatorTeacher == null ? null : e.ClassYear.CoordinatorTeacher.User.FullName

            }).FirstOrDefaultAsync();
        var hall = yearId == null ? null : await Db.HallRegistrations.Where(h => h.StudentId == StudentId && h.AcademicYearId == yearId).Select(h => h.Hall.Name).FirstOrDefaultAsync();
        var room = yearId == null ? null : await Db.RoomAllocations.Where(a => a.StudentId == StudentId && a.AcademicYearId == yearId).Select(a => a.Room.Number).FirstOrDefaultAsync();
        var subjects = yearId == null ? new List<StudentSubjectCard>() : await q.StudentSubjectsAsync(StudentId, yearId.Value);
        var published = await Db.Results.CountAsync(r => r.Enrollment.StudentId == StudentId && r.Status == ResultStatus.Published);
        return View(new StudentDash(s.User.FullName, s.StudentNumber, s.Course.Name, enr?.Name, enr?.Coordinator, history, yearId ?? 0, subjects, s.Residence == ResidenceType.Boarding, hall, room, published));
    }


    public async Task<IActionResult> Profile()
    {
        ViewData["Title"] = "My profile";
        var s = await Db.Students.Include(x => x.User).Include(x => x.Course).SingleAsync(x => x.Id == StudentId);
        var y = await CurrentYearIdAsync();
        var hall = await Db.HallRegistrations.Where(h => h.StudentId == StudentId && h.AcademicYearId == y).Select(h => h.Hall.Name).FirstOrDefaultAsync();
        var room = await Db.RoomAllocations.Where(a => a.StudentId == StudentId && a.AcademicYearId == y).Select(a => a.Room.Number).FirstOrDefaultAsync();
        var master = await Db.HallMasterAssignments.Where(m => m.AcademicYearId == y && Db.HallRegistrations.Any(h => h.StudentId == StudentId && h.AcademicYearId == y && h.HallId == m.HallId))
            .Select(m => m.Teacher.User.FullName).FirstOrDefaultAsync();
        return View(new ProfileVm(s, hall, room, master));
    }
}

