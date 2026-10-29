using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using school_management_system.Models;
using school_management_system.Models.Domain;

namespace school_management_system.Controllers;

public class AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) : Controller
{

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home")
        : View(new LoginVm { ReturnUrl = returnUrl });



    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);
        var user = await users.FindByEmailAsync(vm.Email);
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError("", "Invalid email or password");
            return View(vm);
        }

        var result = await signIn.PasswordSignInAsync(user, vm.Password, vm.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
            return !string.IsNullOrEmpty(vm.ReturnUrl) &&
                Url.IsLocalUrl(vm.ReturnUrl) ? LocalRedirect(vm.ReturnUrl)
                : RedirectToAction("Index", "Home");

        ModelState.AddModelError("", result.IsLockedOut ? "Too many attempts. Try again in a few minutes." : "Invalid email or password.");

        return View(vm);
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }



    [HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        var u = await users.GetUserAsync(User);
        ViewData["Forced"] = u?.MustChangePassword;
        return View(new ChangePasswordVm());
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm) 
    {
        var u = await users.GetUserAsync(User);
        if (u == null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View(vm);
        var r = await users.ChangePasswordAsync(u, vm.Current, vm.New);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        u.MustChangePassword = false;
        await users.UpdateAsync(u);
        await signIn.RefreshSignInAsync(u);
        TempData["Success"] = "Password updated.";
        return RedirectToAction("Index", "Home");

    }


    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

