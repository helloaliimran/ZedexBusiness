using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Zedex.Web.Models;

namespace Zedex.Web.Services;

/// <summary>
/// Renders a quotation for the client with QuestPDF: an A4 PDF, or a single tall PNG
/// (same layout on a continuous page) that can be sent over WhatsApp etc.
/// </summary>
public static class QuotationDocument
{
    private const string Accent = "#1F3A5F";
    private const string Muted = "#6C757D";
    private const string Border = "#BFC5CC";

    public static byte[] ToPdf(QuotationDocumentViewModel q) =>
        Build(q, forImage: false).GeneratePdf();

    public static byte[] ToPng(QuotationDocumentViewModel q) =>
        Build(q, forImage: true)
            .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 150 })
            .First();

    private static IDocument Build(QuotationDocumentViewModel q, bool forImage) =>
        Document.Create(container => container.Page(page =>
        {
            if (forImage)
                page.ContinuousSize(PageSizes.A4.Width); // one page, as tall as the content
            else
                page.Size(PageSizes.A4);
            page.Margin(32);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Black));

            page.Header().Element(c => ComposeHeader(c, q));
            page.Content().PaddingTop(10).Element(c => ComposeContent(c, q));
            if (!forImage)
                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.Span($"{q.QuotationNumber} · Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
        }));

    private static void ComposeHeader(IContainer container, QuotationDocumentViewModel q)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(q.CompanyTitle).FontSize(22).Bold().FontColor(Accent);
            if (!string.IsNullOrWhiteSpace(q.HeaderDetails))
                col.Item().AlignCenter().Text(q.HeaderDetails.Trim()).FontSize(9).FontColor(Muted);
            col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Accent);
            col.Item().PaddingTop(6).AlignCenter().Text("QUOTATION").FontSize(13).SemiBold().LetterSpacing(0.2f);
        });
    }

    private static void ComposeContent(IContainer container, QuotationDocumentViewModel q)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            // ---- Client / quotation details ----
            col.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem(3).Border(0.75f).BorderColor(Border).Padding(8).Column(c =>
                {
                    c.Spacing(2);
                    c.Item().Text("QUOTATION FOR").FontSize(8).SemiBold().FontColor(Muted);
                    c.Item().Text(q.ClientName).FontSize(12).Bold();
                    if (!string.IsNullOrWhiteSpace(q.ContactNumber))
                        c.Item().Text(t => { t.Span("Contact: ").SemiBold(); t.Span(q.ContactNumber); });
                    if (!string.IsNullOrWhiteSpace(q.ProjectAddress))
                        c.Item().Text(t => { t.Span("Project address: ").SemiBold(); t.Span(q.ProjectAddress); });
                });
                row.RelativeItem(2).Border(0.75f).BorderColor(Border).Padding(8).Column(c =>
                {
                    c.Spacing(4);
                    InfoLine(c, "Quotation No.", q.QuotationNumber);
                    InfoLine(c, "Date", q.QuotationDate.ToString("dd MMM yyyy"));
                });
            });

            // ---- Scope and specification ----
            col.Item().Column(c =>
            {
                c.Item().PaddingBottom(4).Text("Scope and Specification").FontSize(11).SemiBold().FontColor(Accent);
                c.Item().Element(e => ComposeTable(e, q));
            });

            // ---- Terms ----
            if (q.Terms.Count > 0)
                col.Item().Column(c =>
                {
                    c.Spacing(3);
                    c.Item().PaddingBottom(2).Text("Terms and Conditions").FontSize(11).SemiBold().FontColor(Accent);
                    for (var i = 0; i < q.Terms.Count; i++)
                    {
                        var term = q.Terms[i];
                        var no = i + 1;
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(18).Text($"{no}.");
                            r.RelativeItem().Text(term);
                        });
                    }
                });

            // ---- Signature ----
            col.Item().PaddingTop(30).ShowEntire().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(190).Column(c =>
                {
                    c.Item().LineHorizontal(0.75f);
                    c.Item().PaddingTop(3).AlignCenter().Text("Authorized Signature").FontSize(9);
                    c.Item().AlignCenter().Text($"For {q.CompanyTitle}").FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static void InfoLine(ColumnDescriptor c, string label, string value)
    {
        c.Item().Row(r =>
        {
            r.ConstantItem(80).Text(label).FontColor(Muted);
            r.RelativeItem().Text(value).SemiBold();
        });
    }

    private static void ComposeTable(IContainer container, QuotationDocumentViewModel q)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(24);
                cols.RelativeColumn(3);
                cols.RelativeColumn(3);
                cols.ConstantColumn(48);
                cols.ConstantColumn(68);
                cols.ConstantColumn(80);
            });

            table.Header(h =>
            {
                HeaderCell(h.Cell(), "#", right: false);
                HeaderCell(h.Cell(), "Description", right: false);
                HeaderCell(h.Cell(), "Specification", right: false);
                HeaderCell(h.Cell(), "Qty", right: true);
                HeaderCell(h.Cell(), "Rate", right: true);
                HeaderCell(h.Cell(), "Amount", right: true);
            });

            foreach (var line in q.Lines)
            {
                var bg = line.No % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;
                BodyCell(table.Cell(), bg).Text(line.No.ToString());
                BodyCell(table.Cell(), bg).Text(line.Description);
                BodyCell(table.Cell(), bg).Text(line.Specification ?? "");
                BodyCell(table.Cell(), bg).AlignRight().Text(line.Quantity.ToString("#,0.###"));
                BodyCell(table.Cell(), bg).AlignRight().Text(line.Rate.ToString("N2"));
                BodyCell(table.Cell(), bg).AlignRight().Text(line.Amount.ToString("N2"));
            }

            table.Cell().ColumnSpan(5).Border(0.75f).BorderColor(Border).Background(Colors.Grey.Lighten3)
                .Padding(5).AlignRight().Text("Grand Total").Bold();
            table.Cell().Border(0.75f).BorderColor(Border).Background(Colors.Grey.Lighten3)
                .Padding(5).AlignRight().Text($"Rs. {q.GrandTotal:N2}").Bold();
        });
    }

    private static void HeaderCell(IContainer cell, string text, bool right)
    {
        var c = cell.Background(Accent).Border(0.75f).BorderColor(Accent).PaddingVertical(5).PaddingHorizontal(4);
        (right ? c.AlignRight() : c).Text(text).FontSize(9).SemiBold().FontColor(Colors.White);
    }

    private static IContainer BodyCell(IContainer cell, string background) =>
        cell.Border(0.75f).BorderColor(Border).Background(background).PaddingVertical(4).PaddingHorizontal(4);
}
