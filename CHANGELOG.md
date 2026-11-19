# Changelog

## 2.0.0-preview (unreleased)

A large rework of correctness, coverage and output. See docs/WORKLOG.md for the detailed record.

### Added

- Security findings page with 40 documented checks (docs/checks.md), severities, locations linked into the report, a not-assessed list, per-check skip and thresholds.
- Platform-neutral migration checklist with tick boxes, pre-filled values and "Collect manually" flags.
- Executive summary at the top of the report.
- SonicOS version detection from the build number. 6.x and 7.x supported, 8.x accepted but unverified, anything else rejected (exit code 5).
- CLI options: `--format` (md, pdf, html, json), `--name`, `--quiet`, `--no-findings`, `--no-migration`, `--skip-checks`, `--redact`, `--list-checks`, `--version`.
- `diff` command comparing two exports, and `--batch` mode for a folder.
- JSON export of the model, analysis, findings and checklist.
- Redaction mode for sharing reports.
- Name resolver for nested groups, reference graph, unused, duplicate and dangling object detection.
- New data: rule name, schedule, logging, users, connection limits, bandwidth, GeoIP and botnet flags, IPv6 and MAC address objects, VPN name, peer gateways and state, decoded schedules, interface addressing mode, device settings, licenses.
- Hard deny-list for secret-bearing keys, enforced before any model is built, with tests.
- PDF: US Letter landscape, larger text, cover page, clickable contents, bookmarks.

### Fixed

- Serial number was always empty (wrong key).
- DHCP scopes were always empty (wrong keys).
- Rule "default" detection was wrong in every version. Rules now have a derived origin and one shared unused-rule definition used by every output.
- Zone Guest flag was never read. Zone IPS showed a false "off" on 7.x and now shows Unknown.
- Interface link speed read a key that does not exist.
- NAT hit count column was always zero and was removed.
- Timestamps depended on the machine's time zone and are now UTC.
- Markdown tables broke on a pipe in a name or comment.
- Index gaps created blank objects. Missing entries are now skipped with a warning.
- Unparsed keys were hidden behind a coarse prefix list. Keys actually read are now tracked.
- CLI text export detection rejected any file containing the word "commit" or "configure".
- Core sections that came back empty were a quiet zero. They now warn loudly in the CLI and every report.
- Rule action 0 was labelled Discard. It is now "Deny (0)" and marked unverified. Value 1 shows as Unknown(1).
- Crypto values without a confirmed mapping show `Unknown(n)` instead of a guessed name.
- The extractor no longer keeps static state or writes to the console.

### Known gaps (unverified or not read)

- Action value 0, IKEv2 values, default site-to-site crypto values, zone IPS on 7.x, DHCP lease unit, schedule time units, link ability bits and the meaning of `ipsecSaDisabled` and `userObjGuestEnable` are not confirmed in the SonicOS UI.
- Routes, HA, wireless, certificates, per-rule security profiles, flood protection and admin privilege masks are not read.
- No real SonicOS 8.x export has been tested.
- Baseline or policy comparison against a defined standard is not implemented.

## 1.0.1

Earlier release.

## 1.0.0

First release.
