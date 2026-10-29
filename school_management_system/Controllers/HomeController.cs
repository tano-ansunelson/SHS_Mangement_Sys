using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using school_management_system.Models;
using school_management_system.Models.Enums;
using System.Diagnostics;

namespace school_management_system.Controllers;
    public class HomeController : Controller
    {

        [Authorize]
        public IActionResult Index()
        {
            if (User.IsInRole(Roles.Admin)) return RedirectToAction("Index", "Home", new { area = "Admin" });
            if (User.IsInRole(Roles.Teacher)) return RedirectToAction("Index", "Home", new { area = "Teacher" });
            if (User.IsInRole(Roles.HallMaster)) return RedirectToAction("Index", "Home", new { area = "HallMaster" });
            if (User.IsInRole(Roles.Student)) return RedirectToAction("Index", "Home", new { area = "Student" });
        return RedirectToAction("AccessDenied", "Account");
        }

    [AllowAnonymous]
    public IActionResult Error() => View();

}
