using school_management_system.Models.Enums;

namespace school_management_system.Models;
    public class ClassYear
    {

    public int Id { get; set; }
    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public int? CoordinatorTeacherId { get; set; }   // one coordinator per class per year
    public Teacher? CoordinatorTeacher { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; }
      = new List<Enrollment>();
}


public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int ClassYearId { get; set; }
    public ClassYear ClassYear { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public EnrollmentOutcome Outcome { get; set; } = EnrollmentOutcome.Pending;
}


