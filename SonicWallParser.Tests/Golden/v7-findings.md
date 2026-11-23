# SonicWall Security Findings

**TZ 370  \|  Firmware 7.1.2-7019-R6288  \|  SonicOS 7.x**

- Report Generated: <normalized>
- Parser version: <normalized>
- Age and expiry checks use 2025-01-01 UTC, the newest change time in the export.

---
## Summary

| Severity | Findings |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 3 |
| Low | 2 |
| Info | 2 |

40 checks ran. 14 item(s) could not be assessed from the export. Hit counters reset on reboot, so zero-hit findings are for review only.

No findings in: Device and lifecycle, NAT and exposure, Users and authentication. That covers only the checks that had data: see Not assessed for those that did not.

> **Warning:** Some findings rest on value mappings that are not confirmed against the SonicOS UI. They are marked Unverified mapping in the Basis column.

---
## Firewall rules

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Info | R008 | Custom rule with zero hits | Rule 0 (Wifi to printer) | WLAN to LAN, source Corp LAN, destination Printer, service HTTPS. Hit counters reset on reboot or when counters are cleared, so zero hits means review, not delete. Only custom rules are listed; auto-generated and built-in default rules are never cleanup candidates. | Confirmed |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| R008 | A rule that has never matched may be obsolete. Hit counters reset on reboot or when cleared, so this is a reason to review, not to delete. | Confirm with the owner whether the rule is still needed, and check logs over a longer period before removing it. |

---
## Management exposure

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Medium | M005 | SonicOS API enabled | Device settings | The API is enabled and CORS is permitted. | Confirmed |
| Medium | M007 | Admin login does not require a one-time password | Device settings | adminOtpReq is 0, which is believed to mean not required. That meaning is unverified. | Unverified mapping |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| M005 | The API is another management surface. Permissive CORS widens who can call it from a browser. | Disable the API if unused, and turn off CORS unless a known web application needs it. |
| M007 | A password alone is a single point of failure for the most privileged accounts. | Require multi-factor authentication for administrators. |

---
## VPN

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Info | V007 | Disabled VPN policy still present | VPN 0 (Partner) | ipsecSaDisabled is on, which is believed to mean the policy is disabled. That meaning is unverified. | Unverified mapping |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| V007 | Disabled VPN policies clutter the configuration and keep old peer details around. | Delete policies that are no longer needed. |

---
## Zones and security services

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Medium | Z002 | Wireless or guest zone can reach a trusted zone | Rule 0 (Wifi to printer) | WLAN to LAN, source Corp LAN, destination Printer, service HTTPS | Confirmed |
| Low | Z001 | Security services off on a zone | Zone LAN | Zone LAN has gateway antivirus, anti-spyware, application control, content filtering off. Intrusion prevention is not assessed because its state is unknown on SonicOS 7.x. | Confirmed |
| Low | Z001 | Security services off on a zone | Zone WLAN | Zone WLAN has gateway antivirus, anti-spyware, application control, content filtering off. Intrusion prevention is not assessed because its state is unknown on SonicOS 7.x. | Confirmed |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| Z001 | Gateway antivirus, anti-spyware, application control and content filtering are applied per zone. A zone with them off gets no inspection (if licensed). | Enable the services you are licensed for on LAN, DMZ and wireless zones. |
| Z002 | Guest and wireless devices are less trusted. Allowing them into the LAN undermines segmentation. | Remove or narrow the rule so guest and wireless clients can reach only the internet and required services. |

---
## Not assessed

These checks could not be done from the export. They are listed so that a gap is never mistaken for a pass.

| Id | What was not checked | Why |
|---|---|---|
| NA01 | Admin accounts, admin rights and per-account multi-factor | Privilege masks are not decoded (they were zero in every sample), so admin accounts cannot be told apart. |
| NA02 | SNMP v1 or v2c community strings and default communities | Community strings are credentials and are never read. |
| NA03 | Known vulnerable or end-of-life firmware | Needs a maintained lookup of builds, which is not part of the export. |
| NA04 | Certificate expiry | Certificate validity dates are not read from the export yet. |
| NA05 | Security profiles applied per rule (gateway antivirus, IPS, application control, DPI-SSL) | The per-rule profile keys are unconfirmed, so allow rules without profiles cannot be identified. |
| NA06 | Flood protection and anti-spoofing settings | These settings are not read yet. |
| NA07 | IKE version in use and whether a pre-shared key is used | The meaning of ipsecVersion is unverified, and keys are never read. |
| NA08 | Remote access VPN multi-factor and breadth of network access | Not determinable from the settings that are read. |
| NA09 | High availability configuration | HA settings are not read yet. |
| NA10 | Intrusion prevention per zone | On SonicOS 7.x the source of the zone IPS state is not confirmed, so it shows as Unknown. |
| M003 | Admin login lockout | loginFailLockoutEnable is not in the export. |
| M004 | Minimum admin password length | minPasswordLength is not in the export. |
| D002 | NTP time sync | ntp_useNtp is not in the export. |
| D003 | Syslog server | The syslog settings are not in the export. |

