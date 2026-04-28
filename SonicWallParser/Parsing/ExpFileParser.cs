using System.Web;
using SonicWallParser.Models;

namespace SonicWallParser.Parsing;

/// <summary>
/// Decodes a SonicWall .exp backup file by stripping terminators, Base64-decoding, URL-decoding,
/// and splitting the result into a flat key-value dictionary.
/// </summary>
public static class ExpFileParser
{
    /// <summary>
    /// Parses the specified .exp file and returns decoded configuration key-value pairs with diagnostics.
    /// </summary>
    public static ParseResult<ParsedSettings> Parse(string filePath)
    {
        var diagnostics = new List<ParseDiagnostic>();

        if (!File.Exists(filePath))
            return Fail($"File not found: {filePath}", diagnostics, "File", ParseSeverity.Error);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            return Fail("File is empty (0 bytes).", diagnostics, "File", ParseSeverity.Error);

        if (fileInfo.Length > 100_000_000)
            return Fail($"File is suspiciously large ({fileInfo.Length:N0} bytes). " +
                        "SonicWall .exp files are typically under 5MB.",
                diagnostics, "File", ParseSeverity.Error);

        string rawContent;
        try
        {
            rawContent = File.ReadAllText(filePath).Trim();
        }
        catch (IOException ex)
        {
            return Fail($"Cannot read file: {ex.Message}", diagnostics, "File", ParseSeverity.Error);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Fail($"Access denied: {ex.Message}", diagnostics, "File", ParseSeverity.Error);
        }

        if (string.IsNullOrWhiteSpace(rawContent))
            return Fail("File contains only whitespace.", diagnostics, "File", ParseSeverity.Error);

        var base64 = rawContent.TrimEnd('&');

        if (base64.Length > 0 && base64[0] == '\uFEFF')
        {
            base64 = base64[1..];
            Add(diagnostics, ParseSeverity.Info, "File", "Stripped UTF-8 BOM from file start.", 1);
        }

        var compactBase64 = new string(base64.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (compactBase64.Length != base64.Length)
            Add(diagnostics, ParseSeverity.Info, "File", "Ignored whitespace in Base64 payload.");

        var invalidChars = compactBase64.Where(c =>
            !char.IsLetterOrDigit(c) && c != '+' && c != '/' && c != '=')
            .Distinct().ToArray();

        if (invalidChars.Length > 0)
        {
            if (LooksLikeDecodedSettings(rawContent))
            {
                Add(diagnostics, ParseSeverity.Info, "File",
                    "Input appears to be an already-decoded settings file; parsing as plain key/value text.");
                return ParseDecodedText(rawContent, diagnostics);
            }

            if (LooksLikeCliExport(rawContent))
                return Fail("Input looks like a SonicOS CLI text export, not a SonicOS .exp settings export. " +
                            "CLI command-format imports are not parsed yet.",
                    diagnostics, "File", ParseSeverity.Error);

            return Fail($"File contains invalid Base64 characters: " +
                        $"{string.Join(", ", invalidChars.Select(c => $"'{c}' (0x{(int)c:X2})"))}. " +
                        "This may not be a SonicWall .exp file.",
                diagnostics, "File", ParseSeverity.Error);
        }

        byte[] decoded;
        try
        {
            var remainder = compactBase64.Length % 4;
            if (remainder > 0)
            {
                compactBase64 = compactBase64.PadRight(compactBase64.Length + (4 - remainder), '=');
                Add(diagnostics, ParseSeverity.Info, "File",
                    $"Added {4 - remainder} padding character(s) for Base64 alignment.");
            }

            decoded = Convert.FromBase64String(compactBase64);
        }
        catch (FormatException ex)
        {
            return Fail($"Base64 decode failed: {ex.Message}. " +
                        "The file may be corrupted or not a valid .exp export.",
                diagnostics, "File", ParseSeverity.Error);
        }

        var queryString = System.Text.Encoding.UTF8.GetString(decoded);
        return ParseDecodedText(queryString, diagnostics);
    }

    /// <summary>
    /// Parses already-decoded SonicWall key/value text. Useful for tests and locally decoded exports.
    /// </summary>
    public static ParseResult<ParsedSettings> ParseDecodedText(
        string queryString,
        List<ParseDiagnostic>? existingDiagnostics = null)
    {
        var diagnostics = existingDiagnostics ?? [];

        if (!queryString.Contains("buildNum=") && !queryString.Contains("shortProdName="))
        {
            if (LooksLikeCliExport(queryString))
                return Fail("Decoded content looks like a SonicOS CLI text export, not a SonicOS .exp settings export. " +
                            "CLI command-format imports are not parsed yet.",
                    diagnostics, "File", ParseSeverity.Error);

            Add(diagnostics, ParseSeverity.Warning, "Global",
                "Decoded content does not contain expected SonicWall headers (buildNum, shortProdName). " +
                "Continuing with partial settings because recognizable key/value pairs may still exist.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var lineNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var pairs = SplitPairs(queryString);

        foreach (var (pair, lineNumber) in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair))
                continue;

            var eqIndex = pair.IndexOf('=');
            if (eqIndex < 0)
            {
                Add(diagnostics, ParseSeverity.Warning, "KeyValue",
                    $"Skipped malformed key/value pair '{TrimForMessage(pair)}' because it does not contain '='.",
                    lineNumber);
                continue;
            }

            var key = pair[..eqIndex];
            if (string.IsNullOrWhiteSpace(key))
            {
                Add(diagnostics, ParseSeverity.Warning, "KeyValue",
                    "Skipped malformed key/value pair because the key is empty.",
                    lineNumber);
                continue;
            }

            string value;
            try
            {
                value = HttpUtility.UrlDecode(pair[(eqIndex + 1)..]);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is UriFormatException)
            {
                Add(diagnostics, ParseSeverity.Warning, "KeyValue",
                    $"URL decode failed for key '{key}': {ex.Message}. Using raw value.",
                    lineNumber);
                value = pair[(eqIndex + 1)..];
            }

            if (result.ContainsKey(key))
            {
                Add(diagnostics, ParseSeverity.Warning, InferSection(key),
                    $"Duplicate key '{key}' encountered; last value wins.",
                    lineNumber);
            }

            result[key] = value;
            lineNumbers[key] = lineNumber;
        }

        if (result.Count == 0)
            return Fail("Decoded content contained no valid key-value pairs.",
                diagnostics, "KeyValue", ParseSeverity.Error);

        var settings = new ParsedSettings
        {
            Values = result,
            KeyLineNumbers = lineNumbers,
            Diagnostics = diagnostics
        };

        return ParseResult<ParsedSettings>.Ok(settings, diagnostics: diagnostics);
    }

    private static List<(string Pair, int LineNumber)> SplitPairs(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var result = new List<(string Pair, int LineNumber)>();
        var line = 1;
        var start = 0;

        for (var i = 0; i <= normalized.Length; i++)
        {
            if (i < normalized.Length && normalized[i] != '&' && normalized[i] != '\n')
                continue;

            result.Add((normalized[start..i], line));

            if (i < normalized.Length && normalized[i] == '\n')
                line++;

            start = i + 1;
        }

        return result;
    }

    private static bool LooksLikeDecodedSettings(string text)
        => text.Contains('=') && (text.Contains('&') || text.Contains('\n'));

    private static bool LooksLikeCliExport(string text)
        => text.Contains("configure", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("access-rule", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("nat-policy", StringComparison.OrdinalIgnoreCase);

    private static ParseResult<ParsedSettings> Fail(
        string message,
        List<ParseDiagnostic> diagnostics,
        string section,
        ParseSeverity severity)
    {
        Add(diagnostics, severity, section, message);
        return ParseResult<ParsedSettings>.Fail(message, diagnostics);
    }

    private static void Add(
        List<ParseDiagnostic> diagnostics,
        ParseSeverity severity,
        string section,
        string message,
        int? lineNumber = null)
    {
        diagnostics.Add(new ParseDiagnostic
        {
            Severity = severity,
            Section = section,
            Message = message,
            LineNumber = lineNumber
        });
    }

    private static string InferSection(string key)
    {
        var index = key.IndexOf('_');
        return index > 0 ? key[..index] : "Global";
    }

    private static string TrimForMessage(string value)
        => value.Length <= 80 ? value : value[..77] + "...";
}
