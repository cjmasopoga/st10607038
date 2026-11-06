using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMonitoringSolutions.Data;
using SmartMonitoringSolutions.Models;
using SmartMonitoringSolutions.Services;
using System.Security.Claims;

namespace SmartMonitoringSolutions.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasherService _hasher;

    public AccountController(AppDbContext db, PasswordHasherService hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
        if (user is null || !_hasher.Verify(user, model.Password))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
