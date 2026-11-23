using SonicWallParser.Reporting;

namespace SonicWallParser.Diff;

/// <summary>Builds the report document for a configuration diff.</summary>
public static class DiffReport
{
    /// <summary>Builds a summary section and one section per category that changed.</summary>
    public static ReportDocument Build(DiffResult diff)
    {
        var doc = new ReportDocument
        {
            Title = "SonicWall Configuration Changes",
            Subtitle = $"{diff.OldLabel}  to  {diff.NewLabel}",
        };
        doc.Meta.Add($"Report Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        doc.Meta.Add($"Parser version: {ParserInfo.Version}");

        var summary = new ReportSection { Id = "sec-diff-summary", Title = "Change summary", Count = diff.Changes.Count };
        summary.Table(["Category", "Added", "Removed", "Changed"],
            diff.CategoriesCompared.Select(c => ReportBuilder.Row(null, new Cell(c, CellStyle.Strong),
                diff.Count(ChangeKind.Added, c).ToString(), diff.Count(ChangeKind.Removed, c).ToString(), diff.Count(ChangeKind.Changed, c).ToString())));
        summary.Note(diff.Changes.Count == 0
            ? "No differences were found between the two exports in the compared categories."
            : $"{diff.Count(ChangeKind.Added)} added, {diff.Count(ChangeKind.Removed)} removed and {diff.Count(ChangeKind.Changed)} changed. " +
              "Items are matched by name, or by their full match definition for rules and NAT, so reordering alone is not a change. " +
              "Hit counters and timestamps are not compared.");
        doc.Sections.Add(summary);

        foreach (var category in diff.CategoriesCompared.Where(c => diff.Changes.Any(x => x.Category == c)))
        {
            var items = diff.Changes.Where(c => c.Category == category).OrderBy(c => c.Kind).ThenBy(c => c.Item, StringComparer.OrdinalIgnoreCase).ToList();
            var section = new ReportSection { Id = $"sec-diff-{Slug(category)}", Title = category, Count = items.Count };
            section.Table(["Change", "Item", "Details"],
                items.Select(c => ReportBuilder.Row(null,
                    new Cell(c.Kind.ToString(), c.Kind switch { ChangeKind.Added => CellStyle.Yes, ChangeKind.Removed => CellStyle.Deny, _ => CellStyle.Medium }),
                    new Cell(c.Item, CellStyle.Strong), c.Detail)));
            doc.Sections.Add(section);
        }

        return doc;
    }

    private static string Slug(string text)
        => new string(text.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}
