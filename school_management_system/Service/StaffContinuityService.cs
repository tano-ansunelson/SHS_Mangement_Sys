using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using school_management_system.Data;
using school_management_system.Models;

namespace school_management_system.Service;

public class StaffContinuityService(AppDbContext db)
{


    public async Task<int?> PreviousYearIdAsync(int yearId)
    {
        var name = await db.AcademicYears
            .Where(y => y.Id == yearId)
            .Select(y => y.Name)
            .SingleAsync();
        return await db.AcademicYears.Where(y => string.Compare(y.Name, name) < 0)
            .OrderByDescending(y => y.Name)
            .Select(y => (int?)y.Id)
            .FirstOrDefaultAsync();
    }


    public async Task<int> CarryForwardAsync(int fromYearId, int toYearId)
    {
        var added = 0;
        var active = (await db.Teachers.Where(t => t.IsActive).Select(t => t.Id).ToListAsync()).ToHashSet();

        var targets = await db.ClassYears.Where(c => c.AcademicYearId == toYearId).ToListAsync();
        var sources = await db.ClassYears.Where(c => c.AcademicYearId == fromYearId).ToListAsync();

        // class coordinators
        foreach (var t in targets.Where(t => t.CoordinatorTeacherId == null))
        {
            var src = sources.FirstOrDefault(s => s.SchoolClassId == t.SchoolClassId);
            if (src?.CoordinatorTeacherId is int tid && active.Contains(tid)) { t.CoordinatorTeacherId = tid; added++; }
        }
        // subject teachers: same subject in the same class (Form 1 Science A keeps its maths teacher)
        var fromAssignments = await db.TeachingAssignments.Where(a => a.ClassYear.AcademicYearId == fromYearId)
            .Select(a => new { a.TeacherId, a.SubjectId, a.ClassYear.SchoolClassId }).ToListAsync();
        var already = (await db.TeachingAssignments.Where(a => a.ClassYear.AcademicYearId == toYearId)
            .Select(a => new { a.ClassYearId, a.SubjectId }).ToListAsync()).Select(a => (a.ClassYearId, a.SubjectId)).ToHashSet();
        var classSubjects = (await db.ClassSubjects.Select(s => new { s.SchoolClassId, s.SubjectId }).ToListAsync())
            .Select(s => (s.SchoolClassId, s.SubjectId)).ToHashSet();


        foreach (var a in fromAssignments)
        {
            var target = targets.FirstOrDefault(t => t.SchoolClassId == a.SchoolClassId);
            if (target == null || !active.Contains(a.TeacherId) || !classSubjects.Contains((a.SchoolClassId, a.SubjectId))) continue;
            if (!already.Add((target.Id, a.SubjectId))) continue;
            db.TeachingAssignments.Add(new TeachingAssignment { TeacherId = a.TeacherId, SubjectId = a.SubjectId, ClassYearId = target.Id });
            added++;
        }

        // hall masters
        var haveHall = (await db.HallMasterAssignments.Where(h => h.AcademicYearId == toYearId).Select(h => h.HallId).ToListAsync()).ToHashSet();
        foreach (var h in await db.HallMasterAssignments.Where(h => h.AcademicYearId == fromYearId).ToListAsync())
        {
            if (haveHall.Contains(h.HallId) || !active.Contains(h.TeacherId)) continue;
            db.HallMasterAssignments.Add(new HallMasterAssignment { TeacherId = h.TeacherId, HallId = h.HallId, AcademicYearId = toYearId });
            added++;
        }
        return added;
    }


    // ---- an admin's change in one year is mirrored into later years that still match the old state ----

    public async Task OnTeachingAddedAsync(int classYearId, int subjectId, int teacherId)
    {
        foreach (var later in await LaterClassYearsAsync(classYearId))
            if (!await db.TeachingAssignments.AnyAsync(a => a.ClassYearId == later.Id && a.SubjectId == subjectId))
                db.TeachingAssignments.Add(new TeachingAssignment { TeacherId = teacherId, SubjectId = subjectId, ClassYearId = later.Id });
    }








    public async Task OnTeachingRemovedAsync(int classYearId, int subjectId, int teacherId)
    {
        foreach (var later in await LaterClassYearsAsync(classYearId))
        {
            if (await db.Results.AnyAsync(r => r.SubjectId == subjectId && r.Enrollment.ClassYearId == later.Id)) continue;   // history stays
            var copy = await db.TeachingAssignments.SingleOrDefaultAsync(a => a.ClassYearId == later.Id && a.SubjectId == subjectId && a.TeacherId == teacherId);
            if (copy != null) db.TeachingAssignments.Remove(copy);
        }
    }

    public async Task OnCoordinatorChangedAsync(int classYearId, int? oldTeacherId, int? newTeacherId)
    {
        if (oldTeacherId == newTeacherId) return;
        foreach (var later in await LaterClassYearsAsync(classYearId))
            if (later.CoordinatorTeacherId == oldTeacherId) later.CoordinatorTeacherId = newTeacherId;
    }







    public async Task OnHallMasterChangedAsync(int hallId, int yearId, int? oldTeacherId, int newTeacherId)
    {
        if (oldTeacherId == newTeacherId) return;
        var yearName = await db.AcademicYears.Where(y => y.Id == yearId).Select(y => y.Name).SingleAsync();
        var laterYears = await db.AcademicYears.Where(y => string.Compare(y.Name, yearName) > 0).Select(y => y.Id).ToListAsync();
        foreach (var laterId in laterYears)
        {
            var existing = await db.HallMasterAssignments.SingleOrDefaultAsync(h => h.HallId == hallId && h.AcademicYearId == laterId);
            if (existing == null && oldTeacherId == null)
                db.HallMasterAssignments.Add(new HallMasterAssignment { TeacherId = newTeacherId, HallId = hallId, AcademicYearId = laterId });
            else if (existing != null && existing.TeacherId == oldTeacherId)
                existing.TeacherId = newTeacherId;
        }
    }



    private async Task<List<ClassYear>> LaterClassYearsAsync(int classYearId)
    {
        var src = await db.ClassYears.Where(c => c.Id == classYearId)
            .Select(c => new { c.SchoolClassId, Year = c.AcademicYear.Name }).SingleAsync();
        return await db.ClassYears.Where(c => c.SchoolClassId == src.SchoolClassId && string.Compare(c.AcademicYear.Name, src.Year) > 0).ToListAsync();
    }

}

