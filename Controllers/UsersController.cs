using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMonitoringSolutions.Data;
using SmartMonitoringSolutions.Models;
using SmartMonitoringSolutions.Services;

namespace SmartMonitoringSolutions.Controllers;

/// <summary>Admin-only user account management (Username/Password/Role) backed by Azure SQL Database.</summary>
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasherService _hasher;

    public UsersController(AppDbContext db, PasswordHasherService hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _db.Users.OrderBy(u => u.Username).ToListAsync();
        return View(users);
    }

    [HttpGet]
    public IActionResult Create() => View(new RegisterUserViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegisterUserViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (await _db.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Username already exists.");
            return View(model);
        }

        var user = new User { Username = model.Username, Role = model.Role };
        user.PasswordHash = _hasher.Hash(user, model.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"User '{user.Username}' created with role {user.Role}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is not null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
