namespace Zedex.Api.DTOs.Bills
{
    /// <summary>
    /// One line of a bill save (create or edit).
    /// <para>
    /// <b>Create</b> (POST /api/bills): BillItemId is ignored (must be null/0) — every
    /// line is a new item.
    /// </para>
    /// <para>
    /// <b>Edit</b> (PUT /api/bills/{id}): BillItemId determines the operation —
    /// null/0 = add a NEW line; &gt; 0 = UPDATE that existing line (must belong to the
    /// bill being edited, else the request is rejected). Existing invoice lines whose
    /// id is not present in the request are REMOVED.
    /// </para>
    /// </summary>
    public class BillItemUpdateDto
    {
        /// <summary>Id of an existing line to update; null/0 means add a new line.</summary>
        public int? BillItemId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? SizeFt { get; set; }
        public decimal? DiscountPercent { get; set; }
    }
}
