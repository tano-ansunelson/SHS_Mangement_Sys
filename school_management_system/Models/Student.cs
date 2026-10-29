using school_management_system.Models.Enums;
using System.Reflection;

namespace school_management_system.Models
{
    public class Student
    {

        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;
        public string StudentNumber { get; set; } = "";
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        public Gender Gender { get; set; }
        public ResidenceType Residence { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public string GuardianName { get; set; } = "";
        public string GuardianPhone { get; set; } = "";
        public StudentStatus Status { get; set; } = StudentStatus.Active;

        // Enrollments belonging to this student
        public ICollection<Enrollment> Enrollments { get; set; }
            = new List<Enrollment>();

    }
}
