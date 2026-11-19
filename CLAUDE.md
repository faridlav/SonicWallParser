\# SonicWallParser



.NET 10 CLI that parses SonicWall .exp configuration backups and produces Markdown, PDF and HTML reports.



\## Before you start any work

\- Read docs/SonicWallParser-HANDOFF.md first, then docs/SonicWallParser-project-checklist.md and docs/SonicOS-sample-findings.md.



\## Data safety rules

\- Never commit .exp files, anything in the samples folder, or any report generated from real samples.

\- Never force-add ignored files (no git add -f).

\- Never read, store or print secrets from an export (passwords, TOTP data, RADIUS, LDAP, AD secrets, VPN keys, PPPoE credentials).

\- Test fixtures must be synthetic and clearly fake. Use reserved address ranges and example.com.



\## Working rules

\- Work on feature branches, never directly on main.

\- Make meaningful commits with good descriptions.

\- Write tests with every change: unit, integration and end to end.

\- Do not guess enum mappings. Show Unknown(n) and mark it unverified until confirmed.

\- Do not use em dashes in documentation, comments or report text.

\- Keep GitHub Actions to a minimum.



\## Environment

\- Project targets net10.0. The machine has SDK 11.0.100 and runtime 10.0.15.

