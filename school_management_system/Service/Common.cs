using school_management_system.Data;
using school_management_system.Models;
using Microsoft.EntityFrameworkCore;
namespace school_management_system.Service;


public class DomainException(string message) : Exception(message);

public class GradingOptions
{
    public decimal MaxClassScore { get; set; } = 30;
    public decimal MaxExamScore { get; set; } = 70;

}

public class SchoolOptions
{
    public string StaffEmailDomain { get; set; } = "staff.school.edu.gh";
    public string StudentEmailDomain { get; set; } = "students.school.edu.gh";
}

public static class DbExtensions
{
    public static async Task<AcademicYear> CurrentYearAsync(this AppDbContext db) =>
        await db.AcademicYears.SingleOrDefaultAsync(y => y.IsCurrent)
        ?? throw new DomainException("No current academic year is set. Create one first.");
}


public record AccountCredentials(string FullName, string Email, string Password, string Number);





public class Common
    {
    }

