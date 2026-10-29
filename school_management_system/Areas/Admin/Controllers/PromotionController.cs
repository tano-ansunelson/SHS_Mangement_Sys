using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using school_management_system.Data;
using school_management_system.Models.Enums;
using school_management_system.Service;
using Microsoft.EntityFrameworkCore;

namespace school_management_system.Areas.Admin.Controllers;
public record PromotionClassRow(int ClassYearId, string Name, int Students, int Promote, int Repeat, int Pending, bool Done);
public record PromotionStudentRow(string Name, string Number, PromotionDecision Decision, decimal? Average);


public class PromotionController(AppDbContext db, PromotionService promotion) : AdminBase(db)
{
    public async Task<IActionResult> Index(int? nextYearId)
    {
        var y = await CurrentYearIdAsync();
        var years = await Db.AcademicYears.Where(a => a.Id != y).OrderByDescending(a => a.Name).ToListAsync();
        nextYearId ??= years.Select(a => (int?)a.Id).FirstOrDefault();
        ViewBag.Years = new SelectList(years, "Id", "Name", nextYearId);
        ViewData["NextYearId"] = nextYearId;
        var rows = await Db.ClassYears.Where(c => c.AcademicYearId == y)
            .OrderBy(c => c.SchoolClass.Course.Name).ThenBy(c => c.SchoolClass.FormLevel).ThenBy(c => c.SchoolClass.Group)
            .Select(c => new PromotionClassRow(c.Id, "Form " + c.SchoolClass.FormLevel + " " + c.SchoolClass.Course.Name + " " + c.SchoolClass.Group,
                Db.Enrollments.Count(e => e.ClassYearId == c.Id && e.Student.Status != StudentStatus.Withdrawn),
                Db.PromotionRecommendations.Count(p => p.Enrollment.ClassYearId == c.Id && p.SubmittedToAdmin && p.Decision == PromotionDecision.Promote),
                Db.PromotionRecommendations.Count(p => p.Enrollment.ClassYearId == c.Id && p.SubmittedToAdmin && p.Decision == PromotionDecision.Repeat),
                Db.PromotionRecommendations.Count(p => p.Enrollment.ClassYearId == c.Id && p.SubmittedToAdmin && p.AdminDecision == AdminDecision.Pending),
                Db.PromotionRecommendations.Any(p => p.Enrollment.ClassYearId == c.Id && p.AdminDecision == AdminDecision.Approved))).ToListAsync();
        return View(rows);
    }


    public async Task<IActionResult> Review(int classYearId, int nextYearId)
    {
        var cy = await Db.ClassYears.Include(c => c.SchoolClass).ThenInclude(s => s.Course).SingleOrDefaultAsync(c => c.Id == classYearId);
        if (cy == null) return NotFound();
        ViewData["Title"] = "Review promotion";
        ViewData["ClassName"] = ClassLabel(cy.SchoolClass);
        ViewData["ClassYearId"] = classYearId; ViewData["NextYearId"] = nextYearId; ViewData["Form"] = cy.SchoolClass.FormLevel;
        ViewData["NextYear"] = await Db.AcademicYears.Where(a => a.Id == nextYearId).Select(a => a.Name).FirstOrDefaultAsync();
        var rows = await Db.PromotionRecommendations.Where(p => p.Enrollment.ClassYearId == classYearId && p.SubmittedToAdmin && p.AdminDecision == AdminDecision.Pending)
            .OrderBy(p => p.Enrollment.Student.User.FullName)
            .Select(p => new PromotionStudentRow(p.Enrollment.Student.User.FullName, p.Enrollment.Student.StudentNumber, p.Decision,
                Db.Results.Where(r => r.EnrollmentId == p.EnrollmentId).Average(r => (decimal?)r.Total))).ToListAsync();
        return View(rows);
    }



    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int classYearId, int nextYearId)
    {
        var ok = await Run(() => promotion.ApproveClassAsync(classYearId, nextYearId), "Promotion approved. New enrollments were created for the next year.");
        return ok ? RedirectToAction(nameof(Index), new { nextYearId }) : RedirectToAction(nameof(Review), new { classYearId, nextYearId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int classYearId, int nextYearId, string note)
    {
        await Run(() => promotion.RejectAsync(classYearId, string.IsNullOrWhiteSpace(note) ? "Returned by admin." : note), "Sent back to the class coordinator.");
        return RedirectToAction(nameof(Index), new { nextYearId });
    }







}

