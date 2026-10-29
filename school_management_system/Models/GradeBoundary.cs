namespace school_management_system.Models;
    public class GradeBoundary
    {
    public int Id { get; set; }
    public decimal MinScore { get; set; }
    public string Grade { get; set; } = "";
    public string Remark { get; set; } = "";
}

