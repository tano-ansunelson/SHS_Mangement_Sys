namespace school_management_system.Models;

    public class SchoolClass
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        public int FormLevel { get; set; }          // 1..3
        public string Group { get; set; } = "A";
    }

public class ClassSubject
{
    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}