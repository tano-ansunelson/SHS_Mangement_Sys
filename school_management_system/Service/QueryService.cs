using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;

namespace school_management_system.Service;



public record ClassCard(int ClassYearId, string ClassName);
public record SubjectCard(int SubjectId, string Subject, List<ClassCard> Classes);
public record PersonRow(int EnrollmentId, int StudentId, string Name, string StudentNumber, ResidenceType Residence);
public record StudentSubjectCard(int SubjectId, string Subject, string? Teacher);
public record HistoryRow(int AcademicYearId, string Year, int FormLevel);
public class DashboardQueries(AppDbContext db)
{

    public static string ClassName(SchoolClass c) => $"Form {c.FormLevel} {c.Course.Name} {c.Group}";

    public async Task<List<SubjectCard>> TeacherSubjectsAsync(int teacherId)
    {
        var year = await db.CurrentYearAsync();
        var rows = await db.TeachingAssignments
            .Where(t => t.TeacherId == teacherId && t.ClassYear.AcademicYearId == year.Id)
            .Select(t => new { t.SubjectId, SubjectName = t.Subject.Name, t.ClassYearId, Class = t.ClassYear.SchoolClass })
            .ToListAsync();


        var courses = await db.Courses.ToDictionaryAsync(c => c.Id, c => c.Name);

        return rows.GroupBy(r => new { r.SubjectId, r.SubjectName })
            .Select(g => new SubjectCard(g.Key.SubjectId, g.Key.SubjectName,
            g.Select(x => new ClassCard(x.ClassYearId, $"Form {x.Class.FormLevel} {courses[x.Class.CourseId]} {x.Class.Group}")).ToList()))
            .ToList();

    }

    /// <summary>Classes the teacher coordinates this year (appears in the sidebar).</summary>
    public async Task<List<ClassCard>> CoordinatedClassesAsync(int teacherId)
    {
        var year = await db.CurrentYearAsync();
        var row = await db.ClassYears.Include(c => c.SchoolClass)
            .ThenInclude(s => s.Course)
            .Where(c => c.CoordinatorTeacherId == teacherId && c.AcademicYearId == year.Id).ToListAsync();

        return row.Select(c => new ClassCard(c.Id, ClassName(c.SchoolClass))).ToList();
    }


    public Task<List<PersonRow>> ClassStudentsAsync(int classYearId) =>
        db.Enrollments.Where(e => e.ClassYearId == classYearId && e.Outcome == EnrollmentOutcome.Pending)
        .OrderBy(e => e.Student.User.FullName)
        .Select(e => new PersonRow(
            e.Id,
            e.StudentId,
            e.Student.User.FullName,
            e.Student.StudentNumber,
            e.Student.Residence
            )).ToListAsync();


    public async Task<List<StudentSubjectCard>> StudentSubjectsAsync(int studentId, int academicYearId)
    {
        var enr = await db.Enrollments.Where(e => e.StudentId == studentId && e.AcademicYearId == academicYearId)
            .Select(e => new
            {
                e.ClassYearId,
                e.ClassYear.SchoolClassId
            }).SingleOrDefaultAsync();
        if (enr == null) return new();
        return await db.ClassSubjects
             .Where(s => s.SchoolClassId == enr.SchoolClassId)
             .Select(s => new StudentSubjectCard(s.SubjectId, s.Subject.Name,
             db.TeachingAssignments.Where(t => t.SubjectId == s.SubjectId && t.ClassYearId == enr.ClassYearId)
             .Select(t => t.Teacher.User.FullName).FirstOrDefault())).ToListAsync();



    }

    /// <summary>Years the student has been enrolled in (Form 1, Form 2, Form 3 history).</summary>
    public Task<List<HistoryRow>> StudentHistoryAsync(int studentId) =>
        db.Enrollments.Where(e => e.StudentId == studentId).OrderBy(e => e.AcademicYear.Name)
            .Select(e => new HistoryRow(e.AcademicYearId, e.AcademicYear.Name, e.ClassYear.SchoolClass.FormLevel))
            .ToListAsync();








}

