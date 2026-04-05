# SonicWall Configuration Parser

A .NET CLI tool that parses SonicWall firewall `.exp` configuration backup files and generates comprehensive security documentation in multiple formats.

## What It Does

SonicWall appliances export their configuration as `.exp` files (Base64-encoded, URL-encoded key-value pairs). This tool decodes that file and produces human-readable reports covering:

- **Device information** and firmware history
- **Zones** with security service mappings (GAV, IPS, App Control, DPI-SSL, CFS)
- **Interfaces** with IP addressing, VLAN tags, and management access settings
- **Address objects** and group memberships
- **Service objects** and group memberships
- **Firewall policies** (IPv4 and IPv6) with hit counts and action labels
- **NAT policies** (IPv4 and IPv6)
- **VPN policies** with full Phase 1 / Phase 2 IPSec crypto details
- **Users, user groups**, and group memberships
- **Schedule objects**
- **DHCP server scopes**
- **WAN load balancing** groups and members
- **Content filter (CFS) policies**
- **Bandwidth management objects**
- **SSLVPN / NetExtender profiles**
- **Unused rule audit** (firewall rules with zero hits)

## Output Formats

| Format | Description |
|--------|-------------|
| **Markdown** (`.md`) | Tabular documentation suitable for version control and wikis |
| **PDF** (`.pdf`) | Professional landscape report with color-coded tables and alternating row styles |
| **HTML** (`.html`) | Interactive single-file report with dark/light mode, collapsible sections, and global table search |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Build

```bash
dotnet build
```

## Usage

```bash
SonicWallParser <path-to-.exp-file> [output-directory]
```

**Arguments:**

| Argument | Required | Description |
|----------|----------|-------------|
| `<path>` | Yes | Path to the SonicWall `.exp` backup file |
| `[output-dir]` | No | Output directory (defaults to same folder as input) |

**Example:**

```bash
SonicWallParser firewall-backup.exp ./reports
```

This generates three files in `./reports`:
- `firewall-backup-report.md`
- `firewall-backup-report.pdf`
- `firewall-backup-report.html`

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Bad arguments |
| `2` | Parse error (bad format, corrupt data) |
| `3` | I/O error (permissions, disk full) |
| `4` | Export error (one or more exports failed) |
| `99` | Unhandled fatal exception |

## Project Structure

```
SonicWallParser/
├── Models/          # Data classes for all SonicWall configuration objects
├── Parsing/
│   ├── ExpFileParser.cs     # Decodes .exp files (Base64 → URL-decode → key-value)
│   └── TableExtractor.cs    # Extracts typed objects from flat key-value dictionary
├── Export/
│   ├── MarkdownExporter.cs  # Markdown report generator
│   ├── PdfExporter.cs       # PDF report generator (QuestPDF)
│   └── HtmlExporter.cs      # Interactive HTML report generator
└── Program.cs               # CLI entry point
```

## Dependencies

- [QuestPDF](https://www.questpdf.com/) — PDF generation (Community license)

## Disclaimer

This tool is provided strictly for parsing SonicWall configuration files that you own or have explicit authorization to access. You are solely responsible for ensuring you have the legal right to analyze any configuration data you process with this tool. The author assumes no liability for any misuse, unauthorized access, or damages resulting from the use of this software.

## License

This project is licensed under the [MIT License](LICENSE).
