using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;

namespace school_management_system.Service;



public record RegisterStudentRequest(
    string FullName, Gender Gender, DateOnly DateOfBirth, string GuardianName, string GuardianPhone,
    int ClassYearId, ResidenceType Residence, int? HallId);

public record TeachingSlot(int SubjectId, int ClassYearId);

public record CreateTeacherRequest(
    string FullName, string? Phone, List<TeachingSlot> Slots, int? CoordinatorClassYearId);
public class RegistrationService
{
}


public class StudentService(AppDbContext db, CredentialService creds, IOptions<SchoolOptions> opt)
{
    public async Task<AccountCredentials> RegisterAsync(RegisterStudentRequest r)
    {
        var cy = await db.ClassYears
            .Include(c => c.SchoolClass)
            .Include(c => c.AcademicYear)
            .SingleOrDefaultAsync(c => c.Id == r.ClassYearId)
                ?? throw new DomainException("Class not found.");

        if (r.Residence == ResidenceType.Boarding && r.HallId is null)
            throw new DomainException("Boarding students must be assigned a hall.");
        if (r.Residence == ResidenceType.Boarding && !await db.Halls.AnyAsync(h => h.Id == r.HallId))
            throw new DomainException("Hall not found.");


        await using var tx = await db.Database.BeginTransactionAsync();
        var prefix = $"STU{cy.AcademicYear.Name[..4]}";
        var seq = await creds.NextNumberAsync(prefix);
        var number = $"{prefix}-{seq:D4}";


        var (user, password) = await creds.CreateAccountAsync(r.FullName, Roles.Student, opt.Value.StudentEmailDomain);

        var student = new Student
        {
            UserId = user.Id,
            StudentNumber = number,
            CourseId = cy.SchoolClass.CourseId,
            Gender = r.Gender,
            DateOfBirth = r.DateOfBirth,
            GuardianName = r.GuardianName,
            GuardianPhone = r.GuardianPhone,
            Residence = r.Residence

        };
        db.Students.Add(student);
        db.Enrollments.Add(new Enrollment { Student = student, ClassYearId = cy.Id, AcademicYearId = cy.AcademicYearId });

        if (r.Residence == ResidenceType.Boarding)
            db.HallRegistrations.Add(new HallRegistration
            {
                Student = student,
                HallId = r.HallId!.Value,
                AcademicYearId = cy.AcademicYearId,
            });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return new AccountCredentials(user.FullName, user.Email!, password, number);


    }
}


public class TeacherService(AppDbContext db, CredentialService creds, IOptions<SchoolOptions> opt)
{
    public async Task<AccountCredentials> CreateAsync(CreateTeacherRequest r)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var seq = await creds.NextNumberAsync("TCH");
        var staffId = $"TCH-{seq:D4}";
        var (user, password) = await creds.CreateAccountAsync(r.FullName, Roles.Teacher, opt.Value.StaffEmailDomain);

        var teacher = new Teacher
        {
            UserId = user.Id,
            StaffId = staffId,
            Phone = r.Phone
        };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();

        await ApplySlotsAsync(teacher.Id, r.Slots);
        if (r.CoordinatorClassYearId is int cyId) await ApplyCoordinatorAsync(teacher.Id, cyId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return new AccountCredentials(user.FullName, user.Email!, password, staffId);
    }


    public async Task AddAssignmentsAsync(int teacherId, List<TeachingSlot> slots)
    {
        await ApplySlotsAsync(teacherId, slots);
        await db.SaveChangesAsync();
    }

    public async Task RemoveAssignmentAsync(int teachingAssignmentId)
    {
        var ta = await db.TeachingAssignments
            .FindAsync(teachingAssignmentId) ?? throw new DomainException("Assignment not found.");
        if (await db.Results.AnyAsync(r => r.SubjectId == ta.SubjectId && r.Enrollment.ClassYearId == ta.ClassYearId))
            throw new DomainException("This assignment already has result and is kept as a record.");
        db.TeachingAssignments.Remove(ta);
        await db.SaveChangesAsync();
    }



    public async Task SetCoordinatorAsync(int teacherId, int classYearId)
    {
        await ApplyCoordinatorAsync(teacherId, classYearId);
        await db.SaveChangesAsync();
    }





    private async Task ApplySlotsAsync(int teacherId, IEnumerable<TeachingSlot> slots)
    {
        foreach (var s in slots.DistinctBy(x => (x.SubjectId, x.ClassYearId)))
        {
            var cy = await db.ClassYears.SingleOrDefaultAsync(c => c.Id == s.ClassYearId)
                     ?? throw new DomainException("Class not found.");
            if (!await db.ClassSubjects.AnyAsync(x => x.SchoolClassId == cy.SchoolClassId && x.SubjectId == s.SubjectId))
                throw new DomainException("That subject is not taught in the selected class.");
            if (await db.TeachingAssignments.AnyAsync(t => t.SubjectId == s.SubjectId && t.ClassYearId == s.ClassYearId))
                throw new DomainException("That subject already has a teacher in the selected class.");
            db.TeachingAssignments.Add(new TeachingAssignment { TeacherId = teacherId, SubjectId = s.SubjectId, ClassYearId = s.ClassYearId });
        }
    }



    private async Task ApplyCoordinatorAsync(int teacherId, int classYearId)
    {
        var cy = await db.ClassYears.SingleOrDefaultAsync(c => c.Id == classYearId) ?? throw new DomainException("Class not found.");
        if (cy.CoordinatorTeacherId is not null && cy.CoordinatorTeacherId != teacherId)
            throw new DomainException("This class already has a coordinator.");
        cy.CoordinatorTeacherId = teacherId;
    }
}
