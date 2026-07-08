using System.Security.Claims;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;

namespace Zedex.Application.Common;

public interface IPermissionService
{
    Task<bool> HasModuleAccessAsync(string userId, AppModule module);
    Task<UserPermission?> GetForUserAsync(string userId);
    /// <summary>Modules to show in navigation. Admins get all modules.</summary>
    Task<IReadOnlySet<AppModule>> GetVisibleModulesAsync(ClaimsPrincipal user);
}
