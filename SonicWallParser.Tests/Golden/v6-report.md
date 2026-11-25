# SonicWall Configuration Report

**TZ 400  \|  Firmware 6.5.4.15-117n  \|  SonicOS 6.x**

- Report Generated: <normalized>
- Parser version: <normalized>

---
## Executive Summary

| Parameter | Value |
|---|---|
| Device | TZ 400 (serial 0006B1000001) |
| Firmware | 6.5.4.15-117n, SonicOS 6.x |
| Zones and interfaces | 3 zones, 3 interfaces |
| Firewall rules (IPv4) | 4 total: 3 custom, 0 auto-generated, 1 built-in default |
| Unused custom rules | 2 (zero hits, for review only) |
| NAT policies | 1 IPv4, 0 IPv6 |
| VPN policies | 1 |
| Local users | 1 |
| Objects | 4 address, 2 service, 1 schedule |

| Parameter | Value |
|---|---|
| Findings | 0 critical, 0 high, 8 medium, 4 low, 3 info |
| Checks not assessable from an export | 11 |

No critical or high findings.

Coverage: 0 parser warning(s), 0 setting key(s) not read by this parser yet. Values marked unverified have not been confirmed against the SonicOS UI.

---
## Device Information

| Parameter | Value |
|---|---|
| Product | TZ 400 |
| Firmware | 6.5.4.15-117n |
| Firmware generation | SonicOS 6.x |
| Serial Number | 0006B1000001 |
| Locale | en |
| CLI Idle Timeout | 0s |
| SonicOS API | Disabled |

---
## Zones

| Zone | Type | Intra-Zone | GAV | IPS | App Ctrl | Anti-Spy | DPI-SSL | CFS | Guest |
|---|---|---|---|---|---|---|---|---|---|
| LAN | LAN (Trusted) | No | No | No | No | No | No | No | No |
| WAN | WAN (Untrusted) | No | No | No | No | No | No | No | No |
| DMZ | DMZ (Public) | No | No | Yes | No | No | No | No | No |

---
## Interfaces

| Name | Description | Zone | IP Address | Mask | Mode | VLAN | HTTPS | HTTP | SSH | Ping | SNMP |
|---|---|---|---|---|---|---|---|---|---|---|---|
| X0 |  | LAN | 192.0.2.1 | 255.255.255.0 | Unknown | - | Yes | No | No | No | No |
| X1 |  | WAN | 198.51.100.2 | 255.255.255.0 | Unknown | - | No | No | No | Yes | No |
| X2 |  | DMZ | 192.0.2.65 | 255.255.255.192 | Unknown | - | No | No | No | No | No |

Addressing mode is derived from confirmed keys only (DHCP client, static address). PPPoE, L2TP and PPTP are not detected because their keys are unconfirmed.

---
## Address Objects

| # | Name | Type | Address | Zone |
|---|---|---|---|---|
| 0 | Corp LAN | Network | 192.0.2.0/255.255.255.0 |  |
| 1 | Web Server | Host | 192.0.2.10 |  |
| 2 | Mail Server | Host | 192.0.2.11 |  |
| 3 | Servers | Group | (group) |  |

---
## Address Groups (resolved)

| Group | Direct members | Resolved to |
|---|---|---|
| Servers | Web Server, Mail Server | Web Server (192.0.2.10), Mail Server (192.0.2.11) |

---
## Service Objects

| # | Name | Protocol | Port(s) |
|---|---|---|---|
| 0 | HTTPS | TCP | 443 |
| 1 | RDP | TCP | 3389 |

---
## Schedule Objects

| # | Name | Type | Days | Time Window |
|---|---|---|---|---|
| 0 | Office hours | 1 | Mon, Tue, Wed, Thu, Fri | 08:00 to 17:00 |

Days: bit 0 is Sunday (inferred from the values 127, 62 and 65). Type 1 times are minutes since midnight and type 2 patterns are not decoded. Both are unverified.

---
## Firewall Policies (IPv4)

Rule origin: 3 custom, 0 auto-generated, 1 built-in default. 2 custom rule(s) are unused (zero hits).

Action value 0 is shown as Deny (0) but is unverified. It is the value on the built-in WAN-to-LAN default rules and is probably Deny, which still needs confirming in the SonicOS UI.

| # | Action | Name | Zones | Source | Destination | Service | Schedule | Log | On | Hits | Origin | Comment |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Allow | Outbound | LAN to WAN | Corp LAN | Any | Any | Always | Yes | Yes | 0 | Custom | Synthetic outbound |
| 1 | Allow | Web in | WAN to DMZ | Any | Web Server | HTTPS | Always | Yes | Yes | 120 | Custom | Synthetic published web |
| 2 | Allow | Idle rule | LAN to DMZ | Corp LAN | Mail Server | HTTPS | Office hours | No | Yes | 0 | Custom |  |
| 3 | Deny (0) | Default Access Rule | WAN to LAN | Any | Any | Any | Always | No | Yes | 0 | Default |  |

