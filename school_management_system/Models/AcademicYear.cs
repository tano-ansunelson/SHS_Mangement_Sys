namespace school_management_system.Models;
    public class AcademicYear
    {

    public int Id { get; set; }
    public string Name { get; set; } = "";      // 2025/2026
    public bool IsCurrent { get; set; }

    // Enrollments belonging to this student
    public ICollection<Enrollment> Enrollments { get; set; }
        = new List<Enrollment>();

}

public class Semester
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public int Number { get; set; }             // 1 or 2
}
