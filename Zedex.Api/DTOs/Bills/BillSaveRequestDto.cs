using System.ComponentModel.DataAnnotations;

namespace Zedex.Api.DTOs.Bills;

/// <summary>
/// Request body for both POST /api/bills (create) and PUT /api/bills/{id} (edit).
/// A bill is either entirely Standard or entirely PVC — this is decided automatically
/// from the products referenced by Items (a PVC-category product makes the whole bill
/// PVC); mixing the two in one bill is rejected. This only ever produces/updates a
/// draft — it never touches stock or the customer ledger. Posting (which does) is a
/// separate step, not covered by this endpoint.
///
/// Each <see cref="BillItemUpdateDto"/> line is either a NEW line (BillItemId null/0)
/// or an EXISTING line to update (BillItemId set to the line's id from a prior GET).
/// On edit, existing invoice lines whose id is not in <c>Items</c> are removed.
/// </summary>
public class BillSaveRequestDto
{
    [Required]
    public int CustomerId { get; set; }

    public string? Remarks { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Add at least one item.")]
    public List<BillItemUpdateDto> Items { get; set; } = new();
}
