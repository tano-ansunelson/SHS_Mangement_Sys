using school_management_system.Models.Enums;

namespace school_management_system.Models;
    public class PromotionRecommendation
    {

    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public PromotionDecision Decision { get; set; }
    public int SubmittedByTeacherId { get; set; }
    public bool SubmittedToAdmin { get; set; }
    public AdminDecision AdminDecision { get; set; } = AdminDecision.Pending;
    public string? AdminNote { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class IdCounter
{
    public string Key { get; set; } = "";
    public int Value { get; set; }
}
