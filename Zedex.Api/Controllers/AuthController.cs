using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs;
using Zedex.Api.Services;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Identity;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        AppDbContext db)
    {
        _userManager  = userManager;
        _tokenService = tokenService;
        _db           = db;
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    /// <summary>
    /// Authenticates with email + password.
    /// Returns a short-lived JWT access token and a long-lived refresh token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // 1. Find user by email
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        // 2. Check active status
        if (!user.IsActive)
            return Unauthorized(new { message = "Your account has been deactivated. Contact the administrator." });

        // 3. Validate password
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
            return Unauthorized(new { message = "Invalid email or password." });

        // 4. Issue tokens
        var result = await _tokenService.CreateTokensAsync(user.Id);

        // 5. Build allowed-modules string for the response (mirrors what's in the JWT)
        var modulesClaim = await BuildModulesClaimAsync(user.Id);

        return Ok(new AuthResponse
        {
            AccessToken    = result.AccessToken,
            RefreshToken   = result.RefreshToken,
            ExpiresIn      = result.ExpiresIn,
            RefreshExpires = result.RefreshExpires,
            User = new UserInfo
            {
                FullName       = user.FullName,
                Email          = user.Email!,
                AllowedModules = modulesClaim
            }
        });
    }

    // ── POST /api/auth/refresh ────────────────────────────────────────────────

    /// <summary>
    /// Exchanges a valid refresh token for a new access token + refresh token pair.
    /// The old refresh token is revoked on success (token rotation).
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _tokenService.RefreshAsync(request.RefreshToken);
        if (result is null)
            return Unauthorized(new { message = "Refresh token is invalid or has expired. Please log in again." });

        // Decode userId from the new token to fetch user info for the response.
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt     = handler.ReadJwtToken(result.AccessToken);
        var userId  = jwt.Subject;
        var user    = await _userManager.FindByIdAsync(userId);
        var modulesClaim = await BuildModulesClaimAsync(userId);

        return Ok(new AuthResponse
        {
            AccessToken    = result.AccessToken,
            RefreshToken   = result.RefreshToken,
            ExpiresIn      = result.ExpiresIn,
            RefreshExpires = result.RefreshExpires,
            User = new UserInfo
            {
                FullName       = user?.FullName ?? string.Empty,
                Email          = user?.Email    ?? string.Empty,
                AllowedModules = modulesClaim
            }
        });
    }

    // ── POST /api/auth/logout ─────────────────────────────────────────────────

    /// <summary>
    /// Revokes the provided refresh token on the server.
    /// The mobile app should also delete its locally stored tokens.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _tokenService.RevokeAsync(request.RefreshToken);
        return Ok(new { message = "Logged out successfully." });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> BuildModulesClaimAsync(string userId)
    {
        var permission = await _db.UserPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var allowed = permission is null
            ? Enum.GetValues<AppModule>()
            : Enum.GetValues<AppModule>().Where(permission.Has);

        return string.Join(",", allowed.Select(m => (int)m));
    }
}
