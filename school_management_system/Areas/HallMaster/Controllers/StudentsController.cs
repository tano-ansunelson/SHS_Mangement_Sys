using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Service;

namespace school_management_system.Areas.HallMaster.Controllers;

public class StudentsController(AppDbContext db, RoomAllocationService rooms) : HallMasterBase(db)
{
    public async Task<IActionResult> Index()
    {
        if (HallId == null)
            return NoHall();

        ViewData["Title"] = "Students";
        ViewData["Rooms"] = (await rooms.GetRoomGridAsync(HallId.Value)).ToList();
        return View(await rooms.GetHallStudentsAsync(HallId.Value));
    }
}

