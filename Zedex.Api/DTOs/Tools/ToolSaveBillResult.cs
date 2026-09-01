namespace Zedex.Api.DTOs.Tools;

/// <summary>Outcome of <c>IToolCallingService.SaveBillAsync</c>.</summary>
public record ToolSaveBillResult(bool Success, List<string> Errors, ToolBillResultDto? Bill);
