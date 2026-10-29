using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;
using school_management_system.Service;
namespace school_management_system.Areas.Admin.Controllers;

public record HallRow(int Id, string Name, Gender? Gender, int Rooms, int Beds, int Occupied, int Registered, string? Master);
public record RoomRow(int Id, string Number, int Capacity, int Occupied);
public record HallStudent(int StudentId, string Name, string Number, string? Room);

public class HallsController(AppDbContext db, HallService halls, RoomAllocationService rooms) : AdminBase(db)
{

    public async Task<IActionResult> Index()
    {
        var y = await CurrentYearIdAsync();
        return View(await Db.Halls.OrderBy(h => h.Name).Select(h => new HallRow(h.Id, h.Name, h.Gender,
            Db.Rooms.Count(r => r.HallId == h.Id),
            Db.Rooms.Where(r => r.HallId == h.Id).Sum(r => (int?)r.Capacity) ?? 0,
            Db.RoomAllocations.Count(a => a.Room.HallId == h.Id && a.AcademicYearId == y),
            Db.HallRegistrations.Count(r => r.HallId == h.Id && r.AcademicYearId == y),
            Db.HallMasterAssignments.Where(m => m.HallId == h.Id && m.AcademicYearId == y).Select(m => m.Teacher.User.FullName).FirstOrDefault())).ToListAsync());
    }


