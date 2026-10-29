using school_management_system.Models.Enums;

namespace school_management_system.Models;
    public class Assessment
    {
        public int Id { get; set; }
        public int SemesterId { get; set; }
        public Semester Semester { get; set; } = null!;
        public AssessmentType Type { get; set; }
        public string Title { get; set; } = "";
        public bool IsOpen { get; set; } = true;
    }

