using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Identity;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public TokenService(
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        _config     = config;
        _userManager = userManager;
        _db         = db;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<TokenResult> CreateTokensAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var accessToken = await BuildAccessTokenAsync(user);
        var (rawRefresh, refreshExpires) = await StoreNewRefreshTokenAsync(userId);

        return new TokenResult(
            AccessToken    : accessToken,
            RefreshToken   : rawRefresh,
            ExpiresIn      : AccessTokenExpiryMinutes * 60,
            RefreshExpires : refreshExpires);
    }

    public async Task<TokenResult?> RefreshAsync(string rawRefreshToken)
    {
        var hash   = Hash(rawRefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        // Reject if not found, already revoked, or expired.
        if (stored is null || !stored.IsActive) return null;

        // Token rotation — revoke the used token and issue a new pair.
        stored.IsRevoked = true;
        _db.RefreshTokens.Update(stored);

        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user is null || !user.IsActive) return null;

        var accessToken = await BuildAccessTokenAsync(user);
        var (newRaw, newExpires) = await StoreNewRefreshTokenAsync(stored.UserId);

        await _db.SaveChangesAsync();

        return new TokenResult(
            AccessToken    : accessToken,
            RefreshToken   : newRaw,
            ExpiresIn      : AccessTokenExpiryMinutes * 60,
            RefreshExpires : newExpires);
    }

    public async Task<bool> RevokeAsync(string rawRefreshToken)
    {
        var hash   = Hash(rawRefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (stored is null || stored.IsRevoked) return false;

        stored.IsRevoked = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> BuildAccessTokenAsync(ApplicationUser user)
    {
        var jwtSection = _config.GetSection("Jwt");
        var key        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Secret"]!));
        var creds      = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Determine allowed modules.
        // Admins (no UserPermission row) → all modules.
        // Workers → only their toggled modules.
        var permission = await _db.UserPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        var allowedModules = permission is null
            ? Enum.GetValues<AppModule>()                         // owner → everything
            : Enum.GetValues<AppModule>().Where(permission.Has);  // worker → filtered

        var modulesClaim = string.Join(",", allowedModules.Select(m => (int)m));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name,  user.FullName),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("modules", modulesClaim)
        };

        var token = new JwtSecurityToken(
            issuer            : jwtSection["Issuer"],
            audience          : jwtSection["Audience"],
            claims            : claims,
            notBefore         : DateTime.UtcNow,
            expires           : DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<(string raw, DateTime expires)> StoreNewRefreshTokenAsync(string userId)
    {
        var expires = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);
        var raw     = GenerateSecureToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId      = userId,
            TokenHash   = Hash(raw),
            Expires     = expires,
            IsRevoked   = false,
            CreatedDate = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return (raw, expires);
    }

    private int AccessTokenExpiryMinutes =>
        int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

    private int RefreshTokenExpiryDays =>
        int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "30");

    /// <summary>Generates a cryptographically random 64-byte base-64 string.</summary>
    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Returns the lowercase hex SHA-256 hash of the input string.</summary>
    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