    public IActionResult Create() => View("Form", new HallVm { RoomCount = 10 });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HallVm vm)
    {
        if (await Db.Halls.AnyAsync(h => h.Name == vm.Name)) ModelState.AddModelError(nameof(vm.Name), "A hall with this name exists.");
        if (!ModelState.IsValid) return View("Form", vm);
        var hall = await halls.CreateHallAsync(vm.Name, vm.Gender, vm.RoomCount, vm.Capacity);
        Flash("Hall created.");
        return RedirectToAction(nameof(Details), new { id = hall.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var h = await Db.Halls.FindAsync(id);
        return h == null ? NotFound() : View("Form", new HallVm { Id = h.Id, Name = h.Name, Gender = h.Gender });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(HallVm vm)
    {
        var h = await Db.Halls.FindAsync(vm.Id);
        if (h == null) return NotFound();
        ModelState.Remove(nameof(vm.RoomCount)); ModelState.Remove(nameof(vm.Capacity));
        if (await Db.Halls.AnyAsync(x => x.Name == vm.Name && x.Id != vm.Id)) ModelState.AddModelError(nameof(vm.Name), "A hall with this name exists.");
        if (!ModelState.IsValid) return View("Form", vm);
        h.Name = vm.Name.Trim(); h.Gender = vm.Gender;
        await Db.SaveChangesAsync();
        Flash("Hall updated.");
        return RedirectToAction(nameof(Details), new { id = h.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var h = await Db.Halls.FindAsync(id);
        if (h == null) return NotFound();
        var y = await CurrentYearIdAsync();
        ViewData["Title"] = h.Name;
        ViewData["Master"] = await Db.HallMasterAssignments.Where(m => m.HallId == id && m.AcademicYearId == y).Select(m => m.Teacher.User.FullName).FirstOrDefaultAsync();
        ViewData["Rooms"] = await Db.Rooms.Where(r => r.HallId == id).OrderBy(r => r.Number)
            .Select(r => new RoomRow(r.Id, r.Number, r.Capacity, Db.RoomAllocations.Count(a => a.RoomId == r.Id && a.AcademicYearId == y))).ToListAsync();
        ViewData["Students"] = await Db.HallRegistrations.Where(r => r.HallId == id && r.AcademicYearId == y).OrderBy(r => r.Student.User.FullName)
            .Select(r => new HallStudent(r.StudentId, r.Student.User.FullName, r.Student.StudentNumber,
                Db.RoomAllocations.Where(a => a.StudentId == r.StudentId && a.AcademicYearId == y).Select(a => a.Room.Number).FirstOrDefault())).ToListAsync();
        return View(h);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRoom(int hallId, string number, int capacity)
    {
        await Run(() => halls.AddRoomAsync(hallId, number.Trim(), capacity), "Room added.");
        return RedirectToAction(nameof(Details), new { id = hallId });
    }

    public async Task<IActionResult> EditRoom(int id)
    {
        var r = await Db.Rooms.FindAsync(id);
        return r == null ? NotFound() : View("RoomForm", new RoomVm { Id = r.Id, HallId = r.HallId, Number = r.Number, Capacity = r.Capacity });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoom(RoomVm vm)
    {
        var r = await Db.Rooms.FindAsync(vm.Id);
        if (r == null) return NotFound();
        if (await Db.Rooms.AnyAsync(x => x.HallId == r.HallId && x.Number == vm.Number && x.Id != r.Id)) ModelState.AddModelError(nameof(vm.Number), "This room number exists in the hall.");
        var y = await CurrentYearIdAsync();
        var occ = await Db.RoomAllocations.CountAsync(a => a.RoomId == r.Id && a.AcademicYearId == y);
        if (vm.Capacity < occ) ModelState.AddModelError(nameof(vm.Capacity), $"{occ} students are already in this room.");
        if (!ModelState.IsValid) return View("RoomForm", vm);
        r.Number = vm.Number.Trim(); r.Capacity = vm.Capacity;
        await Db.SaveChangesAsync();
        Flash("Room updated.");
        return RedirectToAction(nameof(Details), new { id = r.HallId });
    }

    public async Task<IActionResult> DeleteRoom(int id)
    {
        var r = await Db.Rooms.FindAsync(id);
        return r == null ? NotFound() : Confirm("Delete room", $"Delete room {r.Number}?", Url.Action(nameof(DeleteRoomConfirmed))!, id, Url.Action(nameof(Details), new { id = r.HallId })!);
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoomConfirmed(int id)
    {
        var r = await Db.Rooms.FindAsync(id);
        if (r == null) return NotFound();
        if (await Db.RoomAllocations.AnyAsync(a => a.RoomId == id)) Fail("This room has (or had) students and is kept for records.");
        else { Db.Rooms.Remove(r); await Db.SaveChangesAsync(); Flash("Room deleted."); }
        return RedirectToAction(nameof(Details), new { id = r.HallId });
    }

    public async Task<IActionResult> AssignMaster(int id)
    {
        var h = await Db.Halls.FindAsync(id);
        if (h == null) return NotFound();
        var y = await CurrentYearIdAsync();
        ViewData["Hall"] = h;
        var current = await Db.HallMasterAssignments.Where(m => m.HallId == id && m.AcademicYearId == y).Select(m => (int?)m.TeacherId).FirstOrDefaultAsync();
        ViewBag.Teachers = new SelectList(await Db.Teachers.Where(t => t.IsActive).OrderBy(t => t.User.FullName).Select(t => new { t.Id, t.User.FullName }).ToListAsync(), "Id", "FullName", current);
        return View();
    }



    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignMaster(int id, int teacherId)
    {
        var ok = await Run(() => halls.AssignHallMasterAsync(teacherId, id), "Hall master assigned. They will see the Hall Master portal the next time they sign in.");
        return ok ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(AssignMaster), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var h = await Db.Halls.FindAsync(id);
        return h == null ? NotFound() : Confirm("Delete hall", $"Delete {h.Name}? Only halls without rooms or students can be deleted.", Url.Action(nameof(DeleteConfirmed))!, id, Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await Db.Rooms.AnyAsync(r => r.HallId == id) || await Db.HallRegistrations.AnyAsync(r => r.HallId == id) || await Db.HallMasterAssignments.AnyAsync(r => r.HallId == id))
        { Fail("This hall has rooms, students or hall-master history."); return RedirectToAction(nameof(Index)); }
        Db.Halls.Remove((await Db.Halls.FindAsync(id))!);
        await Db.SaveChangesAsync();
        Flash("Hall deleted.");
        return RedirectToAction(nameof(Index));
    }


}


