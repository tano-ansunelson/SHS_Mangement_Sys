using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Service;

namespace school_management_system.Areas.Student.Controllers;

public class ResultsController(AppDbContext db, ResultWorkflowService workflow) : StudentBase(db)
{
    public async Task<IActionResult> Index(string? year, int? semester)
    {
        ViewData["Title"] = "My results";

        var all = await workflow.GetPublishedForStudentAsync(StudentId);
        var years = all.Select(r => r.AcademicYear).Distinct().OrderByDescending(y => y).ToList();
        var chosenYear = !string.IsNullOrEmpty(year) ? year : years.FirstOrDefault();

        var chosenSemester = semester ?? all.Where(r => r.AcademicYear == chosenYear).Select(r => r.Semester).DefaultIfEmpty(1).Max();

        ViewData["Years"] = years;
        ViewData["Year"] = chosenYear;
        ViewData["Semester"] = chosenSemester;

        return View(all.Where(r => r.AcademicYear == chosenYear && r.Semester == chosenSemester).ToList());
    }
}

