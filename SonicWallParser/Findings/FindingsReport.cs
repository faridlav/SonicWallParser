using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Reporting;

namespace SonicWallParser.Findings;

/// <summary>Builds the Security Findings report document from a findings result.</summary>
public static class FindingsReport
{
    /// <summary>Id of the summary section, used for links and tests.</summary>
    public const string SummaryId = "sec-findings-summary";

    /// <summary>Builds the findings page. Locations link to anchors in the main report when both are in one file.</summary>
    public static ReportDocument Build(SonicWallConfig config, AnalysisResult analysis, FindingsResult result)
    {
        var g = config.Global;
        var doc = new ReportDocument
        {
            Title = "SonicWall Security Findings",
            Subtitle = $"{g.ProductName}  |  Firmware {g.BuildNumber}  |  {g.Firmware.Description}",
        };
        doc.Meta.Add($"Report Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        doc.Meta.Add($"Parser version: {SonicWallParser.Reporting.ParserInfo.Version}");
        doc.Meta.Add($"Age and expiry checks use {result.ReferenceDate:yyyy-MM-dd} UTC, the newest change time in the export.");

        doc.Sections.Add(Summary(result));

        foreach (var category in result.Findings.Select(f => f.Category).Distinct().OrderBy(c => c, StringComparer.Ordinal))
        {
            var items = result.Findings.Where(f => f.Category == category).ToList();
            var section = new ReportSection { Id = $"sec-findings-{Slug(category)}", Title = category, Count = items.Count };
            section.Table(["Severity", "Check", "Finding", "Where", "Detail", "Basis"],
                items.Select(f => ReportBuilder.Row(null, SeverityCell(f.Severity), f.CheckId, new Cell(f.Title, CellStyle.Strong),
                    new Cell(f.Location, CellStyle.Normal, f.LinkTo), f.Detail, f.UnverifiedBasis ? "Unverified mapping" : "Confirmed")));

            var infos = result.ChecksRun.Where(c => c.Category == category && items.Any(f => f.CheckId == c.Id)).ToList();
            if (infos.Count > 0)
            {
                section.Heading("Why it matters and how to fix it");
                section.Table(["Check", "Why it matters", "Suggested fix"],
                    infos.Select(i => ReportBuilder.Row(null, new Cell(i.Id, CellStyle.Strong), i.Why, i.Fix)));
            }

            doc.Sections.Add(section);
        }

        if (result.NotAssessed.Count > 0)
        {
            var section = new ReportSection { Id = "sec-findings-notassessed", Title = "Not assessed", Count = result.NotAssessed.Count, Collapsed = true };
            section.Note("These checks could not be done from the export. They are listed so that a gap is never mistaken for a pass.");
            section.Table(["Id", "What was not checked", "Why"],
                result.NotAssessed.Select(n => ReportBuilder.Row(null, n.CheckId, n.Title, n.Reason)));
            doc.Sections.Add(section);
        }

        return doc;
    }

    private static ReportSection Summary(FindingsResult result)
    {
        var s = new ReportSection { Id = SummaryId, Title = "Summary", Count = result.Findings.Count };
        s.Table(["Severity", "Findings"],
            Enum.GetValues<Severity>().Select(sev => ReportBuilder.Row(null, SeverityCell(sev), result.Count(sev).ToString())));
        s.Note($"{result.ChecksRun.Count} checks ran. {result.NotAssessed.Count} item(s) could not be assessed from the export. " +
               "Hit counters reset on reboot, so zero-hit findings are for review only.");

        var clean = result.CleanCategories.ToList();
        if (clean.Count > 0)
            s.Note($"No findings in: {string.Join(", ", clean)}.");

        if (result.Findings.Any(f => f.UnverifiedBasis))
            s.Warning("Some findings rest on value mappings that are not confirmed against the SonicOS UI. They are marked Unverified mapping in the Basis column.");
        return s;
    }

    internal static Cell SeverityCell(Severity severity) => new(severity.ToString(), severity switch
    {
        Severity.Critical => CellStyle.Critical,
        Severity.High => CellStyle.High,
        Severity.Medium => CellStyle.Medium,
        Severity.Low => CellStyle.Low,
        _ => CellStyle.Info
    });

    private static string Slug(string text)
        => new string(text.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}
