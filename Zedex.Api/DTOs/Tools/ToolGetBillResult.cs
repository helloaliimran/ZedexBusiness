namespace Zedex.Api.DTOs.Tools;

/// <summary>Outcome of <c>IToolCallingService.GetBillAsync</c>.</summary>
public record ToolGetBillResult(bool Success, string? Error, ToolBillDetailDto? Bill);
