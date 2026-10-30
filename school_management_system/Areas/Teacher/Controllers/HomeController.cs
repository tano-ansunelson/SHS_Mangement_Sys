using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Models.Enums;
using school_management_system.Service;
using Microsoft.EntityFrameworkCore;

namespace school_management_system.Areas.Teacher.Controllers;


public record OpenAssessment(int Id, string Name, bool IsOpen);
public record TeacherDash(List<SubjectCard> Subjects, List<ClassCard> Coordinated, int Classes, int Students, List<OpenAssessment> Assessments, string Name);

public class HomeController(AppDbContext db, DashboardQueries q) : TeacherBase(db,q)
    {
        public async Task<IActionResult> Index()
        {
        ViewData["Title"] = "Dashboard";
        var subjects = new List<SubjectCard>();

        var coord = new List<ClassCard>();

        try
        {
            subjects = await q.TeacherSubjectsAsync(TeacherId);
            coord = await q.CoordinatedClassesAsync(TeacherId);
        }
        catch (DomainException) { }

        var y = await CurrentYearIdAsync();

        var classIds = subjects.SelectMany(s => s.Classes)
            .Select(c => c.ClassYearId)
            .Distinct()
            .ToList();

        var students = await Db.Enrollments.Where(e => classIds.Contains(e.ClassYearId)
        && e.Outcome == EnrollmentOutcome.Pending).Select(e => e.StudentId).Distinct().CountAsync();

        var asms = await Db.Assessments
            .Where(a => a.Semester.AcademicYearId == y)
            .OrderBy(a => a.Semester.Number)
            .ThenBy(a => a.Type)
           .Select(a => 
           new OpenAssessment
           (
               a.Id, "Semester " + a.Semester.Number + " · " + a.Title, a.IsOpen
               ))
           .ToListAsync();
        var name = await Db.Teachers
            .Where(t => t.Id == TeacherId)
            .Select(t => t.User.FullName)
            .FirstOrDefaultAsync() ?? "";
        return View(new TeacherDash(subjects, coord, classIds.Count, students, asms, name));
    }
    }

