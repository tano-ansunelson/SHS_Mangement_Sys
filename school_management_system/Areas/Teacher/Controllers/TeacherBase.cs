using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using school_management_system.Controllers;
using school_management_system.Data;
using school_management_system.Models.Enums;
using school_management_system.Service;

namespace school_management_system.Areas.Teacher.Controllers;

[Area("Teacher"), Authorize(Roles = Roles.Teacher)]
public abstract class TeacherBase(AppDbContext db, DashboardQueries queries): PortalController(db)
    {


    protected int TeacherId { get; private set; }

    protected override async Task OnPortalAsync()
    {
        TeacherId = await Db.Teachers
            .Where(t => t.UserId == UserId)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();
    }

    protected Task<bool> IsCoordinatorAsync(int clasYearId) =>
        Db.ClassYears.AnyAsync(c => c.Id == clasYearId && c.CoordinatorTeacherId == TeacherId);

    protected Task<bool> TeacherAsync(int classYearId, int subjectId) =>
        Db.TeachingAssignments.AnyAsync(t => t.TeacherId == TeacherId && t.ClassYearId == classYearId && t.SubjectId == subjectId);




    }

