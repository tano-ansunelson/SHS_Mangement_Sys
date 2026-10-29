using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using school_management_system.Models.Enums;
using school_management_system.Service;

var builder = WebApplication.CreateBuilder(args);



builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 8;
    o.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AppClaimsFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.Configure<GradingOptions>(builder.Configuration.GetSection("Grading"));
builder.Services.Configure<SchoolOptions>(builder.Configuration.GetSection("School"));


builder.Services.AddScoped<CredentialService>();
builder.Services.AddScoped<AcademicSetupService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<TeacherService>();
builder.Services.AddScoped<HallService>();
builder.Services.AddScoped<RoomAllocationService>();
builder.Services.AddScoped<GradingService>();
builder.Services.AddScoped<ResultWorkflowService>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<DashboardQueries>();
builder.Services.AddScoped<MatrixService>();
builder.Services.AddScoped<AccountAdminService>();


builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin))
    .AddPolicy("TeacherOnly", p => p.RequireRole(Roles.Teacher))
    .AddPolicy("HallMasterOnly", p => p.RequireRole(Roles.HallMaster))
    .AddPolicy("StudentOnly", p => p.RequireRole(Roles.Student))
     .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());








// Add services to the container.
builder.Services.AddControllersWithViews();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
//app.UseMiddleware<ForcePasswordChangeMiddleware>();
app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

await DbSeeder.SeedAsync(app.Services);
app.Run();
