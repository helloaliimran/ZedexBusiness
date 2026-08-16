using System.Security.Claims;
using Zedex.Application.Common;

namespace Zedex.Api.Services;

/// <summary>
/// ICurrentUserService implementation for the API layer.
/// Reads identity from the JWT claims set by JwtBearer middleware
/// (instead of the cookie-based implementation in Zedex.Web).
/// </summary>
public class ApiCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public ApiCurrentUserService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string? UserId =>
        _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _http.HttpContext?.User.FindFirstValue("sub");

    public string? UserName =>
        _http.HttpContext?.User.FindFirstValue("name")
        ?? _http.HttpContext?.User.FindFirstValue(ClaimTypes.Name);
}
