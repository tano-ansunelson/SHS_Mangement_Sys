using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Enums;

using school_management_system.Models;
namespace school_management_system.Service;

public class PromotionService(AppDbContext db)
{

    /// <summary>Promotion (and graduation) can only happen once the class's academic year is in its second semester.</summary>
    public Task<bool> IsPromotionOpenAsync(int classYearId) =>
        db.Semesters.AnyAsync(s => s.Number == 2 && s.IsCurrent && s.AcademicYearId == db.ClassYears.Where(c => c.Id == classYearId).Select(c => c.AcademicYearId).FirstOrDefault());

    /// <summary>Finds a class's record for an academic year, adding it if the class was created after that year.</summary>
    private async Task<ClassYear> EnsureClassYearAsync(int schoolClassId, int academicYearId)
    {
        var existing = await db.ClassYears.SingleOrDefaultAsync(c => c.SchoolClassId == schoolClassId && c.AcademicYearId == academicYearId);
        if (existing != null) return existing;
        var created = new ClassYear { SchoolClassId = schoolClassId, AcademicYearId = academicYearId };
        db.ClassYears.Add(created);
        return created;
    }

    private async Task EnsurePromotionOpenAsync(int classYearId)
    {
        if (!await IsPromotionOpenAsync(classYearId))
            throw new DomainException("Promotion opens in the second semester. The admin starts it from Academic years.");
    }

    public async Task RecommendAsync(int coordinatorTeacherId, int classYearId,
        IEnumerable<(int EnrollmentId, PromotionDecision Decision)> decisions)
    {
        await EnsurePromotionOpenAsync(classYearId);
        if (!await db.ClassYears.AnyAsync(c => c.Id == classYearId && c.CoordinatorTeacherId == coordinatorTeacherId))
            throw new DomainException("You are not the coordinator of this class.");

        var enrollmentIds = await db.Enrollments.Where(e => e.ClassYearId == classYearId).Select(e => e.Id).ToListAsync();
        foreach (var (enrollmentId, decision) in decisions)
        {
            if (!enrollmentIds.Contains(enrollmentId)) throw new DomainException("A student does not belong to this class.");
            var rec = await db.PromotionRecommendations.SingleOrDefaultAsync(p => p.EnrollmentId == enrollmentId);
            if (rec == null)
                db.PromotionRecommendations.Add(new PromotionRecommendation
                { EnrollmentId = enrollmentId, Decision = decision, SubmittedByTeacherId = coordinatorTeacherId });
            else if (rec.SubmittedToAdmin && rec.AdminDecision != AdminDecision.Rejected)
                throw new DomainException("Recommendations already sent to admin cannot be changed.");
            else { rec.Decision = decision; rec.SubmittedByTeacherId = coordinatorTeacherId; }
        }
        await db.SaveChangesAsync();
    }

    //public async Task SubmitToAdminAsync(int coordinatorTeacherId, int classYearId)
    //{
    //    await EnsurePromotionOpenAsync(classYearId);
    //    if (!await db.ClassYears.AnyAsync(c => c.Id == classYearId && c.CoordinatorTeacherId == coordinatorTeacherId))
    //        throw new DomainException("You are not the coordinator of this class.");

    //    var active = await db.Enrollments.Where(e => e.ClassYearId == classYearId && e.Outcome == EnrollmentOutcome.Pending
    //                                                 && e.Student.Status == StudentStatus.Active).Select(e => e.Id).ToListAsync();
    //    var recs = await db.PromotionRecommendations.Where(p => active.Contains(p.EnrollmentId)).ToListAsync();
    //    if (recs.Count != active.Count) throw new DomainException("Every student needs a promote/repeat recommendation first.");
    //    foreach (var r in recs) { r.SubmittedToAdmin = true; r.AdminDecision = AdminDecision.Pending; r.AdminNote = null; }
    //    await db.SaveChangesAsync();
    //}



    public async Task SubmitToAdminAsync(int coordinatorTeacherId, int classYearId)
    {
        await EnsurePromotionOpenAsync(classYearId);
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
        await EnsurePromotionOpenAsync(classYearId);
        var cy = await db.ClassYears.Include(c => c.SchoolClass).ThenInclude(s => s.Course).SingleOrDefaultAsync(c => c.Id == classYearId)
                 ?? throw new DomainException("Class not found.");
        if (cy.AcademicYearId == nextAcademicYearId) throw new DomainException("Next academic year must differ from the class's year.");

        var recs = await db.PromotionRecommendations
            .Include(p => p.Enrollment).ThenInclude(e => e.Student)
            .Where(p => p.Enrollment.ClassYearId == classYearId && p.SubmittedToAdmin && p.AdminDecision == AdminDecision.Pending)
            .ToListAsync();
        if (recs.Count == 0) throw new DomainException("No recommendations are waiting for approval.");

        if (!await db.AcademicYears.AnyAsync(y => y.Id == nextAcademicYearId)) throw new DomainException("Select the academic year to promote into.");
        var sc = cy.SchoolClass;
        var sameClassNext = await EnsureClassYearAsync(cy.SchoolClassId, nextAcademicYearId);

        // Promoted students go to the same course and group, one form up. Forms 1 to 3 of a group are separate classes,
        // so the next form's class must exist. If it exists but has no record for the new year yet, add the record now.
        ClassYear? upNext = null;
        if (recs.Any(r => r.Decision == PromotionDecision.Promote) && sc.FormLevel < 3)
        {
            var upClass = await db.SchoolClasses.SingleOrDefaultAsync(c => c.CourseId == sc.CourseId && c.FormLevel == sc.FormLevel + 1 && c.Group == sc.Group)
                          ?? throw new DomainException($"The class “Form {sc.FormLevel + 1} {sc.Course.Name} {sc.Group}” does not exist yet. " +
                                                       "Create it under Classes (same course, Form " + (sc.FormLevel + 1) + ", group " + sc.Group + ") and approve again.");
            upNext = await EnsureClassYearAsync(upClass.Id, nextAcademicYearId);
        }

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
                if (target == null) throw new DomainException("The next-year class could not be found.");
                e.Outcome = rec.Decision == PromotionDecision.Promote ? EnrollmentOutcome.Promoted : EnrollmentOutcome.Repeated;
                db.Enrollments.Add(new Enrollment { StudentId = e.StudentId, ClassYear = target, AcademicYearId = nextAcademicYearId });

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

