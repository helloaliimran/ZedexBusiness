using System.Security.Claims;
using Zedex.Domain.Enums;

namespace Zedex.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns true if the user's JWT contains the given AppModule
    /// in the "modules" claim (stored as comma-separated int values).
    /// </summary>
    public static bool HasModule(this ClaimsPrincipal user, AppModule module)
    {
        var raw = user.FindFirstValue("modules") ?? string.Empty;
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Contains(((int)module).ToString());
    }
}
