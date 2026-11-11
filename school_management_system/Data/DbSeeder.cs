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

        // ---------- courses ----------
        const string Arts = "General Arts", Science = "General Science", Visual = "Visual Arts",
                     Business = "Business", Home = "Home Economics";
        var everyCourse = new[] { Arts, Science, Visual, Business, Home };

        if (!await db.Courses.AnyAsync())
        {
            foreach (var n in everyCourse) db.Courses.Add(new Course { Name = n });
            await db.SaveChangesAsync();    // the courses must exist before subjects are linked to them
        }

        // ---------- grade scale ----------
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

        // ---------- subjects, and which courses take them ----------
        var catalogue = new (string Code, string Name, string[] Courses)[]
        {
            // core subjects: every course
            ("COR101", "Core Mathematics", everyCourse),
            ("COR102", "English Language", everyCourse),
            ("COR103", "Integrated Science", everyCourse),
            ("COR104", "Social Studies", everyCourse),
            ("COR105", "Information & Communications Technology", everyCourse),

            // General Science
            ("SCI101", "Elective Mathematics", new[] { Science }),
            ("SCI102", "Biology", new[] { Science }),
            ("SCI103", "Chemistry", new[] { Science }),
            ("SCI104", "Physics", new[] { Science }),

            // Business
            ("BUS101", "Financial Accounting", new[] { Business }),
            ("BUS102", "Cost Accounting", new[] { Business }),
            ("BUS103", "Business Management", new[] { Business }),
            ("BUS104", "Economics", new[] { Business }),

            // General Arts
            ("ART101", "Literature in English", new[] { Arts }),
            ("ART102", "Government", new[] { Arts }),
            ("ART103", "History", new[] { Arts }),
            ("ART104", "Geography", new[] { Arts }),
            ("ART105", "French", new[] { Arts }),
            ("ART106", "Ghanaian Language", new[] { Arts }),

            // Home Economics
            ("HEC101", "Food and Nutrition", new[] { Home }),
            ("HEC102", "Management in Living", new[] { Home }),
            ("HEC103", "Clothing and Textiles", new[] { Home }),

            // Visual Arts
            ("VAR101", "General Knowledge in Art", new[] { Visual }),
            ("VAR102", "Graphic Design", new[] { Visual }),
            ("VAR103", "Sculpture", new[] { Visual }),
            ("VAR104", "Ceramics", new[] { Visual }),
        };

        var courses = await db.Courses.ToDictionaryAsync(c => c.Name);
        var subjects = await db.Subjects.ToDictionaryAsync(s => s.Code);
        var linked = (await db.CourseSubjects.Select(x => new { x.CourseId, x.SubjectId }).ToListAsync())
            .Select(x => (x.CourseId, x.SubjectId)).ToHashSet();

        foreach (var (code, name, forCourses) in catalogue)
        {
            if (!subjects.TryGetValue(code, out var subject))
            {
                subject = new Subject { Code = code, Name = name };
                db.Subjects.Add(subject);
                subjects[code] = subject;
            }
            foreach (var courseName in forCourses)
                if (courses.TryGetValue(courseName, out var course) &&
                    (subject.Id == 0 || !linked.Contains((course.Id, subject.Id))))
                    db.CourseSubjects.Add(new CourseSubject { Course = course, Subject = subject });
        }

        await db.SaveChangesAsync();
    }



}

