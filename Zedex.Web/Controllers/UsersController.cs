using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Identity;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

[Authorize(Roles = DbSeeder.AdminRole)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private const int PageSize = 10;

    public UsersController(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.UserName!, pattern) ||
                EF.Functions.ILike(u.FullName, pattern) ||
                EF.Functions.ILike(u.Email!, pattern));
        }

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new UserListItemViewModel
            {
                Id = u.Id,
                UserName = u.UserName!,
                FullName = u.FullName,
                Email = u.Email!,
                IsActive = u.IsActive,
                CreatedDate = u.CreatedDate,
                Role = (from ur in _db.UserRoles
                        where ur.UserId == u.Id
                        join r in _db.Roles on ur.RoleId equals r.Id
                        select r.Name!).FirstOrDefault() ?? "—"
            })
            .ToListAsync();

        return View(new UserListViewModel
        {
            Search = search,
            Items = new PagedResult<UserListItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new UserFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Password))
            ModelState.AddModelError(nameof(vm.Password), "Password is required.");
        if (!ModelState.IsValid)
            return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.UserName.Trim(),
            Email = vm.Email.Trim(),
            EmailConfirmed = true,
            FullName = vm.FullName.Trim(),
            IsActive = vm.IsActive,
            CreatedDate = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, vm.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(vm);
        }

        var role = vm.Role == DbSeeder.AdminRole ? DbSeeder.AdminRole : DbSeeder.WorkerRole;
        await _userManager.AddToRoleAsync(user, role);

        if (role == DbSeeder.WorkerRole)
        {
            var permission = new UserPermission { UserId = user.Id };
            vm.ApplyTo(permission);
            _db.UserPermissions.Add(permission);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = $"User \"{user.UserName}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var vm = new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            UserName = user.UserName!,
            Email = user.Email!,
            Role = roles.Contains(DbSeeder.AdminRole) ? DbSeeder.AdminRole : DbSeeder.WorkerRole,
            IsActive = user.IsActive
        };

        var permission = await _db.UserPermissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == id);
        if (permission is not null)
            vm.LoadFrom(permission);

        ViewData["IsSelf"] = id == CurrentUserId;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormViewModel vm)
    {
        if (vm.Id is null)
            return NotFound();

        ModelState.Remove(nameof(vm.Password)); // never edited here
        var isSelf = vm.Id == CurrentUserId;
        ViewData["IsSelf"] = isSelf;
        if (!ModelState.IsValid)
            return View(vm);

        var user = await _userManager.FindByIdAsync(vm.Id);
        if (user is null)
            return NotFound();

        // Lockout protection: you cannot demote or deactivate yourself.
        if (isSelf)
        {
            vm.Role = DbSeeder.AdminRole;
            vm.IsActive = true;
        }

        var wasActive = user.IsActive;
        user.FullName = vm.FullName.Trim();
        user.IsActive = vm.IsActive;

        var identityErrors = new List<IdentityError>();
        if (user.UserName != vm.UserName.Trim())
            identityErrors.AddRange((await _userManager.SetUserNameAsync(user, vm.UserName.Trim())).Errors);
        if (user.Email != vm.Email.Trim())
            identityErrors.AddRange((await _userManager.SetEmailAsync(user, vm.Email.Trim())).Errors);
        identityErrors.AddRange((await _userManager.UpdateAsync(user)).Errors);

        if (identityErrors.Count > 0)
        {
            foreach (var error in identityErrors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(vm);
        }

        // Role sync
        var targetRole = vm.Role == DbSeeder.AdminRole ? DbSeeder.AdminRole : DbSeeder.WorkerRole;
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(targetRole) || currentRoles.Count != 1)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, targetRole);
        }

        // Permission upsert (kept even for Admins in case they are demoted later)
        var permission = await _db.UserPermissions.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (permission is null)
        {
            permission = new UserPermission { UserId = user.Id };
            _db.UserPermissions.Add(permission);
        }
        vm.ApplyTo(permission);
        await _db.SaveChangesAsync();

        // Deactivation kills existing sessions (security stamp re-validation).
        if (wasActive && !user.IsActive)
            await _userManager.UpdateSecurityStampAsync(user);

        TempData["Success"] = $"User \"{user.UserName}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        if (id == CurrentUserId)
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        if (!user.IsActive)
            await _userManager.UpdateSecurityStampAsync(user);

        TempData["Success"] = $"User \"{user.UserName}\" {(user.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        return View(new ResetPasswordViewModel { Id = user.Id, UserName = user.UserName! });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var user = await _userManager.FindByIdAsync(vm.Id);
        if (user is null)
            return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, vm.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(vm);
        }

        TempData["Success"] = $"Password for \"{user.UserName}\" has been reset.";
        return RedirectToAction(nameof(Index));
    }
}
