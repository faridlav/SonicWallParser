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
    /// Parses the specified .exp file and returns a dictionary of configuration key-value pairs.
    /// </summary>
    public static ParseResult<Dictionary<string, string>> Parse(string filePath)
    {
        var warnings = new List<string>();

        if (!File.Exists(filePath))
            return ParseResult<Dictionary<string, string>>
                .Fail($"File not found: {filePath}");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            return ParseResult<Dictionary<string, string>>
                .Fail("File is empty (0 bytes).");

        if (fileInfo.Length > 100_000_000)
            return ParseResult<Dictionary<string, string>>
                .Fail($"File is suspiciously large ({fileInfo.Length:N0} bytes). " +
                      "SonicWall .exp files are typically under 5MB.");

        string rawContent;
        try
        {
            rawContent = File.ReadAllText(filePath).Trim();
        }
        catch (IOException ex)
        {
            return ParseResult<Dictionary<string, string>>
                .Fail($"Cannot read file: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ParseResult<Dictionary<string, string>>
                .Fail($"Access denied: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(rawContent))
            return ParseResult<Dictionary<string, string>>
                .Fail("File contains only whitespace.");

        var base64 = rawContent.TrimEnd('&');

        if (base64.Length > 0 && base64[0] == '\uFEFF')
        {
            base64 = base64[1..];
            warnings.Add("Stripped UTF-8 BOM from file start.");
        }

        var invalidChars = base64.Where(c =>
            !char.IsLetterOrDigit(c) && c != '+' && c != '/' && c != '=')
            .Distinct().ToArray();

        if (invalidChars.Length > 0)
        {
            return ParseResult<Dictionary<string, string>>
                .Fail($"File contains invalid Base64 characters: " +
                      $"{string.Join(", ", invalidChars.Select(c => $"'{c}' (0x{(int)c:X2})"))}. " +
                      "This may not be a SonicWall .exp file.");
        }

        byte[] decoded;
        try
        {
            var remainder = base64.Length % 4;
            if (remainder > 0)
            {
                base64 = base64.PadRight(base64.Length + (4 - remainder), '=');
                warnings.Add($"Added {4 - remainder} padding character(s) for Base64 alignment.");
            }

            decoded = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            return ParseResult<Dictionary<string, string>>
                .Fail($"Base64 decode failed: {ex.Message}. " +
                      "The file may be corrupted or not a valid .exp export.");
        }

        var queryString = System.Text.Encoding.UTF8.GetString(decoded);

        if (!queryString.Contains("buildNum=") && !queryString.Contains("shortProdName="))
        {
            return ParseResult<Dictionary<string, string>>
                .Fail("Decoded content does not contain expected SonicWall headers " +
                      "(buildNum, shortProdName). This may not be a valid .exp file.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var pairs = queryString.Split('&');
        int skipped = 0;

        foreach (var pair in pairs)
        {
            if (string.IsNullOrEmpty(pair))
            {
                skipped++;
                continue;
            }

            var eqIndex = pair.IndexOf('=');
            if (eqIndex < 0)
            {
                skipped++;
                continue;
            }

            var key = pair[..eqIndex];
            var value = HttpUtility.UrlDecode(pair[(eqIndex + 1)..]);

            if (result.ContainsKey(key))
            {
                warnings.Add($"Duplicate key '{key}' — last value wins.");
            }

            result[key] = value;
        }

        if (skipped > 0)
            warnings.Add($"Skipped {skipped} malformed or empty key-value pairs.");

        if (result.Count == 0)
            return ParseResult<Dictionary<string, string>>
                .Fail("Decoded content contained no valid key-value pairs.");

        return ParseResult<Dictionary<string, string>>.Ok(result, warnings);
    }
}
