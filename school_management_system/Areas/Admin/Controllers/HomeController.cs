using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models.Domain;
using school_management_system.Models.Enums;

namespace school_management_system.Areas.Admin.Controllers;
    public class HomeController(AppDbContext db) : AdminBase(db)
    {
        public async Task<IActionResult> Index()
        {
        ViewData["Title"] = "Dashboard";
        var yearId = await CurrentYearIdAsync();

        var vm = new AdminDashVm
        {
            Students = await Db.Students.CountAsync(s => s.Status == StudentStatus.Active),
            Boarding = await Db.Students.CountAsync(s => s.Status == StudentStatus.Active && s.Residence == ResidenceType.Boarding),
            Day = await Db.Students.CountAsync(s => s.Status == StudentStatus.Active && s.Residence == ResidenceType.Day),

            Teachers = await Db.Teachers.CountAsync(t => t.IsActive),
            Classes = await Db.SchoolClasses.CountAsync(),
            Halls = await Db.Halls.CountAsync(),
            Subjects = await Db.Subjects.CountAsync(),
            Courses = await Db.Courses.CountAsync(),
            AwaitingApproval = yearId == null ? 0 : await Db.Results
                .Where(r => r.Enrollment.AcademicYearId == yearId && r.Status == ResultStatus.Reviewed)
                .Select( r => new {r.Enrollment.ClassYearId, r.AssessmentId}).Distinct().CountAsync(),
            PromotionsPending= await Db.PromotionRecommendations
                .Where(p => p.SubmittedToAdmin && p.AdminDecision == AdminDecision.Pending)
                .Select(p => p.Enrollment.ClassYearId).Distinct().CountAsync(),
            ByCourse = await Db.Courses.OrderBy(c => c.Name)
                .Select(c => new CountRow(c.Name, Db.Students.Count(s => s.CourseId == c.Id && s.Status == StudentStatus.Active))).ToListAsync()

        };

        ViewData["HasYear"] = yearId != null;


            return View(vm);
        }
    }

