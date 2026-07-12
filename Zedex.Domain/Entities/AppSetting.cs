using Zedex.Domain.Common;

namespace Zedex.Domain.Entities;

/// <summary>Admin-configurable key/value application setting.</summary>
public class AppSetting : BaseEntity
{
    /// <summary>Well-known setting keys.</summary>
    public static class Keys
    {
        /// <summary>Gas kit price in Rs. per foot (PVC billing).
        /// Single kit: rate × length × qty; Double kit: rate × 2 × length × qty.</summary>
        public const string GasKitRatePerFt = "GasKitRatePerFt";

        /// <summary>Business/shop name shown as the heading on PVC invoice prints
        /// (both full and small). Falls back to "Zedex Business" when empty.</summary>
        public const string PvcPrintTitle = "PvcPrintTitle";
    }

    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
}
