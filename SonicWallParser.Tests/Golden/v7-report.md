# SonicWall Configuration Report

**TZ 370  \|  Firmware 7.1.2-7019-R6288  \|  SonicOS 7.x**

- Report Generated: <normalized>
- Parser version: <normalized>

---
## Executive Summary

| Parameter | Value |
|---|---|
| Device | TZ 370 (serial 0006B1000002) |
| Firmware | 7.1.2-7019-R6288, SonicOS 7.x |
| Zones and interfaces | 3 zones, 3 interfaces |
| Firewall rules (IPv4) | 2 total: 1 custom, 0 auto-generated, 1 built-in default |
| Unused custom rules | 1 (zero hits, for review only) |
| NAT policies | 0 IPv4, 0 IPv6 |
| VPN policies | 1 |
| Local users | 0 |
| Objects | 3 address, 1 service, 0 schedule |

| Parameter | Value |
|---|---|
| Findings | 0 critical, 0 high, 3 medium, 2 low, 2 info |
| Checks not assessable from an export | 14 |

No critical or high findings.

Coverage: 0 parser warning(s), 1 setting key(s) not read by this parser yet. Values marked unverified have not been confirmed against the SonicOS UI.

---
## Device Information

| Parameter | Value |
|---|---|
| Product | TZ 370 |
| Firmware | 7.1.2-7019-R6288 |
| Firmware generation | SonicOS 7.x |
| Serial Number | 0006B1000002 |
| Locale | en |
| CLI Idle Timeout | 0s |
| SonicOS API | Enabled |

---
## Parsing Warnings / Skipped Sections

| Severity | Section | Line | Message |
|---|---|---|---|
| Info | Unparsed |  | 1 key(s) in 1 settings group(s) are not read by this parser yet. |
| Info | zoneObjDefaultRuleOptions |  | Unparsed or unsupported settings group 'zoneObjDefaultRuleOptions' contains 1 key(s). Sample keys: zoneObjDefaultRuleOptions_0. |

| Unparsed Group | Keys | Sample Keys |
|---|---|---|
| zoneObjDefaultRuleOptions | 1 | zoneObjDefaultRuleOptions_0 |

---
## Zones

| Zone | Type | Intra-Zone | GAV | IPS | App Ctrl | Anti-Spy | DPI-SSL | CFS | Guest |
|---|---|---|---|---|---|---|---|---|---|
| LAN | LAN (Trusted) | No | No | Unknown | No | No | No | No | No |
| WAN | WAN (Untrusted) | No | No | Unknown | No | No | No | No | No |
| WLAN | WLAN (Wireless) | No | No | Unknown | No | No | No | No | No |

---
## Interfaces

| Name | Description | Zone | IP Address | Mask | Mode | VLAN | HTTPS | HTTP | SSH | Ping | SNMP |
|---|---|---|---|---|---|---|---|---|---|---|---|
| X0 |  | LAN | 192.0.2.1 | 255.255.255.0 | Unknown | - | No | No | No | No | No |
| X1 |  | WAN | 198.51.100.2 | 255.255.255.0 | DHCP | - | No | No | No | No | No |
| X2 |  | WLAN | 192.0.2.129 | 255.255.255.128 | Unknown | - | No | No | No | No | No |

Addressing mode is derived from confirmed keys only (DHCP client, static address). PPPoE, L2TP and PPTP are not detected because their keys are unconfirmed.

---
## Address Objects

| # | Name | Type | Address | Zone |
|---|---|---|---|---|
| 0 | Corp LAN | Network | 192.0.2.0/255.255.255.0 |  |
| 1 | Printer | Host | 192.0.2.30 |  |

---
## Address Objects (IPv6)

| # | Name | Type | Address | Zone |
|---|---|---|---|---|
| 0 | v6 LAN | Network | 2001:db8:1::/64 |  |

---
## Service Objects

| # | Name | Protocol | Port(s) |
|---|---|---|---|
| 0 | HTTPS | TCP | 443 |

