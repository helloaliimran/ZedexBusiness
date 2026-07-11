using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Zedex.Application.Common;

namespace Zedex.Infrastructure.Services;

public class ReportExportService : IReportExportService
{
    public byte[] ToExcel(string title, string subtitle, string[] headers, IEnumerable<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheetName = title.Length > 31 ? title[..31] : title;
        var ws = workbook.Worksheets.Add(sheetName);

        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Range(1, 1, 1, headers.Length).Merge();

        ws.Cell(2, 1).Value = subtitle;
        ws.Cell(2, 1).Style.Font.SetFontColor(XLColor.Gray);
        ws.Range(2, 1, 2, headers.Length).Merge();

        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(4, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E9ECEF"));
        }

        var r = 5;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
                SetCell(ws.Cell(r, c + 1), row[c]);
            r++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ToPdf(string title, string subtitle, string[] headers, IEnumerable<object?[]> rows)
    {
        var rowList = rows.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(headers.Length > 5 ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(15).SemiBold();
                    col.Item().Text(subtitle).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(6);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        foreach (var _ in headers)
                            cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                            header.Cell().Background(Colors.Grey.Lighten3)
                                .BorderBottom(1).BorderColor(Colors.Grey.Medium)
                                .Padding(4).Text(h).SemiBold();
                    });

                    foreach (var row in rowList)
                    {
                        foreach (var cell in row)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4).Text(Format(cell));
                        }
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm} — Zedex Business")
                        .FontColor(Colors.Grey.Darken1).FontSize(8);
                    row.ConstantItem(80).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case decimal d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case int i:
                cell.Value = i;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd mmm yyyy hh:mm";
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static string Format(object? value) => value switch
    {
        null => "",
        decimal d => d.ToString("N2"),
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd MMM yyyy") : dt.ToString("dd MMM yyyy HH:mm"),
        _ => value.ToString() ?? ""
    };
}
