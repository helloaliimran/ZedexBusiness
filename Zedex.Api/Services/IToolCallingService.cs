using Zedex.Api.DTOs.Lookups;
using Zedex.Api.DTOs.Products;
using Zedex.Api.DTOs.Tools;

namespace Zedex.Api.Services;

/// <summary>
/// Shared business logic behind every AI tool-calling endpoint on <c>ToolCallingController</c>.
/// Kept separate from the mobile-app controllers (ProductsController, BillsController, ...) so
/// tool behaviour can diverge from the mobile app's without touching that code — for now the
/// two happen to overlap a lot (Standard bills only, no PVC).
/// </summary>
public interface IToolCallingService
{
    /// <summary>Same batch "contains" search used by the mobile Products screen.</summary>
    Task<List<ProductSearchGroupDto>> SearchProductsAsync(List<ProductSearchRequestDto> items);

    /// <summary>All master-data lookups (Colors, Gauges, Categories, Companies).</summary>
    Task<AllLookupsDto> GetLookupsAsync();

    /// <summary>Finds customers by name/phone "contains" match, for resolving a spoken/typed name to a CustomerId.</summary>
    Task<List<ToolCustomerLookupDto>> FindCustomersAsync(string? search);

    /// <summary>
    /// Creates a new draft bill (BillId null/0) or updates an existing draft (BillId set).
    /// Standard (non-PVC) products only; posted bills cannot be updated.
    /// </summary>
    Task<ToolSaveBillResult> SaveBillAsync(ToolBillRequestDto request);

    /// <summary>
    /// Looks up a single bill's full header + line items by BillId (numeric) or invoice
    /// number. Use this before an edit to see current lines (with their BillItemIds) and
    /// header fields. Standard (non-PVC) bills only.
    /// </summary>
    Task<ToolGetBillResult> GetBillAsync(string idOrInvoiceNumber);
}
