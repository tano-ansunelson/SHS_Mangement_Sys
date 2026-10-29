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
public record MatrixCell(decimal Total, string Grade, ResultStatus Status);

public class MatrixRow
{
    public int EnrollmentId { get; set; }
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public Dictionary<int, MatrixCell> Cells { get; set; } = new();
    public decimal? Average => Cells.Count == 0 ? null : Math.Round(Cells.Values.Average(c => c.Total), 1);
}

public class MatrixVm
{
    public int ClassYearId { get; set; }
    public int AssessmentId { get; set; }
    public string ClassName { get; set; } = "";
    public string AssessmentTitle { get; set; } = "";
    public List<SubjectRef> Subjects { get; set; } = new();
    public List<MatrixRow> Rows { get; set; } = new();
}
/// <summary>Students x subjects grid used by coordinator and admin.</summary>
public class MatrixService(AppDbContext db)
{
    public async Task<MatrixVm> BuildAsync(int classYearId, int assessmentId)
    {
        var cy = await db.ClassYears.Include(c => c.SchoolClass).ThenInclude(s => s.Course)
                     .SingleOrDefaultAsync(c => c.Id == classYearId) ?? throw new DomainException("Class not found.");
        var asm = await db.Assessments.Include(a => a.Semester).SingleOrDefaultAsync(a => a.Id == assessmentId)
                  ?? throw new DomainException("Assessment not found.");
        var vm = new MatrixVm
        {
            ClassYearId = classYearId,
            AssessmentId = assessmentId,
            ClassName = $"Form {cy.SchoolClass.FormLevel} {cy.SchoolClass.Course.Name} {cy.SchoolClass.Group}",
            AssessmentTitle = $"Semester {asm.Semester.Number} · {asm.Title}"
        };
        vm.Subjects = await db.ClassSubjects.Where(s => s.SchoolClassId == cy.SchoolClassId)
            .OrderBy(s => s.Subject.Name).Select(s => new SubjectRef(s.SubjectId, s.Subject.Name)).ToListAsync();
        var students = await db.Enrollments.Where(e => e.ClassYearId == classYearId)
            .OrderBy(e => e.Student.User.FullName)
            .Select(e => new { e.Id, Name = e.Student.User.FullName, Number = e.Student.StudentNumber }).ToListAsync();
        var results = await db.Results.Where(r => r.Enrollment.ClassYearId == classYearId && r.AssessmentId == assessmentId)
            .Select(r => new { r.EnrollmentId, r.SubjectId, r.Total, r.Grade, r.Status }).ToListAsync();
        foreach (var s in students)
        {
            var row = new MatrixRow { EnrollmentId = s.Id, Name = s.Name, Number = s.Number };
            foreach (var r in results.Where(x => x.EnrollmentId == s.Id))
                row.Cells[r.SubjectId] = new MatrixCell(r.Total, r.Grade, r.Status);
            vm.Rows.Add(row);
        }
        return vm;
    }
}
