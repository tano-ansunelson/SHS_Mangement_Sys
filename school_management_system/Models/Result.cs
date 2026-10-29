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
    public decimal ClassScore { get; set; }
    public decimal ExamScore { get; set; }
    public decimal Total { get; set; }
    public string Grade { get; set; } = "";
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