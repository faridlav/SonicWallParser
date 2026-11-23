# SonicWall Security Findings

**TZ 400  \|  Firmware 6.5.4.15-117n  \|  SonicOS 6.x**

- Report Generated: <normalized>
- Parser version: <normalized>
- Age and expiry checks use 1970-01-01 UTC, the newest change time in the export.

---
## Summary

| Severity | Findings |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 8 |
| Low | 4 |
| Info | 3 |

40 checks ran. 11 item(s) could not be assessed from the export. Hit counters reset on reboot, so zero-hit findings are for review only.

No findings in: Device and lifecycle, NAT and exposure, Users and authentication. That covers only the checks that had data: see Not assessed for those that did not.

> **Warning:** Some findings rest on value mappings that are not confirmed against the SonicOS UI. They are marked Unverified mapping in the Basis column.

---
## Firewall rules

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Medium | R002 | Inbound allow from any internet address | Rule 1 (Web in) | WAN to DMZ, source Any, destination Web Server, service HTTPS | Confirmed |
| Low | R004 | Allow rule without logging | Rule 2 (Idle rule) | LAN to DMZ, source Corp LAN, destination Mail Server, service HTTPS | Confirmed |
| Info | R003 | Allow rule with any service | Rule 0 (Outbound) | LAN to WAN, source Corp LAN, destination Any, service Any | Confirmed |
| Info | R008 | Custom rule with zero hits | Rule 0 (Outbound) | LAN to WAN, source Corp LAN, destination Any, service Any. Hit counters reset on reboot or when counters are cleared, so zero hits means review, not delete. Only custom rules are listed; auto-generated and built-in default rules are never cleanup candidates. | Confirmed |
| Info | R008 | Custom rule with zero hits | Rule 2 (Idle rule) | LAN to DMZ, source Corp LAN, destination Mail Server, service HTTPS. Hit counters reset on reboot or when counters are cleared, so zero hits means review, not delete. Only custom rules are listed; auto-generated and built-in default rules are never cleanup candidates. | Confirmed |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| R002 | A rule that accepts traffic from any address on the WAN exposes the destination to the whole internet. | Limit the source to known addresses or a geography where possible, and make sure the service is hardened and patched. |
| R003 | A rule that allows every service gives more access than needed and makes the rule hard to review. | List the specific services required, or use a service group. |
| R004 | Without logging there is no record of what the rule permitted, which hurts investigation and auditing. | Enable logging on the rule, at least for rules that expose services or cross a trust boundary. |
| R008 | A rule that has never matched may be obsolete. Hit counters reset on reboot or when cleared, so this is a reason to review, not to delete. | Confirm with the owner whether the rule is still needed, and check logs over a longer period before removing it. |

---
## Management exposure

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Medium | M003 | Admin login lockout disabled | Device settings | loginFailLockoutEnable is off, so repeated failed admin logins are not blocked. | Confirmed |
| Low | M001 | Ping response enabled on a WAN interface | Interface X1 | X1 answers ping from the internet. | Confirmed |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| M001 | Administration reachable from the internet is a common way into firewalls. | Disable HTTPS, SSH and SNMP management on WAN interfaces, and manage the device from the LAN or over a VPN. |
| M003 | Without lockout an attacker can guess admin passwords without limit. | Enable login failure lockout with a sensible threshold. |

---
## VPN

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Medium | V001 | Weak VPN encryption | VPN 0 (Branch office) | Phase 1 uses 3DES (unverified). | Unverified mapping |
| Medium | V001 | Weak VPN encryption | VPN 0 (Branch office) | Phase 2 uses 3DES (unverified). | Unverified mapping |
| Medium | V003 | Weak Diffie-Hellman group | VPN 0 (Branch office) | Phase 1 uses Group 1 (768-bit, unverified). | Unverified mapping |
| Medium | V003 | Weak Diffie-Hellman group | VPN 0 (Branch office) | Phase 2 uses Group 1 (768-bit, unverified). | Unverified mapping |
| Medium | V004 | Aggressive mode in use | VPN 0 (Branch office) | Phase 1 exchange is Aggressive Mode (unverified). The mapping of value 2 to aggressive mode is unverified. | Unverified mapping |
| Medium | V005 | Perfect forward secrecy disabled | VPN 0 (Branch office) | PFS is off for this site-to-site policy. | Confirmed |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| V001 | DES and 3DES are obsolete and weak by modern standards. | Use AES-128 or AES-256 (preferably AES-GCM where supported). |
| V003 | Groups 1, 2 and 5 are too small to resist modern attacks. | Use group 14 (2048-bit) or higher, or an elliptic curve group. |
| V004 | Aggressive mode exposes more of the handshake, and with a pre-shared key it allows offline guessing. Whether a pre-shared key is used cannot be seen, because keys are never read. | Use main mode, or IKEv2, with a long random key or certificates. |
| V005 | Without PFS, recording the traffic and later obtaining the key exposes past sessions. | Enable PFS on the Phase 2 settings. |

---
## Zones and security services

| Severity | Check | Finding | Where | Detail | Basis |
|---|---|---|---|---|---|
| Low | Z001 | Security services off on a zone | Zone DMZ | Zone DMZ has gateway antivirus, anti-spyware, application control, content filtering off. Intrusion prevention is not assessed because its state is unknown on SonicOS 7.x. | Confirmed |
| Low | Z001 | Security services off on a zone | Zone LAN | Zone LAN has gateway antivirus, anti-spyware, application control, content filtering off. Intrusion prevention is not assessed because its state is unknown on SonicOS 7.x. | Confirmed |

### Why it matters and how to fix it

| Check | Why it matters | Suggested fix |
|---|---|---|
| Z001 | Gateway antivirus, anti-spyware, application control and content filtering are applied per zone. A zone with them off gets no inspection (if licensed). | Enable the services you are licensed for on LAN, DMZ and wireless zones. |

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
| M004 | Minimum admin password length | minPasswordLength is not in the export. |
| D004 | Service license expiry | No service license entries are in the export. |

