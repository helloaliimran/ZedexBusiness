using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zedex.Api.DTOs.Products;
using Zedex.Api.DTOs.Tools;
using Zedex.Api.Services;

namespace Zedex.Api.Controllers;

/// <summary>
/// One controller for every endpoint an AI tool-calling agent uses (mobile-independent
/// callers, e.g. the assistant that drafts bills over chat). Kept separate from the
/// mobile-app controllers (Products/Bills/Customers/Lookups) so their contracts can
/// diverge without touching this one. No authentication yet — every action is
/// [AllowAnonymous]; add auth once the calling app has a way to supply it.
/// Standard (non-PVC) bills only for now — PVC bills are out of scope for tools.
/// </summary>
[ApiController]
[Route("api/tools")]
[AllowAnonymous]
[Produces("application/json")]
public class ToolCallingController : ControllerBase
{
    private readonly IToolCallingService _tools;

    public ToolCallingController(IToolCallingService tools) => _tools = tools;

    // ── Tool: search_product ─────────────────────────────────────────────────

    /// <summary>
    /// Batch product search. Each row's ProductName is required; Color/Gauge/Category/
    /// Company are optional refinements. Case-insensitive "contains" match on every
    /// field. Results come back grouped by row, in submission order.
    /// </summary>
    [HttpPost("products/search")]
    [ProducesResponseType(typeof(List<ProductSearchGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchProduct([FromBody] List<ProductSearchRequestDto> items)
    {
        if (items is null || items.Count == 0)
            return BadRequest(new { message = "At least one search row is required." });

        return Ok(await _tools.SearchProductsAsync(items));
    }

    // ── Tool: lookup ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all product master-data — Colors, Gauges, Categories, Companies — each
    /// sorted by name. Use this to resolve/validate the refinement fields accepted by
    /// the product search tool.
    /// </summary>
    [HttpGet("lookups")]
    [ProducesResponseType(typeof(Zedex.Api.DTOs.Lookups.AllLookupsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup() => Ok(await _tools.GetLookupsAsync());

    // ── Tool: find_customer ──────────────────────────────────────────────────

    /// <summary>
    /// Finds customers by name/phone "contains" match (max 20, alphabetical). Call this
    /// when a bill is being created and no CustomerId is known yet — pass the name the
    /// user gave to resolve it to a CustomerId, or omit the search term to browse.
    /// </summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(List<ToolCustomerLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindCustomer([FromQuery] string? search) =>
        Ok(await _tools.FindCustomersAsync(search));

    // ── Tool: get_bill ────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches a single bill's full header and line items (with each line's BillItemId) by
    /// BillId or invoice number. Call this before editing a bill so the request to
    /// create_or_update_bill can reference existing BillItemIds correctly.
    /// </summary>
    [HttpGet("bills/{idOrInvoiceNumber}")]
    [ProducesResponseType(typeof(ToolBillDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBill(string idOrInvoiceNumber)
    {
        var result = await _tools.GetBillAsync(idOrInvoiceNumber);
        if (result.Success)
            return Ok(result.Bill);

        if (result.Error == "Bill not found.")
            return NotFound(new { message = result.Error });

        return BadRequest(new { message = result.Error });
    }

    // ── Tool: create_or_update_bill ──────────────────────────────────────────

    /// <summary>
    /// Creates a new draft bill (omit/zero BillId) or updates an existing draft
    /// (BillId set). Standard (non-PVC) products only. Line semantics: BillItemId
    /// null/0 adds a new line, set updates that line; on update, existing lines whose
    /// id is missing from the request are removed. Posted bills cannot be edited.
    /// </summary>
    [HttpPost("bills")]
    [ProducesResponseType(typeof(ToolBillResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOrUpdateBill([FromBody] ToolBillRequestDto request)
    {
        var result = await _tools.SaveBillAsync(request);
        if (result.Success)
            return Ok(result.Bill);

        if (result.Errors.Count == 1 && result.Errors[0] == "Bill not found.")
            return NotFound(new { message = result.Errors[0] });
        if (result.Errors.Count == 1 && result.Errors[0].Contains("posted"))
            return Conflict(new { message = result.Errors[0] });

        return BadRequest(new { errors = result.Errors });
    }
}
