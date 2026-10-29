using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;

namespace school_management_system.Service;

public class AcademicSetupService(AppDbContext db)
{
    public async Task<AcademicYear> CreateAcademicYearAsync(string name, bool makeCurrent)
    {
        if (await db.AcademicYears.AnyAsync(y => y.Name == name))
            throw new DomainException($"Academic year {name} already exists.");

        await using var tx = await db.Database.BeginTransactionAsync();
        if (makeCurrent)
            await db.AcademicYears.Where(y => y.IsCurrent)
                .ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, false));

        var year = new AcademicYear { Name = name, IsCurrent = makeCurrent };
        db.AcademicYears.Add(year);
        db.Semesters.Add(new Semester { AcademicYear = year, Number = 1 });
        db.Semesters.Add(new Semester { AcademicYear = year, Number = 2 });
        foreach (var classId in await db.SchoolClasses.Select(c => c.Id).ToListAsync())
            db.ClassYears.Add(new ClassYear { SchoolClassId = classId, AcademicYear = year });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return year;
    }


    public async Task SetCurrentYearAsync(int yearId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.AcademicYears.Where(y => y.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, false));
        var n = await db.AcademicYears.Where(y => y.Id == yearId).ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, true));
        if (n == 0) throw new DomainException("Academic year not found.");
        await tx.CommitAsync();
    }
    public async Task SetCourseSubjectsAsync(int courseId, IEnumerable<int> subjectIds)
    {
        var ids = subjectIds.Distinct().ToList();
        var existing = await db.CourseSubjects.Where(x => x.CourseId == courseId).ToListAsync();
        db.CourseSubjects.RemoveRange(existing.Where(x => !ids.Contains(x.SubjectId)));
        foreach (var id in ids.Except(existing.Select(x => x.SubjectId)))
            db.CourseSubjects.Add(new CourseSubject { CourseId = courseId, SubjectId = id });
        await db.SaveChangesAsync();
    }

    /// <summary>Creates a permanent class (e.g. Form 1 General Science A). Subjects default to the course's subjects.</summary>
    public async Task<SchoolClass> CreateClassAsync(int courseId, int formLevel, string group, IEnumerable<int>? subjectIds = null)
    {
        if (formLevel is < 1 or > 3) throw new DomainException("Form level must be 1, 2 or 3.");
        group = group.Trim().ToUpperInvariant();
        if (await db.SchoolClasses.AnyAsync(c => c.CourseId == courseId && c.FormLevel == formLevel && c.Group == group))
            throw new DomainException("That cl  ass already exists.");

        var ids = (subjectIds ?? await db.CourseSubjects.Where(x => x.CourseId == courseId).Select(x => x.SubjectId).ToListAsync())
            .Distinct().ToList();

        await using var tx = await db.Database.BeginTransactionAsync();
        var cls = new SchoolClass { CourseId = courseId, FormLevel = formLevel, Group = group };
        db.SchoolClasses.Add(cls);
        foreach (var id in ids) db.ClassSubjects.Add(new ClassSubject { SchoolClass = cls, SubjectId = id });

        // make the class usable immediately in every existing academic year that is current or later
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.IsCurrent);
        if (year != null) db.ClassYears.Add(new ClassYear { SchoolClass = cls, AcademicYearId = year.Id });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return cls;
    }


    /// <summary>Replaces a class's subject list. Refuses to remove a subject that already has results.</summary>
    public async Task SetClassSubjectsAsync(int classId, IEnumerable<int> subjectIds)
    {
        var ids = subjectIds.Distinct().ToList();
        var existing = await db.ClassSubjects.Where(x => x.SchoolClassId == classId).ToListAsync();
        var removing = existing.Where(x => !ids.Contains(x.SubjectId)).ToList();
        foreach (var r in removing)
            if (await db.Results.AnyAsync(x => x.SubjectId == r.SubjectId && x.Enrollment.ClassYear.SchoolClassId == classId))
                throw new DomainException("A subject that already has results cannot be removed from this class.");
        db.ClassSubjects.RemoveRange(removing);
        foreach (var id in ids.Except(existing.Select(x => x.SubjectId)))
            db.ClassSubjects.Add(new ClassSubject { SchoolClassId = classId, SubjectId = id });
        await db.SaveChangesAsync();
    }

    public async Task<Assessment> CreateAssessmentAsync(int semesterId, AssessmentType type, string? title = null)
    {
        if (await db.Assessments.AnyAsync(a => a.SemesterId == semesterId && a.Type == type))
            throw new DomainException($"A {type} assessment already exists for this semester.");
        var a = new Assessment { SemesterId = semesterId, Type = type, Title = title ?? type.ToString() };
        db.Assessments.Add(a);
        await db.SaveChangesAsync();
        return a;
    }



      



}

