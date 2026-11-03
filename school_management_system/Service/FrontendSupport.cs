using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using school_management_system.Data;
using school_management_system.Models;
using Microsoft.EntityFrameworkCore;
using school_management_system.Models.Enums;
using System.Security.Claims;

namespace school_management_system.Service;
    public class AppClaimsFactory(UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm, IOptions<IdentityOptions> o)
    :UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(um,rm,o)
    {

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var id = await base.GenerateClaimsAsync(user);
        id.AddClaim(new Claim("FullName", user.FullName));
        return id;
    }
}




public class AccountAdminService(UserManager<ApplicationUser> users)
{
    /// <summary>Generates a new temporary password and forces a change at next login.</summary>
    public async Task<string> ResetPasswordAsync(ApplicationUser user)
    {
        var pw = CredentialService.GeneratePassword();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var r = await users.ResetPasswordAsync(user, token, pw);
        if (!r.Succeeded) throw new DomainException(string.Join("; ", r.Errors.Select(e => e.Description)));
        user.MustChangePassword = true;
        await users.UpdateAsync(user);
        return pw;
    }
}



public record SubjectRef(int Id, string Name);
/// <summary>One student's score for one subject in the selected exam. On a final, Total and Grade add the semester's midterm.</summary>
public record MatrixCell(decimal? Score, decimal? Total, string Grade, ResultStatus Status);

public class MatrixRow
{
    public int EnrollmentId { get; set; }
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public Dictionary<int, MatrixCell> Cells { get; set; } = new();
    public decimal? Average { get; set; }
}
    public class MatrixVm
{
    public int ClassYearId { get; set; }
    public int AssessmentId { get; set; }
    public bool IsFinal { get; set; }
    public string ClassName { get; set; } = "";
    public string AssessmentTitle { get; set; } = "";
    public List<SubjectRef> Subjects { get; set; } = new();
    public List<MatrixRow> Rows { get; set; } = new();
}
/// <summary>Students x subjects grid used by coordinator and admin.</summary>
public class MatrixService(AppDbContext db, GradingService grading)
{
    public async Task<MatrixVm> BuildAsync(int classYearId, int assessmentId)
    {
        var cy = await db.ClassYears.Include(c => c.SchoolClass).ThenInclude(s => s.Course)
                      .SingleOrDefaultAsync(c => c.Id == classYearId) ?? throw new DomainException("Class not found.");
        var asm = await db.Assessments.Include(a => a.Semester).SingleOrDefaultAsync(a => a.Id == assessmentId)
                  ?? throw new DomainException("Assessment not found.");
        var isFinal = asm.Type == AssessmentType.Final;
        var vm = new MatrixVm
        {
            ClassYearId = classYearId,
            AssessmentId = assessmentId,
            IsFinal = isFinal,
            ClassName = $"Form {cy.SchoolClass.FormLevel} {cy.SchoolClass.Course.Name} {cy.SchoolClass.Group}",
            AssessmentTitle = $"Semester {asm.Semester.Number} · {asm.Title}"
        };
        vm.Subjects = await db.ClassSubjects.Where(s => s.SchoolClassId == cy.SchoolClassId)
             .OrderBy(s => s.Subject.Name).Select(s => new SubjectRef(s.SubjectId, s.Subject.Name)).ToListAsync();
        var students = await db.Enrollments.Where(e => e.ClassYearId == classYearId)
            .OrderBy(e => e.Student.User.FullName)
            .Select(e => new { e.Id, Name = e.Student.User.FullName, Number = e.Student.StudentNumber }).ToListAsync();
        var results = await db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.AssessmentId == assessmentId)
            .Select(r => new { r.EnrollmentId, r.SubjectId, r.ClassScore, r.ExamScore, r.Status }).ToListAsync();

        // on a final, add the same semester's midterm so the total can be shown
        var midterms = isFinal
            ? await db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.Assessment.SemesterId == asm.SemesterId && r.Assessment.Type == AssessmentType.Midterm)
                .Select(r => new { r.EnrollmentId, r.SubjectId, r.ClassScore }).ToListAsync()
            : null;
        var boundaries = await grading.GetBoundariesAsync();
        foreach (var s in students)
        {
            var row = new MatrixRow { EnrollmentId = s.Id, Name = s.Name, Number = s.Number };
            foreach (var r in results.Where(x => x.EnrollmentId == s.Id))
            {
                var score = isFinal ? r.ExamScore : r.ClassScore;
                decimal? total = null; var grade = "";
                if (isFinal && score != null)
                {
                    var mid = midterms!.FirstOrDefault(m => m.EnrollmentId == s.Id && m.SubjectId == r.SubjectId)?.ClassScore;
                    if (mid != null) { total = mid + score; grade = GradingService.GradeFor(boundaries, total.Value); }
                }
                row.Cells[r.SubjectId] = new MatrixCell(score, total, grade, r.Status);
            }
            var numbers = row.Cells.Values.Select(c => c.Total ?? c.Score).Where(v => v != null).Select(v => v!.Value).ToList();
            row.Average = numbers.Count == 0 ? null : Math.Round(numbers.Average(), 1);
            vm.Rows.Add(row);
        }
        return vm;
    }


    /// <summary>Average semester total per subject across everything entered so far (midterm + final of each semester).</summary>
    public async Task<Dictionary<int, decimal?>> AveragesAsync(IEnumerable<int> enrollmentIds)
    {
        var ids = enrollmentIds.Distinct().ToList();
        var rows = await db.Results.Where(r => ids.Contains(r.EnrollmentId))
            .Select(r => new { r.EnrollmentId, r.SubjectId, r.Assessment.SemesterId, r.ClassScore, r.ExamScore }).ToListAsync();
        return ids.ToDictionary(id => id, id =>
        {
            var mine = rows.Where(r => r.EnrollmentId == id).ToList();
            var slots = mine.Select(r => (r.SubjectId, r.SemesterId)).Distinct().Count();
            return slots == 0 ? (decimal?)null : Math.Round(mine.Sum(r => (r.ClassScore ?? 0) + (r.ExamScore ?? 0)) / slots, 1);
        });
    }




}