---
## Firewall Policies (IPv4)

Rule origin: 1 custom, 0 auto-generated, 1 built-in default. 1 custom rule(s) are unused (zero hits).

| # | Action | Name | Zones | Source | Destination | Service | Schedule | Log | On | Hits | Origin | Comment |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Allow | Wifi to printer | WLAN to LAN | Corp LAN | Printer | HTTPS | Always | Yes | Yes | 0 | Custom |  |
| 1 | Allow | Default Access Rule | LAN to WAN | Any | Any | Any | Always | No | Yes | 0 | Default |  |

---
## VPN Policies (IPSec)

### VPN 0: Partner (Site-to-Site)

| Parameter | Value |
|---|---|
| State | Disabled (unverified) |
| Peer gateway | 198.51.100.77 |
| Secondary gateway | - |
| Local network | - (Any) |
| Remote network | - (Any) |
| Bound to | - |
| Remote ID | - |
| Exchange mode | Unknown(3) |
| Protocol | Proto(0) |
| PFS | No |
| Keep-alive | No |
| XAUTH client | No |
| NetBIOS | No |
| Multicast | No |
| Raw ipsecVersion | 0 (meaning unverified) |

| Phase | Encryption | Hash | DH Group | Lifetime |
|---|---|---|---|---|
| Phase 1 | Unknown(251) | Unknown(6) | Unknown(3) | 0s |
| Phase 2 | Unknown(251) | Unknown(6) | Unknown(3) | 0s |

Pre-shared keys and certificates are never read or shown. Enabled or disabled state comes from ipsecSaDisabled and is unverified.

---
## Device Settings

### Management and admin hardening

| Parameter | Value |
|---|---|
| Admin login timeout | Unknown (unit unverified) |
| CLI idle timeout | Unknown |
| Login lockout | Unknown |
| Account lockout | Unknown |
| Max failed logins | Unknown |
| Lockout time | Unknown |
| Admin one-time password required (raw) | 0 (0 believed to mean no, unverified) |
| Password complexity (raw) | Unknown |
| Minimum password length | Unknown |
| Password update period | Unknown |
| SonicOS API | Yes |
| API CORS | Yes |
| API basic auth | Unknown |
| API digest auth | Unknown |
| API public key auth | Unknown |
| API bearer token | Unknown |

### Time, logging and monitoring

| Parameter | Value |
|---|---|
| NTP enabled | Unknown |
| Daylight saving | Unknown |
| NTP update interval | Unknown |
| Time zone (raw) | Unknown |
| Syslog server | Not configured |
| Syslog standby server | Not configured |
| SNMP | Unknown |

### Service licenses

| # | Service id | Activated | Count | Expiration |
|---|---|---|---|---|
| 0 | svc-a | Yes | 0 | 2030-01-01 00:00:00 UTC |

License counts and expiry come from the export when present. Registration state and support contracts are not in the export.

SNMP community strings, passwords, one-time-password data and keys are never read.

---
## Object Hygiene

1 unused custom object(s), 0 duplicate(s), 0 dangling reference(s). A further 0 unused object(s) look built-in (created in the same instant as many others) and are not listed. That test is a heuristic and is unverified.

### Unused objects (nothing refers to them)

| Kind | Name | Definition |
|---|---|---|
| Address | v6 LAN | Network: 2001:db8:1::/64 |

---
## ⚠ Unused Custom Rules (Zero Hits)

> **Warning:** These enabled custom rules have never matched any traffic and may be candidates for review. Hit counters reset on reboot or when counters are cleared, so zero hits means review, not delete. Only custom rules are listed; auto-generated and built-in default rules are never cleanup candidates.

| # | Action | Name | Zones | Source | Destination | Service | Comment |
|---|---|---|---|---|---|---|---|
| 0 | Allow | Wifi to printer | WLAN to LAN | Corp LAN | Printer | HTTPS |  |

