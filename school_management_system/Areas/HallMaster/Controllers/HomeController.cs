using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Service;

namespace school_management_system.Areas.HallMaster.Controllers;
    public class HomeController(AppDbContext db, RoomAllocationService rooms) : HallMasterBase(db)
    {
        public async Task<IActionResult> Index()
        {
            if (HallId == null) return NoHall();
            ViewData["Title"] = "Dashboard";
            var grid = await rooms.GetRoomGridAsync(HallId.Value);
            var students = await rooms.GetHallStudentsAsync(HallId.Value);
            ViewData["Students"] = students;



            return View(grid);
        }
    }


