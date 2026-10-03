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

    /// <summary>Company name centred at the top of quotations.</summary>
    [Display(Name = "Quotation company title")]
    [StringLength(200)]
    public string? QuotationTitle { get; set; }

    /// <summary>Optional line(s) under the quotation title (address, phone, email).</summary>
    [Display(Name = "Header details (address / phone)")]
    [StringLength(500)]
    public string? QuotationHeaderDetails { get; set; }

    /// <summary>One term per line; pre-filled on every new quotation.</summary>
    [Display(Name = "Default terms and conditions")]
    [StringLength(10000)]
    public string? QuotationDefaultTerms { get; set; }
}
