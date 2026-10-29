namespace school_management_system.Models
{
    public class CourseSubject
    {
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
    }
}
