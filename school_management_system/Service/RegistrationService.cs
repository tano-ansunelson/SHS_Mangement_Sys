using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;

namespace school_management_system.Service;


public static class Text
{
    public static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

/// <summary>Everything about a student except the account, class and hall.</summary>
public class StudentProfile
{
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? RollNumber { get; set; }
    public string? AdmissionNumber { get; set; }
    public string? Category { get; set; }
    public string? Phone { get; set; }
    public string? ContactEmail { get; set; }
    public string? FatherName { get; set; }
    public string? FatherPhone { get; set; }
    public string? FatherOccupation { get; set; }
    public string? MotherName { get; set; }
    public string? MotherPhone { get; set; }
    public string? MotherOccupation { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    // set only when a new photo was uploaded; null keeps the existing photo
    public string? PhotoPath { get; set; }
    public string? FatherPhotoPath { get; set; }
    public string? MotherPhotoPath { get; set; }

    public bool HasContact =>
        (!string.IsNullOrWhiteSpace(FatherName) && !string.IsNullOrWhiteSpace(FatherPhone)) ||
        (!string.IsNullOrWhiteSpace(MotherName) && !string.IsNullOrWhiteSpace(MotherPhone)) ||
        (!string.IsNullOrWhiteSpace(GuardianName) && !string.IsNullOrWhiteSpace(GuardianPhone));

    public void ApplyTo(Student s)
    {
        s.Gender = Gender; s.DateOfBirth = DateOfBirth;
        s.RollNumber = Text.Clean(RollNumber); s.AdmissionNumber = Text.Clean(AdmissionNumber); s.Category = Text.Clean(Category);
        s.Phone = Text.Clean(Phone); s.ContactEmail = Text.Clean(ContactEmail);
        s.FatherName = Text.Clean(FatherName); s.FatherPhone = Text.Clean(FatherPhone); s.FatherOccupation = Text.Clean(FatherOccupation);
        s.MotherName = Text.Clean(MotherName); s.MotherPhone = Text.Clean(MotherPhone); s.MotherOccupation = Text.Clean(MotherOccupation);
        s.GuardianName = Text.Clean(GuardianName) ?? ""; s.GuardianPhone = Text.Clean(GuardianPhone) ?? "";
        if (PhotoPath != null) s.PhotoPath = PhotoPath;
        if (FatherPhotoPath != null) s.FatherPhotoPath = FatherPhotoPath;
        if (MotherPhotoPath != null) s.MotherPhotoPath = MotherPhotoPath;
    }
}
public class TeacherProfile
{
    public string? Phone { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? FatherName { get; set; }
    public string? MotherName { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public ContractType? ContractType { get; set; }
    public Shift? Shift { get; set; }
    public string? WorkLocation { get; set; }
    public DateOnly? JoinDate { get; set; }
    public string? ContactEmail { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Qualification { get; set; }
    public string? PhotoPath { get; set; }          // null keeps the existing photo

    public void ApplyTo(Teacher t)
    {
        t.Phone = Text.Clean(Phone); t.Gender = Gender; t.DateOfBirth = DateOfBirth;
        t.FatherName = Text.Clean(FatherName); t.MotherName = Text.Clean(MotherName);
        t.MaritalStatus = MaritalStatus; t.ContractType = ContractType; t.Shift = Shift;
        t.WorkLocation = Text.Clean(WorkLocation); t.JoinDate = JoinDate; t.ContactEmail = Text.Clean(ContactEmail);
        t.ExperienceYears = ExperienceYears; t.Qualification = Text.Clean(Qualification);
        if (PhotoPath != null) t.PhotoPath = PhotoPath;
    }
}



public record RegisterStudentRequest(string FullName, int ClassYearId, ResidenceType Residence, int? HallId, StudentProfile Profile);

public record TeachingSlot(int SubjectId, int ClassYearId);

public record CreateTeacherRequest(string FullName, string? StaffId, TeacherProfile Profile, List<TeachingSlot> Slots, int? CoordinatorClassYearId);



public class StudentService(AppDbContext db, CredentialService creds, IOptions<SchoolOptions> opt)
{
    public async Task<AccountCredentials> RegisterAsync(RegisterStudentRequest r)
    {
        var p = r.Profile;
        var cy = await db.ClassYears.Include(c => c.SchoolClass).Include(c => c.AcademicYear)
                     .SingleOrDefaultAsync(c => c.Id == r.ClassYearId)
                 ?? throw new DomainException("Class not found.");
        if (r.Residence == ResidenceType.Boarding && r.HallId is null)
            throw new DomainException("Boarding students must be assigned a hall.");
        if (r.Residence == ResidenceType.Boarding && !await db.Halls.AnyAsync(h => h.Id == r.HallId))
            throw new DomainException("Hall not found.");
        if (!p.HasContact)
            throw new DomainException("Enter at least one parent or guardian with a name and phone number.");
        var admission = Text.Clean(p.AdmissionNumber);
        if (admission != null && await db.Students.AnyAsync(s => s.AdmissionNumber == admission))
            throw new DomainException("This admission number is already used by another student.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var prefix = $"STU{cy.AcademicYear.Name[..4]}";
        var seq = await creds.NextNumberAsync(prefix);
        var number = $"{prefix}-{seq:D4}";

        var (user, password) = await creds.CreateAccountAsync
            (
            r.FullName,
            Roles.Student,
            opt.Value.StudentEmailDomain
            );
        var student =
            new Student
            { UserId = user.Id, 
                StudentNumber = number, 
                CourseId = cy.SchoolClass.CourseId, 
                Residence = r.Residence 
            };
        p.ApplyTo(student);
        db.Students.Add(student);
        db.Enrollments.Add(new 
            Enrollment {
            Student = student,
            ClassYearId = cy.Id, 
            AcademicYearId = cy.AcademicYearId 
        });
        if (r.Residence == ResidenceType.Boarding)
            db.HallRegistrations.Add(new
                HallRegistration { 
                Student = student,
                HallId = r.HallId!.Value,
                AcademicYearId = cy.AcademicYearId 
            });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new AccountCredentials(
            user.FullName, 
            user.Email!,
            password,
            number);
    }
}


public class TeacherService(AppDbContext db, CredentialService creds, IOptions<SchoolOptions> opt)
{
    public async Task<AccountCredentials> CreateAsync(CreateTeacherRequest r)
    {
        var manualId = Text.Clean(r.StaffId);
        if (manualId != null && await db.Teachers.AnyAsync(t => t.StaffId == manualId))
            throw new DomainException("This teacher ID is already used.");

        await using var tx = await db.Database.BeginTransactionAsync();
        string staffId;
        if (manualId != null) staffId = manualId;
        else { var seq = await creds.NextNumberAsync("TCH"); staffId = $"TCH-{seq:D4}"; }

        var (user, password) = await creds.CreateAccountAsync(r.FullName, Roles.Teacher, opt.Value.StaffEmailDomain);
        var teacher = new Teacher { UserId = user.Id, StaffId = staffId };
        r.Profile.ApplyTo(teacher);
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
