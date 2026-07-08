using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Zedex.Application.Common;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Web.Authorization;

public class ModulePermissionRequirement : IAuthorizationRequirement
{
    public ModulePermissionRequirement(AppModule module) => Module = module;
    public AppModule Module { get; }
}

/// <summary>Admins pass automatically; Workers must hold the module toggle.</summary>
public class ModulePermissionHandler : AuthorizationHandler<ModulePermissionRequirement>
{
    private readonly IPermissionService _permissions;

    public ModulePermissionHandler(IPermissionService permissions) => _permissions = permissions;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ModulePermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        if (context.User.IsInRole(DbSeeder.AdminRole))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null && await _permissions.HasModuleAccessAsync(userId, requirement.Module))
            context.Succeed(requirement);
    }
}

public static class Policies
{
    public static string For(AppModule module) => $"Module:{module}";
}
