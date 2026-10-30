using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Service;

namespace school_management_system.Areas.Teacher.Controllers;



public class ClassesController(AppDbContext db, DashboardQueries q) : TeacherBase(db, q)
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "My classes";
        try { return View(await q.TeacherSubjectsAsync(TeacherId)); }
        catch (DomainException e) { Fail(e.Message); return View(new List<SubjectCard>()); }
    }

    public async Task<IActionResult> Students(int classYearId, int subjectId)
    {
        if (!await TeacherAsync(classYearId, subjectId)) return Forbid();
        var subject = await Db.Subjects.FindAsync(subjectId);
        var cy = await Db.ClassYears.FindAsync(classYearId);
        var cls = await Db.SchoolClasses.FindAsync(cy!.SchoolClassId);
        var course = await Db.Courses.FindAsync(cls!.CourseId);
        ViewData["Title"] = subject!.Name;
        ViewData["Subject"] = subject.Name; ViewData["SubjectId"] = subjectId; ViewData["ClassYearId"] = classYearId;
        ViewData["ClassName"] = $"Form {cls.FormLevel} {course!.Name} {cls.Group}";
        return View(await q.ClassStudentsAsync(classYearId));
    }
}