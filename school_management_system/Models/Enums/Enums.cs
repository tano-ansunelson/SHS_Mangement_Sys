namespace school_management_system.Models.Enums;




public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string HallMaster = "HallMaster";
    public const string Student = "Student";
}

public enum Gender { Male, Female }
public enum ResidenceType { Day, Boarding }
public enum StudentStatus { Active, Graduated, Withdrawn }
public enum EnrollmentOutcome { Pending, Promoted, Repeated, Graduated, Withdrawn }
public enum AssessmentType { Midterm, Final }
public enum ResultStatus { Draft, Submitted, Returned, Reviewed, Approved, Published }
public enum PromotionDecision { Promote, Repeat }
public enum AdminDecision { Pending, Approved, Rejected }