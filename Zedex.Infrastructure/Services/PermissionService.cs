using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db) => _db = db;

    public Task<UserPermission?> GetForUserAsync(string userId) =>
        _db.UserPermissions.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<bool> HasModuleAccessAsync(string userId, AppModule module)
    {
        var permission = await GetForUserAsync(userId);
        return permission?.Has(module) ?? false;
    }

    public async Task<IReadOnlySet<AppModule>> GetVisibleModulesAsync(ClaimsPrincipal user)
    {
        var all = Enum.GetValues<AppModule>();
        if (user.IsInRole("Admin"))
            return all.ToHashSet();

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return new HashSet<AppModule>();

        var permission = await GetForUserAsync(userId);
        if (permission is null)
            return new HashSet<AppModule>();

        return all.Where(permission.Has).ToHashSet();
    }
}
