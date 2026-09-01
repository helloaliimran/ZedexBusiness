using System.ComponentModel.DataAnnotations;
using Zedex.Api.DTOs.Bills;

namespace Zedex.Api.DTOs.Tools;

/// <summary>
/// Request body for the "create_or_update_bill" tool. Omit/zero <see cref="BillId"/> to
/// create a new draft bill; pass an existing id to edit that draft. Standard (non-PVC)
/// bills only — PVC bills are out of scope for tool calling for now.
/// Line semantics match <see cref="BillItemUpdateDto"/>: BillItemId null/0 = new line,
/// set = update that existing line; lines omitted from an update are removed.
/// </summary>
public class ToolBillRequestDto
{
    /// <summary>Null/0 = create a new bill. Otherwise the id of the draft bill to update.</summary>
    public int? BillId { get; set; }

    [Required]
    public int CustomerId { get; set; }

    public string? Remarks { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Add at least one item.")]
    public List<BillItemUpdateDto> Items { get; set; } = new();
}
