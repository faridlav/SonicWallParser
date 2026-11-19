using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SonicWallParser.Analysis;
using SonicWallParser.Models;
using SonicWallParser.Reporting;

namespace SonicWallParser.Export;

/// <summary>
/// Generates a US Letter landscape PDF from a report document using QuestPDF: a cover page with a clickable
/// contents list, bookmarks for every section, 9 pt body text and no forced page breaks.
/// </summary>
public static class PdfExporter
{
    private const string HdrBg = "#1B2A4A";
    private const string HdrFg = "#FFFFFF";
    private const string RowAlt = "#F0F4FA";
    private const string AccentAllow = "#2E7D32";
    private const string AccentDeny = "#C62828";
    private const string SectionColor = "#1B2A4A";
    private const string SubSectionColor = "#2C5282";
    private const string MutedColor = "#666666";

    /// <summary>Body text size in points. The old 7 pt was too small to read comfortably.</summary>
    private const float BodySize = 9f;

    /// <summary>Heading text sizes in points.</summary>
    private const float SectionSize = 14f;

    /// <summary>Widest relative column weight, so one long column cannot squeeze the rest.</summary>
    private const int MaxColumnWeight = 40;

    /// <summary>
    /// Generates a PDF report at the specified path from the given SonicWall configuration.
    /// </summary>
    public static void Generate(SonicWallConfig config, string outputPath)
        => Generate(ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Pdf), outputPath);

    /// <summary>Writes any report document (main, findings or migration) as a PDF.</summary>
    public static void Generate(ReportDocument doc, string outputPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(BodySize).FontFamily("DejaVu Sans"));
                page.Content().Element(c => Cover(c, doc));
            });

            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(BodySize).FontFamily("DejaVu Sans"));

                page.Header().PaddingBottom(6).Column(col =>
                {
                    col.Item().Text(doc.Title).FontSize(11).Bold().FontColor(HdrBg);
                    col.Item().Text(doc.Subtitle).FontSize(8).FontColor(MutedColor);
                    col.Item().PaddingTop(3).LineHorizontal(1).LineColor("#CCCCCC");
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    foreach (var section in doc.Sections)
                        RenderSection(col, section);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(MutedColor));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(outputPath);
    }

    private static void Cover(IContainer container, ReportDocument doc)
    {
        container.Column(col =>
        {
            col.Spacing(6);
            col.Item().PaddingTop(40).Text(doc.Title).FontSize(30).Bold().FontColor(HdrBg);
            col.Item().Text(doc.Subtitle).FontSize(13).FontColor(SubSectionColor);
            foreach (var line in doc.Meta)
                col.Item().Text(line).FontSize(10).FontColor(MutedColor);

            col.Item().PaddingTop(24).Text("Contents").FontSize(16).Bold().FontColor(SectionColor);
            foreach (var section in doc.Sections)
            {
                col.Item().SectionLink(section.Id).Row(row =>
                {
                    row.RelativeItem().Text(section.Title).FontSize(10).FontColor(SubSectionColor);
                    row.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10).FontColor(MutedColor));
                        t.BeginPageNumberOfSection(section.Id);
                    });
                });
            }
        });
    }

    private static void RenderSection(ColumnDescriptor col, ReportSection section)
    {
        col.Item().EnsureSpace(80).Section(section.Id).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text(section.Title).FontSize(SectionSize).Bold().FontColor(SectionColor);
            if (section.Count >= 0)
                row.AutoItem().Text($"{section.Count}").FontSize(10).FontColor(MutedColor);
        });

        foreach (var block in section.Blocks)
        {
            switch (block)
            {
                case TableBlock t:
                    col.Item().Element(c => Table(c, t));
                    break;
                case KeyValueBlock kv:
                    col.Item().Element(c => Pairs(c, kv));
                    break;
                case ParagraphBlock p:
                    col.Item().Text(p.Text).FontSize(8).Italic().FontColor(p.IsWarning ? AccentDeny : MutedColor);
                    break;
                case SubHeadingBlock h:
                    col.Item().EnsureSpace(60).PaddingTop(4).Text(h.Text).FontSize(11).Bold().FontColor(SubSectionColor);
                    break;
            }
        }
    }

    private static void Table(IContainer container, TableBlock block)
    {
        var weights = ColumnWeights(block);
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                foreach (var w in weights)
                    c.RelativeColumn(w);
            });

            table.Header(h =>
            {
                foreach (var header in block.Headers)
                    h.Cell().Background(HdrBg).Padding(3).Text(header).FontSize(8).Bold().FontColor(HdrFg);
            });

            for (var i = 0; i < block.Rows.Count; i++)
            {
                var row = block.Rows[i];
                var background = i % 2 == 1 ? RowAlt : "#FFFFFF";
                foreach (var cell in row.Cells)
                    table.Cell().Background(background).Padding(3).Element(c => DrawCell(c, cell));
            }
        });
    }

    private static int[] ColumnWeights(TableBlock block)
    {
        var weights = new int[block.Headers.Count];
        for (var i = 0; i < weights.Length; i++)
        {
            var longest = block.Headers[i].Length;
            foreach (var row in block.Rows.Take(200))
                if (i < row.Cells.Count)
                    longest = Math.Max(longest, Math.Min(row.Cells[i].Text.Length, MaxColumnWeight * 2) / 2 + 2);
            weights[i] = Math.Clamp(longest, 4, MaxColumnWeight);
        }

        return weights;
    }

    private static void Pairs(IContainer container, KeyValueBlock block)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(5);
            });
            table.Header(h =>
            {
                h.Cell().Background(HdrBg).Padding(3).Text("Parameter").FontSize(8).Bold().FontColor(HdrFg);
                h.Cell().Background(HdrBg).Padding(3).Text("Value").FontSize(8).Bold().FontColor(HdrFg);
            });

            for (var i = 0; i < block.Items.Count; i++)
            {
                var background = i % 2 == 1 ? RowAlt : "#FFFFFF";
                var item = block.Items[i];
                table.Cell().Background(background).Padding(3).Text(item.Key).FontSize(BodySize).Bold();
                table.Cell().Background(background).Padding(3).Element(c => DrawCell(c, item.Value));
            }
        });
    }

    private static void DrawCell(IContainer container, Cell cell)
    {
        if (cell.Style == CellStyle.Checkbox)
        {
            container.Text("[   ]").FontSize(BodySize);
            return;
        }

        var text = container.Text(cell.Text).FontSize(BodySize);
        switch (cell.Style)
        {
            case CellStyle.Allow:
            case CellStyle.Yes:
                text.Bold().FontColor(AccentAllow);
                break;
            case CellStyle.Deny:
            case CellStyle.Critical:
            case CellStyle.High:
                text.Bold().FontColor(AccentDeny);
                break;
            case CellStyle.Medium:
                text.Bold().FontColor("#B45309");
                break;
            case CellStyle.Low:
                text.Bold().FontColor(SubSectionColor);
                break;
            case CellStyle.Strong:
                text.Bold();
                break;
            case CellStyle.Muted:
            case CellStyle.No:
            case CellStyle.Unknown:
            case CellStyle.Info:
                text.FontColor(MutedColor);
                break;
        }
    }
}
