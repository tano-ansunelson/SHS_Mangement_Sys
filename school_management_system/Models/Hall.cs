using school_management_system.Models.Enums;

namespace school_management_system.Models;
    public class Hall
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Gender? Gender { get; set; }
    }

public class HallMasterAssignment
{
    public int HallMasterAssignmentId { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}


public class Room
{
    public int Id { get; set; }
    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;
    public string Number { get; set; } = "";
    public int Capacity { get; set; }
}

/// <summary>Admin assigns a boarding student to a hall at registration.</summary>
public class HallRegistration
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}


public class RoomAllocation
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}
