using System.Text;

namespace SonicWallParser.Findings;

/// <summary>
/// Generates docs/checks.md from the check registry, so the documentation can never drift from the code.
/// A test compares the file in the repository with this output.
/// </summary>
public static class CheckDocs
{
    /// <summary>Renders the check catalog as Markdown.</summary>
    public static string Markdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Security checks");
        sb.AppendLine();
        sb.AppendLine("This file is generated from the check registry. Do not edit it by hand.");
        sb.AppendLine("Regenerate it with `SonicWallParser --list-checks > docs/checks.md`.");
        sb.AppendLine();
        sb.AppendLine("Each check has a stable id. Disable checks with `--skip-checks R004,R005`. Severity shown is the usual one;");
        sb.AppendLine("some checks raise or lower it per item (for example by zone or by how broad a rule is).");
        sb.AppendLine("Checks that depend on a value mapping not yet confirmed in the SonicOS UI are marked Unverified mapping in the report.");
        sb.AppendLine();

        foreach (var category in FindingsEngine.Catalog.Select(c => c.Category).Distinct())
        {
            sb.AppendLine($"## {category}");
            sb.AppendLine();
            foreach (var c in FindingsEngine.Catalog.Where(c => c.Category == category))
            {
                sb.AppendLine($"### {c.Id}: {c.Title}");
                sb.AppendLine();
                sb.AppendLine($"- **Usual severity:** {c.DefaultSeverity}");
                sb.AppendLine($"- **Why it matters:** {c.Why}");
                sb.AppendLine($"- **Suggested fix:** {c.Fix}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Not assessed");
        sb.AppendLine();
        sb.AppendLine("These cannot be judged from an export. The report lists them so a gap is never mistaken for a pass.");
        sb.AppendLine();
        sb.AppendLine("- Admin accounts, admin rights and per-account multi-factor (privilege masks are not decoded).");
        sb.AppendLine("- SNMP community strings (credentials, never read).");
        sb.AppendLine("- Known vulnerable or end-of-life firmware (needs a maintained lookup).");
        sb.AppendLine("- Certificate expiry.");
        sb.AppendLine("- Security profiles applied per rule (keys unconfirmed).");
        sb.AppendLine("- Flood protection and anti-spoofing.");
        sb.AppendLine("- IKE version in use and pre-shared key use.");
        sb.AppendLine("- Remote access VPN multi-factor and breadth of access.");
        sb.AppendLine("- High availability.");
        sb.AppendLine("- Intrusion prevention per zone on SonicOS 7.x (source unconfirmed).");
        return sb.ToString();
    }
}
