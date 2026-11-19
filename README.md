# SonicWall Configuration Parser

A .NET CLI tool that parses SonicWall firewall `.exp` configuration backup files and produces documentation, a security findings page and a migration checklist.

## What It Does

SonicWall appliances export their configuration as `.exp` files (Base64-encoded, URL-encoded key-value pairs). This tool decodes that file and produces:

- **A configuration report**: device and firmware, zones with security services, interfaces (including addressing mode), IPv4, IPv6 and MAC address objects, resolved address and service groups, schedules (decoded days and times), firewall rules (name, schedule, logging, origin), NAT, VPN policies (name, peer, state, crypto), users, DHCP, WAN load balancing, content filter, bandwidth objects, SSLVPN, device settings (management hardening, NTP, syslog, SNMP state, licenses), and object hygiene (unused, duplicate and dangling objects).
- **A security findings page**: 40 documented checks with severity, exact location, why it matters and a suggested fix. Checks that an export cannot support are listed as not assessed. See [docs/checks.md](docs/checks.md).
- **A migration checklist**: a printable, platform-neutral list of everything to rebuild on another firewall, with values pre-filled from the export, tick boxes, and a "Collect manually" flag for things an export cannot contain (keys, certificates, passwords, licenses).
- **A diff** between two exports, and a **batch mode** for a folder of exports.

The tool never converts a configuration to another brand. It only documents.

## Supported Versions

| SonicOS | Status |
|---------|--------|
| 6.x | Supported. Verified against real 6.5.4 exports. |
| 7.x | Supported. Verified against real 7.1 and 7.3 exports. |
| 8.x | Accepted but **unverified**: no real 8.x export has been tested. Reports say so. |
| 5.x and earlier, 9.x and later | Rejected with a clear message and exit code 5. No report is written. |

Other brands, the SonicOS CLI text export and tech support reports are not supported. The generation is read from `buildNum` and shown in every report.

## Output Formats

| Output | Files |
|--------|-------|
| **HTML** | One interactive file (dark and light mode, collapsible sections, table search) holding the executive summary, the report, the findings page and the migration checklist. Findings link to the exact row in the report. |
| **Markdown** | `<name>-report.md`, `<name>-findings.md`, `<name>-migration.md` |
| **PDF** | `<name>-report.pdf`, `<name>-findings.pdf`, `<name>-migration.pdf`. US Letter landscape, 9 pt text, cover page, clickable contents and bookmarks. |
| **JSON** | `<name>-report.json`: the model, analysis summary, findings and checklist for automation. The raw key dump is never included. |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later (a newer SDK builds the net10.0 target fine)

## Build and Test

```bash
dotnet build
dotnet test
```

## Usage

```bash
SonicWallParser <path-to-.exp-file> [output-directory] [options]
SonicWallParser diff <old.exp> <new.exp> [output-directory] [options]
SonicWallParser --batch <folder> [output-directory] [options]
```

| Option | Description |
|--------|-------------|
| `-f, --format <list>` | Comma separated: `md`, `pdf`, `html`, `json`. Default `md,pdf,html`. |
| `-n, --name <name>` | Output file base name. Default is the input file name. |
| `-q, --quiet` | Print only errors. |
| `--no-findings` | Leave out the security findings page. |
| `--no-migration` | Leave out the migration checklist. |
| `--skip-checks <ids>` | Skip checks by id, for example `R004,R005`. |
| `--redact` | Replace names, addresses, comments, users and serials in all outputs. Files get a `-redacted` suffix. |
| `--batch` | Process every `.exp` in a folder, one sub folder each, and write `batch-summary.md`. |
| `--list-checks` | Print the security checks as Markdown. |
| `--version`, `--help` | Version and usage. |

**Examples:**

```bash
SonicWallParser firewall-backup.exp ./reports
SonicWallParser firewall-backup.exp ./reports --format md,json --name client-a
SonicWallParser firewall-backup.exp ./share --redact
SonicWallParser diff 2025-backup.exp 2026-backup.exp ./changes
SonicWallParser --batch ./exports ./reports --format md
```

## Sharing Reports Safely

- **Never commit or share a raw `.exp` file.** It holds real addresses, names, users, serials and secrets.
- The parser never reads passwords, shared secrets, one-time-password data, VPN keys, SNMP community strings or PPPoE credentials, and no report contains them. A test enforces this.
- Reports still contain your network layout. Use `--redact` before sharing a report outside the client. Redaction is best effort: read the result first. See [docs/anonymization.md](docs/anonymization.md).

## Reading the Results Correctly

- **Hit counts reset** on reboot or when counters are cleared. A rule with zero hits is a reason to review it, not to delete it.
- **Rule origin** is derived: custom, auto-generated and built-in default. Only custom rules feed cleanup findings.
- **Unverified** marks values whose meaning has not been confirmed in the SonicOS UI (for example some crypto values, the rule action value 0, schedule time units). They are shown as `Unknown(n)` or labelled, never guessed.
- Objects created in bulk at the same instant are treated as built-in and are not listed as cleanup candidates. This is a heuristic.

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Bad arguments |
| `2` | Parse error (bad format, corrupt data) |
| `3` | I/O error (permissions, disk full) |
| `4` | Export error (one or more exports failed) |
| `5` | Unsupported SonicOS version |
| `6` | Batch mode: at least one file failed |
| `99` | Unhandled fatal exception |

## Project Structure

```
SonicWallParser/
├── Models/        Data classes for configuration objects, firmware and device settings
├── Parsing/       Decoding (.exp to key-values), secret deny-list, table extraction
├── Analysis/      Name resolver, reference graph, rule origin and the unused-rule audit
├── Findings/      The check registry, findings engine and findings report
├── Migration/     The migration checklist
├── Reporting/     Shared report model, builders and the report set
├── Export/        Markdown, HTML, PDF and JSON renderers
├── Redaction/     Redaction mode
├── Diff/          Comparison of two exports
├── Cli/           Command line parsing and the application
└── Program.cs     Entry point
```

## Contributing Samples

More real exports make parsing better, especially SonicOS 6.0 to 6.4, 8.x, other models (NSa, NSsp) and HA pairs. Anonymize first (see [docs/anonymization.md](docs/anonymization.md)) and never attach a raw export to a public issue. Test fixtures in this repository are hand-written and clearly fake (reserved address ranges and example.com).

## Dependencies

- [QuestPDF](https://www.questpdf.com/) for PDF generation (Community license; check the terms for your organization)

## Disclaimer

This tool is provided strictly for parsing SonicWall configuration files that you own or have explicit authorization to access. You are solely responsible for ensuring you have the legal right to analyze any configuration data you process with this tool. The author assumes no liability for any misuse, unauthorized access, or damages resulting from the use of this software.

## License

This project is licensed under the [MIT License](LICENSE).
