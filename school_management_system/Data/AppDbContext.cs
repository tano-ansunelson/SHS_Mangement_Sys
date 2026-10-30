using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using school_management_system.Models;

namespace school_management_system.Data;
    public class AppDbContext(DbContextOptions<AppDbContext> options):IdentityDbContext<ApplicationUser>(options)
    {
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<CourseSubject> CourseSubjects => Set<CourseSubject>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<ClassYear> ClassYears => Set<ClassYear>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<TeachingAssignment> TeachingAssignments => Set<TeachingAssignment>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<HallMasterAssignment> HallMasterAssignments => Set<HallMasterAssignment>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<HallRegistration> HallRegistrations => Set<HallRegistration>();
    public DbSet<RoomAllocation> RoomAllocations => Set<RoomAllocation>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<ResultAuditLog> ResultAuditLogs => Set<ResultAuditLog>();
    public DbSet<GradeBoundary> GradeBoundaries => Set<GradeBoundary>();
    public DbSet<PromotionRecommendation> PromotionRecommendations => Set<PromotionRecommendation>();
    public DbSet<IdCounter> IdCounters => Set<IdCounter>();








    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // History must never disappear by cascade: restrict all FKs between our own entities.
        foreach (var fk in b.Model.GetEntityTypes()
                      .Where(t => t.ClrType.Namespace != null &&
                         (t.ClrType.Namespace == "school_management_system.Models" ||
                          t.ClrType.Namespace.StartsWith("school_management_system.Models.")))
                     .SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        b.Entity<Enrollment>(e =>
{
    e.HasOne(x => x.Student)
        .WithMany(x => x.Enrollments)
        .HasForeignKey(x => x.StudentId)
        .OnDelete(DeleteBehavior.NoAction);

    e.HasOne(x => x.ClassYear)
        .WithMany(x => x.Enrollments)
        .HasForeignKey(x => x.ClassYearId)
        .OnDelete(DeleteBehavior.NoAction);

    e.HasOne(x => x.AcademicYear)
        .WithMany(x => x.Enrollments)
        .HasForeignKey(x => x.AcademicYearId)
        .OnDelete(DeleteBehavior.NoAction);
});

        b.Entity<CourseSubject>().HasKey(x => new { x.CourseId, x.SubjectId });
        b.Entity<ClassSubject>().HasKey(x => new { x.SchoolClassId, x.SubjectId });
        b.Entity<IdCounter>().HasKey(x => x.Key);

        b.Entity<Teacher>().HasIndex(x => x.StaffId).IsUnique();
        b.Entity<Teacher>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<Student>().HasIndex(x => x.StudentNumber).IsUnique();
        b.Entity<Student>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<Student>().HasIndex(x => x.AdmissionNumber).IsUnique();
        b.Entity<Course>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Subject>().HasIndex(x => x.Code).IsUnique();
        b.Entity<SchoolClass>().HasIndex(x => new { x.CourseId, x.FormLevel, x.Group }).IsUnique();
        b.Entity<AcademicYear>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Semester>().HasIndex(x => new { x.AcademicYearId, x.Number }).IsUnique();
        b.Entity<ClassYear>().HasIndex(x => new { x.SchoolClassId, x.AcademicYearId }).IsUnique();
        b.Entity<Enrollment>().HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();
        b.Entity<TeachingAssignment>().HasIndex(x => new { x.SubjectId, x.ClassYearId }).IsUnique();
        b.Entity<HallMasterAssignment>().HasIndex(x => new { x.HallId, x.AcademicYearId }).IsUnique();
        b.Entity<Room>().HasIndex(x => new { x.HallId, x.Number }).IsUnique();
        b.Entity<HallRegistration>().HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();
        b.Entity<RoomAllocation>().HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();
        b.Entity<Assessment>().HasIndex(x => new { x.SemesterId, x.Type }).IsUnique();
        b.Entity<PromotionRecommendation>().HasIndex(x => x.EnrollmentId).IsUnique();

        b.Entity<Result>(e =>
        {
            e.HasIndex(x => new { x.EnrollmentId, x.SubjectId, x.AssessmentId }).IsUnique();
            e.Property(x => x.ClassScore).HasPrecision(5, 2);
            e.Property(x => x.ExamScore).HasPrecision(5, 2);
            e.Property(x => x.Total).HasPrecision(5, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        b.Entity<GradeBoundary>().Property(x => x.MinScore).HasPrecision(5, 2);
    }
}

