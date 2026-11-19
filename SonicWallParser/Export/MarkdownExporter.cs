using System.Text;
using SonicWallParser.Analysis;
using SonicWallParser.Models;
using SonicWallParser.Reporting;

namespace SonicWallParser.Export;

/// <summary>
/// Generates a GitHub-flavoured Markdown report from a report document. Every free-text cell is escaped, so a
/// pipe or line break in a name or comment cannot break a table.
/// </summary>
public static class MarkdownExporter
{
    /// <summary>
    /// Generates the Markdown report as a string from the given SonicWall configuration.
    /// </summary>
    public static string Generate(SonicWallConfig config)
        => Render(ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Markdown));

    /// <summary>Renders any report document (main, findings or migration) to Markdown.</summary>
    public static string Render(ReportDocument doc)
    {
        var sb = new StringBuilder(32_000);

        sb.AppendLine($"# {doc.Title}");
        sb.AppendLine();
        if (doc.Subtitle.Length > 0)
        {
            sb.AppendLine($"**{ReportText.MdCell(doc.Subtitle)}**");
            sb.AppendLine();
        }

        foreach (var line in doc.Meta)
            sb.AppendLine($"- {line}");
        if (doc.Meta.Count > 0)
            sb.AppendLine();

        foreach (var section in doc.Sections)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine();
            foreach (var block in section.Blocks)
                RenderBlock(sb, block);
        }

        return sb.ToString();
    }

    private static void RenderBlock(StringBuilder sb, Block block)
    {
        switch (block)
        {
            case TableBlock t:
                sb.AppendLine("| " + string.Join(" | ", t.Headers.Select(ReportText.MdCell)) + " |");
                sb.AppendLine("|" + string.Concat(t.Headers.Select(_ => "---|")));
                foreach (var row in t.Rows)
                    sb.AppendLine("| " + string.Join(" | ", row.Cells.Select(CellText)) + " |");
                sb.AppendLine();
                break;
            case KeyValueBlock kv:
                sb.AppendLine("| Parameter | Value |");
                sb.AppendLine("|---|---|");
                foreach (var item in kv.Items)
                    sb.AppendLine($"| {ReportText.MdCell(item.Key)} | {CellText(item.Value)} |");
                sb.AppendLine();
                break;
            case ParagraphBlock p:
                sb.AppendLine(p.IsWarning ? $"> **Warning:** {ReportText.MdCell(p.Text)}" : ReportText.MdCell(p.Text));
                sb.AppendLine();
                break;
            case SubHeadingBlock h:
                sb.AppendLine($"### {ReportText.MdCell(h.Text)}");
                sb.AppendLine();
                break;
        }
    }

    private static string CellText(Cell c)
        => c.Style == CellStyle.Checkbox ? "[ ]" : ReportText.MdCell(c.Text);
}
