namespace SonicWallParser.Reporting;

/// <summary>How a table cell is emphasised. Each renderer maps these to its own look.</summary>
public enum CellStyle
{
    Normal,
    Strong,
    Code,
    Muted,
    Allow,
    Deny,
    Yes,
    No,
    Unknown,
    Critical,
    High,
    Medium,
    Low,
    Info,

    /// <summary>An empty tick box for a technician to fill in (migration checklist).</summary>
    Checkbox
}

/// <summary>One table cell: text, an emphasis and an optional link to a row id elsewhere in the report.</summary>
/// <param name="Text">The visible text (never markup).</param>
/// <param name="Style">Emphasis.</param>
/// <param name="LinkTo">The id of a row or heading this cell links to, or null.</param>
public readonly record struct Cell(string Text, CellStyle Style = CellStyle.Normal, string? LinkTo = null)
{
    /// <summary>Plain text converts to a normal cell.</summary>
    public static implicit operator Cell(string text) => new(text);
}

/// <summary>One table row. <paramref name="Id"/> is an anchor that other cells can link to.</summary>
public sealed record TableRow(IReadOnlyList<Cell> Cells, string? Id = null);

/// <summary>A piece of content inside a section.</summary>
public abstract record Block;

/// <summary>A table with a header row.</summary>
public sealed record TableBlock(IReadOnlyList<string> Headers, IReadOnlyList<TableRow> Rows) : Block;

/// <summary>One label and value pair.</summary>
public readonly record struct KeyValue(string Key, Cell Value);

/// <summary>A list of label and value pairs, shown as a two column table or an info grid.</summary>
public sealed record KeyValueBlock(IReadOnlyList<KeyValue> Items) : Block;

/// <summary>A paragraph. A note is smaller and muted; a warning is emphasised.</summary>
public sealed record ParagraphBlock(string Text, bool IsNote = false, bool IsWarning = false) : Block;

/// <summary>A heading inside a section, optionally an anchor target.</summary>
public sealed record SubHeadingBlock(string Text, string? Id = null) : Block;

/// <summary>A titled part of a report.</summary>
public sealed class ReportSection
{
    /// <summary>Anchor id, for example "sec-zones".</summary>
    public required string Id { get; init; }

    /// <summary>The heading.</summary>
    public required string Title { get; init; }

    /// <summary>An item count shown as a badge, or -1 for none.</summary>
    public int Count { get; init; } = -1;

    /// <summary>Start collapsed in interactive output.</summary>
    public bool Collapsed { get; init; }

    /// <summary>The content.</summary>
    public List<Block> Blocks { get; } = [];

    /// <summary>Adds a table.</summary>
    public ReportSection Table(string[] headers, IEnumerable<TableRow> rows)
    {
        Blocks.Add(new TableBlock(headers, rows.ToList()));
        return this;
    }

    /// <summary>Adds label and value pairs.</summary>
    public ReportSection Pairs(params (string Key, Cell Value)[] items)
    {
        Blocks.Add(new KeyValueBlock(items.Select(i => new KeyValue(i.Key, i.Value)).ToList()));
        return this;
    }

    /// <summary>Adds a muted note.</summary>
    public ReportSection Note(string text)
    {
        Blocks.Add(new ParagraphBlock(text, IsNote: true));
        return this;
    }

    /// <summary>Adds an emphasised warning paragraph.</summary>
    public ReportSection Warning(string text)
    {
        Blocks.Add(new ParagraphBlock(text, IsWarning: true));
        return this;
    }

    /// <summary>Adds a heading, optionally an anchor target.</summary>
    public ReportSection Heading(string text, string? id = null)
    {
        Blocks.Add(new SubHeadingBlock(text, id));
        return this;
    }
}

/// <summary>A complete report, built once and drawn by any renderer.</summary>
public sealed class ReportDocument
{
    /// <summary>The report title.</summary>
    public required string Title { get; init; }

    /// <summary>A one line description, usually device and firmware.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Extra header lines such as the generation time.</summary>
    public List<string> Meta { get; } = [];

    /// <summary>The sections in order.</summary>
    public List<ReportSection> Sections { get; } = [];
}
