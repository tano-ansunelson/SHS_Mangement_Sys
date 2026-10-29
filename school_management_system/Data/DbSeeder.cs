using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using school_management_system.Models;
using school_management_system.Models.Enums;

namespace school_management_system.Data;
    public static class DbSeeder
    {

    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();

        foreach (var r in new[] { Roles.Admin, Roles.Teacher, Roles.HallMaster, Roles.Student })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        var email = cfg["Admin:Email"]!;
        if (await users.FindByEmailAsync(email) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = cfg["Admin:FullName"] ?? "Administrator",
                MustChangePassword = true
            };
            var res = await users.CreateAsync(admin, cfg["Admin:Password"]!);
            if (res.Succeeded) await users.AddToRoleAsync(admin, Roles.Admin);
        }

        if (!await db.Courses.AnyAsync())
        {
            foreach (var n in new[] { "General Arts", "General Science", "Visual Arts", "Business", "Home Economics" })
                db.Courses.Add(new Course { Name = n });
        }

        if (!await db.GradeBoundaries.AnyAsync())
        {
            // WASSCE-style scale; adjust in the admin settings screen later.
            var scale = new (decimal Min, string Grade, string Remark)[]
            {
                (80, "A1", "Excellent"), (75, "B2", "Very Good"), (70, "B3", "Good"),
                (65, "C4", "Credit"), (60, "C5", "Credit"), (55, "C6", "Credit"),
                (50, "D7", "Pass"), (45, "E8", "Pass"), (0, "F9", "Fail")
            };
            foreach (var (min, grade, remark) in scale)
                db.GradeBoundaries.Add(new GradeBoundary { MinScore = min, Grade = grade, Remark = remark });
        }
        await db.SaveChangesAsync();
    }



}

