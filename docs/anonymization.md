# Anonymizing configuration data

Use this before sharing a report, a sample export or a bug report.

## What a real export contains

- Public and private IP addresses, subnets and MAC addresses.
- Object, rule, zone, interface, VPN, schedule and certificate names, often with client or site names in them.
- User names, group names and comments.
- The device serial number, firmware dates and domain names.
- Secrets: password hashes, one-time-password seeds, RADIUS, LDAP and AD secrets, VPN keys, PPPoE credentials, SNMP community strings.

The parser never reads the secrets, but a raw `.exp` file still contains them. **Never share, attach or commit a raw `.exp` file.**

## Sharing a report

Run with `--redact`:

```bash
SonicWallParser backup.exp ./share --redact
```

Redaction works on a copy and replaces, consistently, so references still line up:

| Replaced | With |
|----------|------|
| IPv4 addresses | addresses in 198.18.0.0/15 |
| IPv6 addresses | addresses in 2001:db8::/32 |
| MAC addresses | addresses in 00:00:5e:00:53:xx |
| Names (objects, rules, zones, users, VPNs, certificates, domains) | `name-001`, `name-002`, and so on |
| Comments and descriptions | `[comment redacted]` |
| Serial numbers | `REDACTED-SERIAL` |

Kept: subnet masks, ports and protocols, default zone names (LAN, WAN, DMZ and similar), interface names such as X0, crypto settings, counts, firmware and model.

Redaction is best effort. **Read the redacted output before you share it.** Free text in a field the tool does not know about, or a name typed inside another value, may survive.

## Contributing a sample export

1. Do not post the raw file. Ask the maintainer how to send it privately.
2. If a sanitized export is needed, replace public addresses with documentation ranges (192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24), replace client names with neutral ones, and clear comments, user names, serials and domain names.
3. Check the result with a text search for the original names, domains, public addresses and serial.

## Test fixtures

Fixtures in this repository are hand-written and clearly fake. They use reserved address ranges and `example.com`. They are never copies of real exports, even lightly edited ones.
