using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Domain;
using school_management_system.Service;
using System.Security.Claims;

namespace school_management_system.Controllers;

public abstract class PortalController(AppDbContext db) : Controller
{
    protected readonly AppDbContext Db = db;
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;


    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ViewData["YearName"] = await Db.AcademicYears.Where(y => y.IsCurrent).Select(y => y.Name).FirstOrDefaultAsync();
        await OnPortalAsync();
        await base.OnActionExecutionAsync(context, next);
    }

    protected virtual Task OnPortalAsync() => Task.CompletedTask;
    protected void Flash(string message) => TempData["Success"] = message;
    protected void Fail(string message) => TempData["Error"] = message;





    /// <summary>Runs a service call, turning business-rule errors into a friendly message.</summary>
    protected async Task<bool> Run(Func<Task> action, string? success = null)
    {
        try { await action(); if (success != null) Flash(success); return true; }
        catch (DomainException e) { Fail(e.Message); return false; }
        catch (DbUpdateException) { Fail("Could not save. The record may be duplicated or still in use."); return false; }
    }

    protected IActionResult Confirm(string title, string message, string action, object id, string back, string button = "Delete") =>
        View("ConfirmDelete", new ConfirmDeleteVm { Title = title, Message = message, Action = action, Id = id.ToString()!, Back = back, Button = button });

    protected async Task<int?> CurrentYearIdAsync() =>
        await Db.AcademicYears.Where(y => y.IsCurrent).Select(y => (int?)y.Id).FirstOrDefaultAsync();

    public static string ClassLabel(SchoolClass c) => $"Form {c.FormLevel} {c.Course.Name} {c.Group}";

}

