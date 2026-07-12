using System.ComponentModel.DataAnnotations;

namespace Zedex.Web.Models;

public class SettingsViewModel
{
    /// <summary>Gas kit price in Rs. per foot (PVC billing).</summary>
    [Display(Name = "Gas kit rate (Rs. per foot)")]
    [Range(0, 1_000_000, ErrorMessage = "Rate must be 0 or more.")]
    public decimal GasKitRatePerFt { get; set; }

    /// <summary>Heading printed on PVC invoices (full + small).</summary>
    [Display(Name = "PVC print title")]
    [StringLength(200)]
    public string? PvcPrintTitle { get; set; }
}
