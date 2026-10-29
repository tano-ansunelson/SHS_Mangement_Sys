using school_management_system.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace school_management_system.Models.Domain;


public class LoginVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }

}

public class ChangePasswordVm
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string Current { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "New password")]
    public string New { get; set; } = "";
    [Compare(nameof(New), ErrorMessage = "Passwords do not match."), DataType(DataType.Password), Display(Name = "Confirm new password")]
    public string Confirm { get; set; } = "";
}

public class ConfirmDeleteVm
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public string Back { get; set; } = "/";
    public string Button { get; set; } = "Delete";
}
public record CountRow(string Name, int Count);

public class AdminDashVm
{
    public int Students, Boarding, Day, Teachers, Classes, Halls, Subjects, Courses, AwaitingApproval, PromotionsPending;
    public List<CountRow> ByCourse { get; set; } = new();
}


public record CourseRow(int Id, string Name, int Subjects, int Classes, int Students);
public class CourseVm
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    public List<int> SubjectIds { get; set; } = new();
    public List<Subject> AllSubjects { get; set; } = new();
}

public record SubjectRow(int Id, string Name, string Code, int Courses, int Classes);
public class SubjectVm
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [Required, StringLength(12)] public string Code { get; set; } = "";
}


public record ClassRow(int Id, string Course, int FormLevel, string Group, int Subjects, int Students);
public class ClassVm
{
    public int Id { get; set; }
    [Required, Display(Name = "Course")] public int CourseId { get; set; }
    [Range(1, 3), Display(Name = "Form (year group)")] public int FormLevel { get; set; } = 1;
    [Required, StringLength(3)] public string Group { get; set; } = "A";
    public List<int> SubjectIds { get; set; } = new();
    public List<Subject> AllSubjects { get; set; } = new();
    public List<Course> Courses { get; set; } = new();
    public Dictionary<int, List<int>> CourseSubjects { get; set; } = new();
}



public class TeacherVm
{
    public int Id { get; set; }
    [Required, StringLength(120), Display(Name = "Full name")] public string FullName { get; set; } = "";
    [Phone] public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    [Display(Name = "Class coordinator for (optional)")] public int? CoordinatorClassYearId { get; set; }
}

public class StudentVm
{
    public int Id { get; set; }
    [Required, StringLength(120), Display(Name = "Full name")] public string FullName { get; set; } = "";
    public Gender Gender { get; set; }
    [Required, Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }
    [Required, StringLength(120), Display(Name = "Guardian name")] public string GuardianName { get; set; } = "";
    [Required, Phone, Display(Name = "Guardian phone")] public string GuardianPhone { get; set; } = "";
    [Display(Name = "Class")] public int ClassYearId { get; set; }
    public ResidenceType Residence { get; set; }
    [Display(Name = "Hall")] public int? HallId { get; set; }
}

public class HallVm
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    public Gender? Gender { get; set; }
    [Range(0, 500), Display(Name = "Number of rooms")] public int RoomCount { get; set; }
    [Range(1, 50), Display(Name = "Beds per room")] public int Capacity { get; set; } = 4;
}

public class RoomVm
{
    public int Id { get; set; }
    public int HallId { get; set; }
    [Required, StringLength(20), Display(Name = "Room number")] public string Number { get; set; } = "";
    [Range(1, 50)] public int Capacity { get; set; } = 4;
}
public class YearVm
{
    [Required, RegularExpression(@"^\d{4}/\d{4}$", ErrorMessage = "Use the format 2025/2026.")] public string Name { get; set; } = "";
    [Display(Name = "Make this the current year")] public bool MakeCurrent { get; set; } = true;
}

public class AssessmentVm
{
    [Display(Name = "Semester")] public int SemesterId { get; set; }
    public AssessmentType Type { get; set; }
    [StringLength(60)] public string? Title { get; set; }
}


public class GradeVm
{
    public int Id { get; set; }
    [Range(0, 100), Display(Name = "Minimum score")] public decimal MinScore { get; set; }
    [Required, StringLength(5)] public string Grade { get; set; } = "";
    [Required, StringLength(40)] public string Remark { get; set; } = "";
}

public class EntryRow
{
    public int EnrollmentId { get; set; }
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public decimal? ClassScore { get; set; }
    public decimal? ExamScore { get; set; }
    public ResultStatus? Status { get; set; }
}


public class EntryVm
{
    public int ClassYearId { get; set; }
    public int SubjectId { get; set; }
    public int AssessmentId { get; set; }
    public string ClassName { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public string AssessmentTitle { get; set; } = "";
    public decimal MaxClass { get; set; }
    public decimal MaxExam { get; set; }
    public bool Locked { get; set; }
    public List<EntryRow> Rows { get; set; } = new();
}

public record SubjectTeacherRow(string Name, string Code, string? Teacher);
