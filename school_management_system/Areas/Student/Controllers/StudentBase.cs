using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using school_management_system.Controllers;
using school_management_system.Data;
using school_management_system.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace school_management_system.Areas.Student.Controllers;

[Area("Student"), Authorize(Roles = Roles.Student)]
public abstract class StudentBase(AppDbContext db) : PortalController(db)
{

    protected int StudentId { get; private set; }

    protected override async Task OnPortalAsync() =>
        StudentId = await Db.Students.Where(s => s.UserId == UserId).Select(s => s.Id).FirstOrDefaultAsync();
}











