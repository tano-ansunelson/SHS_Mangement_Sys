using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using school_management_system.Controllers;
using school_management_system.Data;
using school_management_system.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace school_management_system.Areas.HallMaster.Controllers;


[Area("HallMaster"), Authorize(Roles = Roles.HallMaster)]
public abstract class HallMasterBase(AppDbContext db) : PortalController(db)
{
    protected int TeacherId { get; private set; }
    protected int? HallId { get; private set; }
    protected string HallName { get; private set; } = "";

    protected override async Task OnPortalAsync()
    {
        TeacherId = await Db.Teachers.Where(t => t.UserId == UserId).Select(t => t.Id).FirstOrDefaultAsync();
        var y = await CurrentYearIdAsync();
        var h = await Db.HallMasterAssignments.Where(a => a.TeacherId == TeacherId && a.AcademicYearId == y)
            .Select(a => new { a.HallId, a.Hall.Name }).FirstOrDefaultAsync();
        HallId = h?.HallId; HallName = h?.Name ?? "";
        ViewData["HallName"] = HallName;
    }



    protected IActionResult NoHall() { ViewData["Title"] = "No hall assigned"; return View("~/Areas/HallMaster/Views/Shared/NoHall.cshtml"); }

}
