using System.Text.Json;
using System.Text.Json.Serialization;
using SonicWallParser.Reporting;

namespace SonicWallParser.Export;

/// <summary>
/// Writes the full model, the analysis summary, the findings and the migration checklist as JSON for automation
/// and for tools such as Notion. The raw key and value dump is never included.
/// </summary>
public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Builds the JSON text for a report set.</summary>
    public static string Generate(ReportSet set)
    {
        var a = set.Analysis;
        var payload = new
        {
            parserVersion = ParserInfo.Version,
            generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            firmware = new { set.Config.Global.BuildNumber, generation = set.Config.Global.Firmware.Generation, support = set.Config.Global.Firmware.Support.ToString() },
            configuration = set.Config,
            analysis = new
            {
                ruleOrigins = a.RuleOrigins.ToDictionary(p => p.Key.ToString(), p => p.Value),
                unusedRules = a.UnusedRules.Select(r => new { r.Index, r.Name, r.SourceZone, r.DestZone }),
                unusedObjects = a.UnusedObjects,
                duplicateObjects = a.DuplicateObjects,
                danglingReferences = a.DanglingReferences.Select(d => d.Reference),
            },
            findings = set.Options.IncludeFindings
                ? new
                {
                    referenceDateUtc = set.Findings.ReferenceDate,
                    checksRun = set.Findings.ChecksRun.Select(c => c.Id),
                    items = (object)set.Findings.Findings,
                    notAssessed = set.Findings.NotAssessed,
                }
                : null,
            migrationChecklist = set.Options.IncludeMigration
                ? new { stats = set.MigrationStats, sections = Tables(set.MigrationDocument) }
                : null,
        };

        return JsonSerializer.Serialize(payload, Options);
    }

    /// <summary>Writes the JSON to a file.</summary>
    public static void Generate(ReportSet set, string path) => File.WriteAllText(path, Generate(set));

    /// <summary>Projects each table of a document into a list of objects keyed by column header.</summary>
    private static IEnumerable<object> Tables(ReportDocument doc)
        => doc.Sections.Select(s => new
        {
            id = s.Id,
            title = s.Title,
            tables = s.Blocks.OfType<TableBlock>().Select(t => t.Rows.Select(r =>
                t.Headers.Select((h, i) => (Header: h, Value: i < r.Cells.Count ? Display(r.Cells[i]) : string.Empty))
                    .Where(x => x.Header != "Done")
                    .ToDictionary(x => x.Header, x => x.Value))),
        });

    private static string Display(Cell c) => c.Style == CellStyle.Checkbox ? "[ ]" : c.Text;
}
