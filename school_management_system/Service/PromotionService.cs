using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Enums;

using school_management_system.Models;
namespace school_management_system.Service;

public class PromotionService(AppDbContext db)
{

    public async Task RecommendAsync(int coordinatorTeacherId, int classYearId,
        IEnumerable<(int EnrollementId, PromotionDecision Decision)> decisions)
    {
        if (!await db.ClassYears.AnyAsync(c => c.Id == classYearId && c.CoordinatorTeacherId == coordinatorTeacherId))
            throw new DomainException("You are not the coordinator of this class.");

        var enrollmentIds = await db.Enrollments
            .Where(e => e.ClassYearId == classYearId)
            .Select(e => e.Id)
            .ToListAsync();

        foreach (var (enrollmentId, decision) in decisions)
        {
            if (!enrollmentIds.Contains(enrollmentId))
                throw new DomainException("A student does not belong to this class.");
            var rec = await db.PromotionRecommendations.SingleOrDefaultAsync(p => p.EnrollmentId == enrollmentId);

            if (rec == null)
                db.PromotionRecommendations.Add(new PromotionRecommendation
                {

                    EnrollmentId = enrollmentId,
                    Decision = decision,
                    SubmittedByTeacherId = coordinatorTeacherId

                });
            else if (rec.SubmittedToAdmin && rec.AdminDecision != AdminDecision.Rejected)
                throw new DomainException("Recommedation already sent to admin cannot be changed.");
            else
            {
                rec.Decision = decision;
                rec.SubmittedByTeacherId = coordinatorTeacherId;
            }
        }
        await db.SaveChangesAsync();

    }

    public async Task SubmitToAdminAsync(int coordinatorTeacherId, int classYearId)
    {
        if (!await db.ClassYears.AnyAsync(c => c.Id == classYearId && c.CoordinatorTeacherId == coordinatorTeacherId))
            throw new DomainException("You are not the coordinator of this class.");


        var active = await db.Enrollments.Where(e => e.ClassYearId == classYearId && e.Outcome == EnrollmentOutcome.Pending
                                                    && e.Student.Status == StudentStatus.Active).Select(e => e.Id).ToListAsync();
        var recs = await db.PromotionRecommendations.Where(p => active.Contains(p.EnrollmentId)).ToListAsync();
        if (recs.Count != active.Count) throw new DomainException("Every student needs a promote/repeat recommendation first.");
        foreach (var r in recs) { r.SubmittedToAdmin = true; r.AdminDecision = AdminDecision.Pending; r.AdminNote = null; }
        await db.SaveChangesAsync();
    }

    public async Task RejectAsync(int classYearId, string note)
    {
        var recs = await db.PromotionRecommendations
            .Where(p => p.Enrollment.ClassYearId == classYearId && p.SubmittedToAdmin && p.AdminDecision == AdminDecision.Pending).ToListAsync();
        foreach (var r in recs) { r.AdminDecision = AdminDecision.Rejected; r.AdminNote = note; r.SubmittedToAdmin = false; r.DecidedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
    }


    /// <summary>Admin approves one class. Target ClassYears must already exist in the next academic year.
    /// Promoted students go to the same course and group one form up (override by moving them afterwards if needed).</summary>
    public async Task ApproveClassAsync(int classYearId, int nextAcademicYearId)
    {
        var cy = await db.ClassYears.Include(c => c.SchoolClass).SingleOrDefaultAsync(c => c.Id == classYearId)
                 ?? throw new DomainException("Class not found.");
        if (cy.AcademicYearId == nextAcademicYearId) throw new DomainException("Next academic year must differ from the class's year.");

        var recs = await db.PromotionRecommendations
            .Include(p => p.Enrollment).ThenInclude(e => e.Student)
            .Where(p => p.Enrollment.ClassYearId == classYearId && p.SubmittedToAdmin && p.AdminDecision == AdminDecision.Pending)
            .ToListAsync();
        if (recs.Count == 0) throw new DomainException("No recommendations are waiting for approval.");

        var sameClassNext = await db.ClassYears.SingleOrDefaultAsync(c => c.SchoolClassId == cy.SchoolClassId && c.AcademicYearId == nextAcademicYearId);
        var sc = cy.SchoolClass;
        var upClass = await db.SchoolClasses.SingleOrDefaultAsync(c => c.CourseId == sc.CourseId && c.FormLevel == sc.FormLevel + 1 && c.Group == sc.Group);
        var upNext = upClass == null ? null
            : await db.ClassYears.SingleOrDefaultAsync(c => c.SchoolClassId == upClass.Id && c.AcademicYearId == nextAcademicYearId);

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var rec in recs)
        {
            var e = rec.Enrollment;
            if (rec.Decision == PromotionDecision.Promote && sc.FormLevel == 3)
            {
                e.Outcome = EnrollmentOutcome.Graduated;
                e.Student.Status = StudentStatus.Graduated;
            }
            else
            {
                var target = rec.Decision == PromotionDecision.Promote ? upNext : sameClassNext;
                if (target == null)
                    throw new DomainException(rec.Decision == PromotionDecision.Promote
                        ? $"Next-year class for Form {sc.FormLevel + 1} (group {sc.Group}) does not exist. Create it first."
                        : "This class does not exist in the next academic year.");
                e.Outcome = rec.Decision == PromotionDecision.Promote ? EnrollmentOutcome.Promoted : EnrollmentOutcome.Repeated;
                db.Enrollments.Add(new Enrollment { StudentId = e.StudentId, ClassYearId = target.Id, AcademicYearId = nextAcademicYearId });

                // carry the hall registration forward; the hall master picks a new room
                var hall = await db.HallRegistrations.Where(h => h.StudentId == e.StudentId && h.AcademicYearId == cy.AcademicYearId)
                    .Select(h => (int?)h.HallId).FirstOrDefaultAsync();
                if (hall != null && !await db.HallRegistrations.AnyAsync(h => h.StudentId == e.StudentId && h.AcademicYearId == nextAcademicYearId))
                    db.HallRegistrations.Add(new HallRegistration { StudentId = e.StudentId, HallId = hall.Value, AcademicYearId = nextAcademicYearId });
            }
            rec.AdminDecision = AdminDecision.Approved;
            rec.DecidedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }



}

