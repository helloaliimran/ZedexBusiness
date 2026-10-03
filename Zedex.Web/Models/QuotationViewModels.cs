using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;

namespace Zedex.Web.Models;

public class QuotationItemInput
{
    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(2000)]
    public string? Specification { get; set; }

    [Range(0, 99999999, ErrorMessage = "Qty must be 0 or more.")]
    public decimal? Quantity { get; set; }

    [Range(0, 999999999, ErrorMessage = "Rate must be 0 or more.")]
    public decimal? Rate { get; set; }

    /// <summary>True when the user left the whole row empty (it is dropped, not validated).</summary>
    public bool IsBlank => string.IsNullOrWhiteSpace(Description) && string.IsNullOrWhiteSpace(Specification)
                           && Quantity is null && Rate is null;

    public decimal Amount => Math.Round((Quantity ?? 0) * (Rate ?? 0), 2);
}

public class QuotationFormViewModel
{
    public int Id { get; set; }

    /// <summary>Display only — assigned by the system on first save, never posted back.</summary>
    public string? QuotationNumber { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime QuotationDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Enter the client name.")]
    [StringLength(200)]
    [Display(Name = "Client name")]
    public string ClientName { get; set; } = default!;

    [StringLength(30)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [StringLength(500)]
    [Display(Name = "Project address")]
    public string? ProjectAddress { get; set; }

    public List<QuotationItemInput> Items { get; set; } = new();

    public List<string?> Terms { get; set; } = new();

    // ---- Display only ----
    public string CompanyTitle { get; set; } = "";
    public string? HeaderDetails { get; set; }
    /// <summary>Settings defaults, for the "Reset to defaults" button.</summary>
    public List<string> DefaultTerms { get; set; } = new();
}

public class QuotationRowViewModel
{
    public int Id { get; set; }
    public string QuotationNumber { get; set; } = default!;
    public DateTime QuotationDate { get; set; }
    public string ClientName { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public string? ProjectAddress { get; set; }
    public int ItemCount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? CreatedBy { get; set; }
}

public class QuotationListViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public PagedResult<QuotationRowViewModel> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

public class QuotationLineViewModel
{
    public int No { get; set; }
    public string Description { get; set; } = default!;
    public string? Specification { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Everything needed to render a quotation (preview page, PDF and image).</summary>
public class QuotationDocumentViewModel
{
    public int Id { get; set; }
    public string CompanyTitle { get; set; } = default!;
    public string? HeaderDetails { get; set; }
    public string QuotationNumber { get; set; } = default!;
    public DateTime QuotationDate { get; set; }
    public string ClientName { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public string? ProjectAddress { get; set; }
    public List<QuotationLineViewModel> Lines { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public List<string> Terms { get; set; } = new();
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }

    /// <summary>File name without extension, e.g. "Quotation-QT-20261003-0001-Ahmed".</summary>
    public string FileBaseName
    {
        get
        {
            var client = new string(ClientName.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-').ToArray())
                .Trim().Replace(' ', '-');
            if (client.Length > 40)
                client = client[..40];
            return client.Length == 0 ? $"Quotation-{QuotationNumber}" : $"Quotation-{QuotationNumber}-{client}";
        }
    }
}
