namespace SonicWallParser.Models;

/// <summary>
/// Encapsulates the outcome of a parsing operation, carrying either a value on success or an error message on failure.
/// </summary>
public class ParseResult<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public List<string> Warnings { get; init; } = [];
    public List<ParseDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Creates a successful result containing the parsed value and optional warnings.
    /// </summary>
    public static ParseResult<T> Ok(
        T value,
        List<string>? warnings = null,
        List<ParseDiagnostic>? diagnostics = null)
        => new()
        {
            Success = true,
            Value = value,
            Warnings = warnings ?? diagnostics?.Select(d => d.Message).ToList() ?? [],
            Diagnostics = diagnostics ?? []
        };

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static ParseResult<T> Fail(string error, List<ParseDiagnostic>? diagnostics = null)
        => new()
        {
            Success = false,
            Error = error,
            Diagnostics = diagnostics ?? []
        };
}
