namespace Zedex.Api.Services;

/// <summary>Creates and validates JWT access + refresh token pairs.</summary>
public interface ITokenService
{
    /// <summary>Issues a new access token + refresh token for the given user.</summary>
    Task<TokenResult> CreateTokensAsync(string userId);

    /// <summary>
    /// Validates the raw refresh token. If valid, revokes the old token and
    /// issues a fresh pair (token rotation). Returns null if invalid or expired.
    /// </summary>
    Task<TokenResult?> RefreshAsync(string rawRefreshToken);

    /// <summary>Permanently revokes a refresh token (logout).</summary>
    Task<bool> RevokeAsync(string rawRefreshToken);
}

/// <summary>Result returned to the client after a successful auth operation.</summary>
public record TokenResult(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,          // Access token lifetime in seconds
    DateTime RefreshExpires    // UTC date/time when the refresh token expires
);