---
## NAT Policies (IPv4)

| # | Orig Src | Orig Dst | Orig Svc | Trans Src | Trans Dst | Trans Svc | In | Out | On | Comment |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Any | Web Server | HTTPS | Any | Web Server | Any | Any | Any | Yes | Synthetic port forward |

---
## VPN Policies (IPSec)

### VPN 0: Branch office (Site-to-Site)

| Parameter | Value |
|---|---|
| State | Enabled (unverified) |
| Peer gateway | 203.0.113.10 |
| Secondary gateway | - |
| Local network | Corp LAN (192.0.2.0/255.255.255.0) |
| Remote network | Mail Server (192.0.2.11) |
| Bound to | - |
| Remote ID | - |
| Exchange mode | Aggressive Mode (unverified) |
| Protocol | Proto(0) |
| PFS | No |
| Keep-alive | No |
| XAUTH client | No |
| NetBIOS | No |
| Multicast | No |
| Raw ipsecVersion | 0 (meaning unverified) |

| Phase | Encryption | Hash | DH Group | Lifetime |
|---|---|---|---|---|
| Phase 1 | 3DES (unverified) | SHA-256 (unverified) | Group 1 (768-bit, unverified) | 0s |
| Phase 2 | 3DES (unverified) | SHA-256 (unverified) | Group 1 (768-bit, unverified) | 0s |

Pre-shared keys and certificates are never read or shown. Enabled or disabled state comes from ipsecSaDisabled and is unverified.

---
## Local Users

| # | Name | Comment | VPN Network | Guest |
|---|---|---|---|---|
| 0 | alice.example | Synthetic user | - | No |

Passwords, hashes and one-time-password data are never read.

---
## DHCP Server Scopes

### DHCP Scope 0

| Parameter | Value |
|---|---|
| Range | 192.0.2.100 to 192.0.2.199 |
| Subnet Mask | 255.255.255.0 |
| Gateway | 192.0.2.1 |
| DNS | 198.51.100.53, 198.51.100.54 |
| WINS | 203.0.113.5 |
| Domain | lan.example.com |
| Lease Time | 1440 (minutes, unverified) |
| Enabled | Yes |

---
## Device Settings

### Management and admin hardening

| Parameter | Value |
|---|---|
| Admin login timeout | Unknown (unit unverified) |
| CLI idle timeout | Unknown |
| Login lockout | No |
| Account lockout | Unknown |
| Max failed logins | Unknown |
| Lockout time | Unknown |
| Admin one-time password required (raw) | Unknown |
| Password complexity (raw) | Unknown |
| Minimum password length | Unknown |
| Password update period | Unknown |
| SonicOS API | Unknown |
| API CORS | Unknown |
| API basic auth | Unknown |
| API digest auth | Unknown |
| API public key auth | Unknown |
| API bearer token | Unknown |

### Time, logging and monitoring

| Parameter | Value |
|---|---|
| NTP enabled | Yes |
| Daylight saving | Unknown |
| NTP update interval | Unknown |
| Time zone (raw) | Unknown |
| Syslog server | 192.0.2.50:514 |
| Syslog standby server | Not configured |
| SNMP | Unknown |

SNMP community strings, passwords, one-time-password data and keys are never read.

---
## Object Hygiene

2 unused custom object(s), 0 duplicate(s), 0 dangling reference(s). A further 0 unused object(s) look built-in (created in the same instant as many others) and are not listed. That test is a heuristic and is unverified.

### Unused objects (nothing refers to them)

| Kind | Name | Definition |
|---|---|---|
| Address | Servers | Group: (group) |
| Service | RDP | TCP 3389 |

---
## ⚠ Unused Custom Rules (Zero Hits)

> **Warning:** These enabled custom rules have never matched any traffic and may be candidates for review. Hit counters reset on reboot or when counters are cleared, so zero hits means review, not delete. Only custom rules are listed; auto-generated and built-in default rules are never cleanup candidates.

| # | Action | Name | Zones | Source | Destination | Service | Comment |
|---|---|---|---|---|---|---|---|
| 0 | Allow | Outbound | LAN to WAN | Corp LAN | Any | Any | Synthetic outbound |
| 2 | Allow | Idle rule | LAN to DMZ | Corp LAN | Mail Server | HTTPS |  |

