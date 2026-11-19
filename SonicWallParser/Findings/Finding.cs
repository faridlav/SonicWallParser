namespace SonicWallParser.Findings;

/// <summary>How serious a finding is. Ordered from most to least serious.</summary>
public enum Severity
{
    Critical,
    High,
    Medium,
    Low,
    Info
}

/// <summary>One thing found in the configuration.</summary>
/// <param name="CheckId">The id of the check that produced it, for example "R001".</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Category">The check category, for example "Firewall rules".</param>
/// <param name="Title">A short statement of what was found.</param>
/// <param name="Detail">The specifics: names, values and why this item was flagged.</param>
/// <param name="Location">A readable place, for example "Rule 12 (Allow web)".</param>
/// <param name="LinkTo">The report anchor id of the item, for example "rule-12", or null.</param>
/// <param name="UnverifiedBasis">
/// True when the finding rests on a value mapping that is not confirmed against the SonicOS UI yet.
/// </param>
public sealed record Finding(
    string CheckId,
    Severity Severity,
    string Category,
    string Title,
    string Detail,
    string Location,
    string? LinkTo,
    bool UnverifiedBasis = false);

/// <summary>Static description of a check, used for the documentation and the report.</summary>
/// <param name="Id">Stable id, for example "R001".</param>
/// <param name="Category">The category it belongs to.</param>
/// <param name="Title">What the check looks for.</param>
/// <param name="DefaultSeverity">The usual severity, which some checks raise or lower per item.</param>
/// <param name="Why">Why it matters.</param>
/// <param name="Fix">The suggested remediation.</param>
public sealed record CheckInfo(string Id, string Category, string Title, Severity DefaultSeverity, string Why, string Fix);

/// <summary>A check that could not be assessed because the export does not carry the data.</summary>
/// <param name="CheckId">The check id, or a short code for checks that cannot be implemented from an export.</param>
/// <param name="Title">What would have been checked.</param>
/// <param name="Reason">Why it was not assessed.</param>
public sealed record NotAssessed(string CheckId, string Title, string Reason);

/// <summary>Tunable settings for the findings run.</summary>
public sealed class FindingOptions
{
    /// <summary>Check ids to skip, ignoring case.</summary>
    public HashSet<string> DisabledChecks { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A custom rule not changed for this many days is reported as stale (Info). Default two years.</summary>
    public int StaleRuleDays { get; set; } = 730;

    /// <summary>A phase 1 lifetime above this many seconds is reported as long. Default 24 hours.</summary>
    public int LongLifetimeSeconds { get; set; } = 86_400;

    /// <summary>A license expiring within this many days of the reference date is reported. Default 60.</summary>
    public int LicenseWarningDays { get; set; } = 60;

    /// <summary>A minimum admin password length below this is reported. Default 8.</summary>
    public int MinPasswordLength { get; set; } = 8;

    /// <summary>
    /// The date "now" means for age checks. When null, the most recent change time found in the export is used,
    /// so the same file always gives the same findings on any machine and on any day.
    /// </summary>
    public DateTime? ReferenceDate { get; set; }
}

/// <summary>All findings plus the bookkeeping the report needs.</summary>
public sealed class FindingsResult
{
    /// <summary>Findings, most serious first, then by check id and location.</summary>
    public required List<Finding> Findings { get; init; }

    /// <summary>The checks that ran (not disabled), whether or not they found anything.</summary>
    public required List<CheckInfo> ChecksRun { get; init; }

    /// <summary>Checks the export cannot support, with the reason.</summary>
    public required List<NotAssessed> NotAssessed { get; init; }

    /// <summary>The date used for age and expiry checks.</summary>
    public required DateTime ReferenceDate { get; init; }

    /// <summary>Count of findings at a severity.</summary>
    public int Count(Severity severity) => Findings.Count(f => f.Severity == severity);

    /// <summary>Categories where checks ran and found nothing.</summary>
    public IEnumerable<string> CleanCategories
        => ChecksRun.Select(c => c.Category).Distinct()
            .Where(cat => Findings.All(f => f.Category != cat))
            .OrderBy(c => c, StringComparer.Ordinal);
}
