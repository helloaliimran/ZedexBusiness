namespace Zedex.Domain.Entities;

/// <summary>
/// Stores hashed refresh tokens issued by the mobile API.
/// Intentionally does NOT extend BaseEntity — these rows are hard-deleted
/// on expiry/revocation and must not be affected by the global soft-delete filter.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>ASP.NET Identity user ID (string FK).</summary>
    public string UserId { get; set; } = default!;

    /// <summary>SHA-256 hex hash of the raw token sent to the client.
    /// The raw token is never stored — only its hash is kept here.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTime Expires { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedDate { get; set; }

    // ── Computed helpers (not persisted) ──────────────────────────────
    public bool IsExpired => DateTime.UtcNow >= Expires;
    public bool IsActive  => !IsRevoked && !IsExpired;
}
