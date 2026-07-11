namespace Zedex.Application.Common;

/// <summary>Tabular report export. Cells may be string/decimal/int/DateTime; formatting is applied per type.</summary>
public interface IReportExportService
{
    byte[] ToExcel(string title, string subtitle, string[] headers, IEnumerable<object?[]> rows);
    byte[] ToPdf(string title, string subtitle, string[] headers, IEnumerable<object?[]> rows);
}
