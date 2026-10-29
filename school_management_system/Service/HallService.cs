using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;
using school_management_system.Service;
using System.Data;

namespace school_management_system.Service;


public record RoomStatus(int RoomId, string Number, int Capacity, int Occupied)
{
    public bool IsFull => Occupied >= Capacity;
    public string Color => IsFull ? "red" : "green";
}

public record HallStudentRow(int StudentId, string Name, string StudentNumber, string? RoomNumber);

public class HallService(AppDbContext db, UserManager<ApplicationUser> users)
{

    public async Task<Hall> CreateHallAsync(string name, Gender? gender, int roomCount, int defaultCapacity)
    {
        if (roomCount < 0 || defaultCapacity < 1)
            throw new DomainException("Invalid room count or Capacity.");
        var hall = new Hall
        {
            Name = name.Trim(),
            Gender = gender
        };
        db.Halls.Add(hall);
        for (var i = 1; i <= roomCount; i++)
            db.Rooms.Add(new
                Room
            {
                Hall = hall,
                Number = i.ToString(),
                Capacity = defaultCapacity
            });

        await db.SaveChangesAsync();
        return hall;
    }


    public async Task<Room> AddRoomAsync(int hallId, string number, int capacity)
    {
        if (capacity < 1)
            throw new DomainException("Capacity must be at least 1.");

        if (await db.Rooms.AnyAsync(r => r.HallId == hallId && r.Number == number))
            throw new DomainException("That room number is already exist in this hall.");

        var room = new Room
        {
            HallId = hallId,
            Number = number,
            Capacity = capacity
        };


        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room;
    }


    public async Task AssignHallMasterAsync(int teacherId, int hallId)
    {
        var year = await db.CurrentYearAsync();
        var teacher = await db.Teachers
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Id == teacherId) ??
            throw new DomainException("Teacher not found.");


        await using var tx = await db.Database.BeginTransactionAsync();
        var current = await db.HallMasterAssignments.SingleOrDefaultAsync(a => a.HallId == hallId && a.AcademicYearId == year.Id);

        if (current != null) db.HallMasterAssignments.Remove(current);
        db.HallMasterAssignments.Add(
            new HallMasterAssignment
            {
                TeacherId = teacherId,
                HallId = hallId,
                AcademicYearId = year.Id
            });
        await db.SaveChangesAsync();

        if (!await users.IsInRoleAsync(teacher.User, Roles.HallMaster))
            await users.AddToRoleAsync(teacher.User, Roles.HallMaster);
        await tx.CommitAsync();


        if (current != null && current.TeacherId != teacherId)
        {
            var stillMaster = await db.HallMasterAssignments.AnyAsync(a => a.TeacherId == current.TeacherId && a.AcademicYearId == year.Id);
            if (!stillMaster)
            {
                var old = await db.Teachers
                    .Include(t => t.User)
                    .SingleAsync(t => t.Id == current.TeacherId);
                await users.RemoveFromRoleAsync(old.User, Roles.HallMaster);
            }
        }



    }
    public async Task<int?> HallIdForMasterAsync(int teacherId)
    {
        var year = await db.CurrentYearAsync();
        return await db.HallMasterAssignments
            .Where(a => a.TeacherId == teacherId && a.AcademicYearId == year.Id)
            .Select(a => (int?)a.HallId).FirstOrDefaultAsync();
    }


}




public class RoomAllocationService(AppDbContext db)
{
    public async Task<List<RoomStatus>> GetRoomGridAsync(int hallId)
    {
        var year = await db.CurrentYearAsync();
        return await db.Rooms.Where(r => r.HallId == hallId).OrderBy(r => r.Number)
            .Select(r => new RoomStatus(r.Id, r.Number, r.Capacity,

            db.RoomAllocations.Count(a => a.RoomId == r.Id && a.AcademicYearId == year.Id)))
            .ToListAsync();


    }

    /// <summary>Students registered to this hall by admin, with their room if one is set.</summary>
    public async Task<List<HallStudentRow>> GetHallStudentsAsync(int hallId)
    {
        var year = await db.CurrentYearAsync();
        return await db.HallRegistrations
            .Where(
            h => h.HallId == hallId && h.AcademicYearId == year.Id)
            .Select(h =>
            new HallStudentRow(
                h.StudentId,
                h.Student.User.FullName,
                h.Student.StudentNumber,
                db.RoomAllocations.Where(a => a.StudentId == h.StudentId && a.AcademicYearId == year.Id)
                    .Select(a => a.Room.Number).FirstOrDefault()))
            .OrderBy(s => s.Name).ToListAsync();
    }


    public async Task<List<HallStudentRow>> GetRoomStudentsAsync(int roomId)
    {
        var year = await db.CurrentYearAsync();
        return await db.RoomAllocations.Where(a => a.RoomId == roomId && a.AcademicYearId == year.Id)
            .Select(a => new HallStudentRow(a.StudentId, a.Student.User.FullName, a.Student.StudentNumber, a.Room.Number))
            .OrderBy(s => s.Name).ToListAsync();
    }
    public async Task AllocateAsync(int hallMasterTeacherId, int studentId, int roomId)
    {
        var year = await db.CurrentYearAsync();
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId) ?? throw new DomainException("Room not found.");

        if (!await db.HallMasterAssignments.AnyAsync(a => a.TeacherId == hallMasterTeacherId && a.HallId == room.HallId && a.AcademicYearId == year.Id))
            throw new DomainException("You are not the hall master of this hall.");
        if (!await db.HallRegistrations.AnyAsync(h => h.StudentId == studentId && h.HallId == room.HallId && h.AcademicYearId == year.Id))
            throw new DomainException("This student is not registered in this hall.");

        // Serializable so two hall masters/clicks cannot overfill a room.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existing = await db.RoomAllocations.SingleOrDefaultAsync(a => a.StudentId == studentId && a.AcademicYearId == year.Id);
        if (existing?.RoomId == roomId) { await tx.CommitAsync(); return; }

        var occupied = await db.RoomAllocations.CountAsync(a => a.RoomId == roomId && a.AcademicYearId == year.Id);
        if (occupied >= room.Capacity) throw new DomainException($"Room {room.Number} is full.");

        if (existing != null) db.RoomAllocations.Remove(existing);
        db.RoomAllocations.Add(new RoomAllocation { StudentId = studentId, RoomId = roomId, AcademicYearId = year.Id });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }


    public async Task RemoveAsync(int hallMasterTeacherId, int studentId)
    {
        var year = await db.CurrentYearAsync();
        var a = await db.RoomAllocations.Include(x => x.Room)
                    .SingleOrDefaultAsync(x => x.StudentId == studentId && x.AcademicYearId == year.Id)
                ?? throw new DomainException("Student has no room.");
        if (!await db.HallMasterAssignments.AnyAsync(m => m.TeacherId == hallMasterTeacherId && m.HallId == a.Room.HallId && m.AcademicYearId == year.Id))
            throw new DomainException("You are not the hall master of this hall.");
        db.RoomAllocations.Remove(a);
        await db.SaveChangesAsync();
    }


}
