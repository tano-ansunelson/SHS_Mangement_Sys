using school_management_system.Models.Enums;

namespace school_management_system.Models;
    public class Result
    {

    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    /// <summary>Midterm assessments store the student's score here (the "class score").</summary>
    public decimal? ClassScore { get; set; }
    /// <summary>Final assessments store the student's score here (the "exam score").</summary>
    public decimal? ExamScore { get; set; }
    // The semester total (class + exam) and grade are worked out when needed, never stored.
    public ResultStatus Status { get; set; } = ResultStatus.Draft;
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public class ResultAuditLog
{
    public int Id { get; set; }
    public int ResultId { get; set; }
    public Result Result { get; set; } = null!;
    public ResultStatus FromStatus { get; set; }
    public ResultStatus ToStatus { get; set; }
    public string ActorUserId { get; set; } = "";
    public string? Note { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}