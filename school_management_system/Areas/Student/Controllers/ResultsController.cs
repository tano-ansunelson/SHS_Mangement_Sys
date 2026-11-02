using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Service;

namespace school_management_system.Areas.Student.Controllers;
    public class ResultsController(AppDbContext db, ResultWorkflowService workflow) : StudentBase(db)
    {
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "My results";
        return View(await workflow.GetPublishedForStudentAsync(StudentId));
    }
}

