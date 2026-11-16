namespace school_management_system.Models;

public class SemesterPosition
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public int SemesterId { get; set; }
    public Semester Semester { get; set; } = null!;
    public decimal TotalScore { get; set; }
    public int Position { get; set; }
    public int OutOf { get; set; }
}

