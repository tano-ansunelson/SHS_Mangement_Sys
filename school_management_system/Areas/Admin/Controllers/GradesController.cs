using Microsoft.AspNetCore.Mvc;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace school_management_system.Areas.Admin.Controllers;

public class GradesController(AppDbContext db) : AdminBase(db)
{
    public async Task<IActionResult> Index() => View(await Db.GradeBoundaries.OrderByDescending(g => g.MinScore).ToListAsync());

    public IActionResult Create() => View("Form", new GradeVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GradeVm vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        Db.GradeBoundaries.Add(new GradeBoundary { MinScore = vm.MinScore, Grade = vm.Grade.Trim().ToUpperInvariant(), Remark = vm.Remark });
        await Db.SaveChangesAsync();
        Flash("Grade added.");
        return RedirectToAction(nameof(Index));
    }




    public async Task<IActionResult> Edit(int id)
    {
        var g = await Db.GradeBoundaries.FindAsync(id);
        return g == null ? NotFound() : View("Form", new GradeVm { Id = g.Id, MinScore = g.MinScore, Grade = g.Grade, Remark = g.Remark });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GradeVm vm)
    {
        var g = await Db.GradeBoundaries.FindAsync(vm.Id);
        if (g == null) return NotFound();
        if (!ModelState.IsValid) return View("Form", vm);
        g.MinScore = vm.MinScore; g.Grade = vm.Grade.Trim().ToUpperInvariant(); g.Remark = vm.Remark;
        await Db.SaveChangesAsync();
        Flash("Grade updated. Existing results keep the grade they were given.");
        return RedirectToAction(nameof(Index));
    }





    public async Task<IActionResult> Delete(int id)
    {
        var g = await Db.GradeBoundaries.FindAsync(id);
        return g == null ? NotFound() : Confirm("Delete grade", $"Delete grade {g.Grade} (from {g.MinScore})?", Url.Action(nameof(DeleteConfirmed))!, id, Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var g = await Db.GradeBoundaries.FindAsync(id);
        if (g != null) { Db.GradeBoundaries.Remove(g); await Db.SaveChangesAsync(); Flash("Grade deleted."); }
        return RedirectToAction(nameof(Index));
    }




}

