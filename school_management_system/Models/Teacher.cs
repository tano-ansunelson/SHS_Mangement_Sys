namespace school_management_system.Models;
    public class Teacher
    {
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public string StaffId { get; set; } = "";
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

}

public class TeachingAssignment
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int ClassYearId { get; set; }
    public ClassYear ClassYear { get; set; } = null!;
}