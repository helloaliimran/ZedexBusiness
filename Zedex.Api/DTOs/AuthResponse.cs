namespace Zedex.Api.DTOs;

/// <summary>Returned on successful login or token refresh.</summary>
public class AuthResponse
{
    public string AccessToken  { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;

    /// <summary>Access token lifetime in seconds (e.g. 900 = 15 minutes).</summary>
    public int ExpiresIn { get; set; }

    /// <summary>UTC date/time when the refresh token expires.</summary>
    public DateTime RefreshExpires { get; set; }

    public UserInfo User { get; set; } = default!;
}

public class UserInfo
{
    public string FullName       { get; set; } = default!;
    public string Email          { get; set; } = default!;

    /// <summary>
    /// Comma-separated AppModule int values this user is allowed to access.
    /// Mobile app reads this to show/hide navigation items.
    /// Example: "1,3,4,6" → Dashboard, Stock, Billing, CustomerLedger.
    /// </summary>
    public string AllowedModules { get; set; } = default!;
}
