namespace SonicWallParser.Models;

/// <summary>
/// Structured warning or error emitted while decoding and extracting configuration data.
/// </summary>
public class ParseDiagnostic
{
    public ParseSeverity Severity { get; init; }
    public string Section { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int? LineNumber { get; init; }
    public int? EndLineNumber { get; init; }

    public string Location => LineNumber is null
        ? string.Empty
        : EndLineNumber is not null && EndLineNumber != LineNumber
            ? $"{LineNumber}-{EndLineNumber}"
            : LineNumber.Value.ToString();
}
