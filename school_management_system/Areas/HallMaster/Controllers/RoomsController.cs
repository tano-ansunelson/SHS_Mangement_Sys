using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Service;

namespace school_management_system.Areas.HallMaster.Controllers;
    public class RoomsController(AppDbContext db, RoomAllocationService rooms) : HallMasterBase(db)
    {
        public async Task<IActionResult> Index()
        {
        if (HallId == null)
            return NoHall();
        ViewData["Title"] = "Rooms";

            return View( await rooms.GetRoomGridAsync(HallId.Value));
        }



    public async Task<IActionResult> Details(int id)
    {
        if (HallId == null) return NoHall();
        var room = await Db.Rooms.SingleOrDefaultAsync(r => r.Id == id && r.HallId == HallId);
        if (room == null) return NotFound();
        ViewData["Title"] = "Room " + room.Number;
        ViewData["Room"] = room;
        ViewData["Occupants"] = await rooms.GetRoomStudentsAsync(id);
        ViewData["Unplaced"] = (await rooms.GetHallStudentsAsync(HallId.Value)).Where(s => s.RoomNumber == null).ToList();
        return View();
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Allocate(int studentId, int roomId, string? returnTo)
    {
        await Run(() => rooms.AllocateAsync(TeacherId, studentId, roomId), "Student placed in room.");
        return returnTo == "students" ? RedirectToAction("Index", "Students") : RedirectToAction(nameof(Details), new { id = roomId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int studentId, int roomId)
    {
        await Run(() => rooms.RemoveAsync(TeacherId, studentId), "Student removed from room.");
        return RedirectToAction(nameof(Details), new { id = roomId });
    }






}

