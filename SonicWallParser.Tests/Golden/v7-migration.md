# Firewall Migration Checklist

**TZ 370  \|  Firmware 7.1.2-7019-R6288  \|  SonicOS 7.x**

- Report Generated: <normalized>
- Parser version: <normalized>

---
## How to use this checklist

This list is platform neutral. It names settings and values, not any vendor's menus. Tick Done as each item is rebuilt and checked.

| Parameter | Value |
|---|---|
| Items captured from the export | 18 |
| Items partly captured (verify) | 6 |
| Items to collect manually | 30 |
| Planning steps | 21 |
| Custom rules to rebuild | 1 |
| Auto-generated and default rules (not listed, the new platform creates its own) | 1 |

> **Warning:** Pre-shared keys, passwords, one-time-password seeds, certificates and private keys are never read from the export and are marked Collect manually. Treat any value shown here as client confidential.

---
## 1. Pre-migration collection

### Device and licensing

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Model | TZ 370 | Captured from export |  |
| [ ] | Serial number | 0006B1000002 | Captured from export |  |
| [ ] | Firmware | 7.1.2-7019-R6288 (SonicOS 7.x) | Captured from export |  |
| [ ] | Registration and support contract | - | Collect manually | Confirm who owns the registration and when support ends. |
| [ ] | Service svc-a | Activated: yes, count 0, expires 2030-01-01 00:00:00 UTC | Partly captured, verify | Entitlements are not fully in the export. Check the vendor portal. |
| [ ] | Subscriptions to re-buy or transfer | - | Collect manually |  |

### Access, contacts and planning

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Current configuration backup stored safely | - | Plan | Keep the original export. It contains secrets, so store it encrypted. |
| [ ] | Administrator credentials for the old and new devices | - | Collect manually | Passwords are never read from the export. |
| [ ] | Change window and approvals | - | Plan |  |
| [ ] | Rollback plan | - | Plan | Decide the point of no return and how to go back. |
| [ ] | Contact list (client, ISP, VPN peers, application owners) | - | Collect manually |  |
| [ ] | ISP details (circuit ids, account, static address allocation, support numbers) | - | Collect manually |  |

### Secrets and keys the export cannot give

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | VPN pre-shared keys and peer certificates | - | Collect manually | Obtain from the other side or the client. |
| [ ] | Certificates in use and their private keys | - | Collect manually | Names may show here, but private keys are not in the export. Plan to re-issue where needed. |
| [ ] | Local user passwords and one-time-password enrolments | - | Collect manually | Users will need new passwords and to re-enrol. |
| [ ] | Directory, RADIUS, LDAP and single sign-on server secrets | - | Collect manually |  |
| [ ] | PPPoE and other ISP credentials | - | Collect manually |  |
| [ ] | Remote access client software and licenses | - | Collect manually |  |

---
## 2. Build on the new platform

### Interfaces

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Interface X0 | zone LAN; 192.0.2.1 / 255.255.255.0; mode Unknown; VLAN none; MTU default; management: none | Captured from export | Speed, duplex and PortShield or switch ports: check manually (bit meanings unverified). |
| [ ] | Interface X1 | zone WAN; 198.51.100.2 / 255.255.255.0; mode DHCP; VLAN none; MTU default; management: none | Captured from export | Speed, duplex and PortShield or switch ports: check manually (bit meanings unverified). |
| [ ] | Interface X2 | zone WLAN; 192.0.2.129 / 255.255.255.128; mode Unknown; VLAN none; MTU default; management: none | Captured from export | Speed, duplex and PortShield or switch ports: check manually (bit meanings unverified). |

### WAN and ISP

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | WAN X1 addressing | DHCP; address 198.51.100.2; gateway -; DNS  | Partly captured, verify | PPPoE, L2TP and PPTP are not detected. Confirm the connection type and credentials with the ISP. |

### Routing

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Static routes, policy routes and dynamic routing | - | Collect manually | Route tables are not read from the export yet. Copy them from the old device. |

### Zones and trust model

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Zone LAN | LAN (Trusted); intra-zone traffic blocked; guest services off; services: none | Captured from export | Intrusion prevention state is unknown on this firmware: check manually. |
| [ ] | Zone WAN | WAN (Untrusted); intra-zone traffic blocked; guest services off; services: none | Captured from export | Intrusion prevention state is unknown on this firmware: check manually. |
| [ ] | Zone WLAN | WLAN (Wireless); intra-zone traffic blocked; guest services off; services: none | Captured from export | Intrusion prevention state is unknown on this firmware: check manually. |

### Address objects and groups (custom only)

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Address Corp LAN | IPv4 Network: 192.0.2.0/255.255.255.0 | Captured from export |  |
| [ ] | Address Printer | IPv4 Host: 192.0.2.30 | Captured from export |  |
| [ ] | Address v6 LAN | IPv6 Network: 2001:db8:1::/64 | Captured from export |  |
| [ ] | Built-in and factory address objects | 0 unused plus those used by rules | Plan | The new platform has its own defaults. Rebuild only objects that your rules use. |

### Service objects and groups (custom only)

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Service HTTPS | TCP 443 | Captured from export |  |

### Schedules

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|

### Firewall rules (custom, in evaluation order)

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Rule 0: Wifi to printer | Allow; WLAN to LAN; source Corp LAN; destination Printer; service HTTPS; logging on | Captured from export |  |
| [ ] | Per-rule security profiles | - | Collect manually | Per-rule profile keys are not read. Note which rules used antivirus, IPS, application control and DPI-SSL. |

### NAT (port forwards, one-to-one and outbound)

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Hairpin or reflexive NAT needs | - | Plan | Internal users reaching published services by their public name. |

### Site-to-site VPNs

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | VPN Partner | Disabled (unverified); peer 198.51.100.77 (secondary -); local Any; remote Any; phase 1 Unknown(3), Unknown(251), Unknown(6), Unknown(3), 0s; phase 2 Unknown(251), Unknown(6), Unknown(3), 0s; PFS off; local id -, remote id - | Partly captured, verify | Values marked unverified may differ from the old device. IKE version is not determined: confirm with the peer. |
| [ ] | VPN Partner: pre-shared key or certificate | - | Collect manually | Obtain from the other side or the client. |

### Remote access VPN

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Which users and groups may connect, MFA method, client software | - | Collect manually |  |

### Users and groups

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Directory integration, RADIUS, LDAP, SSO and MFA servers | - | Collect manually | Server settings and secrets are not read. |
| [ ] | Administrator accounts and their rights | - | Collect manually |  |

### DHCP and DNS

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | DHCP reservations, custom options and relay | - | Collect manually |  |
| [ ] | DNS proxy, split DNS and dynamic DNS | - | Collect manually |  |

### Security services

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Geo-IP and botnet filtering on custom rules | 0 rule(s) block by country, 0 rule(s) block botnet addresses | Captured from export |  |
| [ ] | Content filtering categories, allowed and forbidden lists, overrides | 0 content filter policy(ies) found | Partly captured, verify | Category contents are not read. Copy the lists from the old device. |
| [ ] | Application control policies and signature exclusions | - | Collect manually |  |
| [ ] | DPI-SSL exclusions and certificate handling | - | Collect manually |  |
| [ ] | Flood protection, anti-spoofing and connection limits | - | Collect manually |  |
| [ ] | Sandbox and other cloud services | - | Collect manually |  |

### Wireless, switching and high availability

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Wireless access points, SSIDs, security and VLANs | - | Collect manually |  |
| [ ] | Switch settings | - | Collect manually |  |
| [ ] | High availability pair, virtual addresses and monitoring | - | Collect manually | HA settings are not read from the export yet. |

### Management and monitoring

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Admin session and lockout policy | lockout unknown; max failures unknown; minimum password length unknown | Captured from export |  |
| [ ] | NTP and time zone | NTP unknown; time zone raw value - (encoding unverified) | Partly captured, verify | Set the time zone by name on the new device. |
| [ ] | Syslog destinations | none configured | Captured from export |  |
| [ ] | SNMP | enabled unknown | Partly captured, verify | Community strings and v3 users are never read. |
| [ ] | Management API | enabled yes | Captured from export |  |
| [ ] | Email alerts, NetFlow, scheduled tasks and backups | - | Collect manually |  |

### Third-party dependencies

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | VoIP and SIP settings, cameras, remote support tools | - | Collect manually |  |
| [ ] | Services that allow-list this firewall's public address | - | Collect manually | Cloud services, partners and banks may need the new address. |
| [ ] | Wake-on-LAN, hairpin or loopback access | - | Collect manually |  |

---
## 3. Cutover

### Before the change window

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Lower DNS TTLs for names that point at the old public address | - | Plan |  |
| [ ] | Tell VPN peers, partners and the ISP about the new device and address | - | Plan |  |
| [ ] | Take a final backup of the old device | - | Plan |  |
| [ ] | Stage and test the new device configuration offline | - | Plan |  |

### During the change window

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Swap cabling and bring up WAN connectivity | - | Plan |  |
| [ ] | Confirm addressing, routes and DNS on every interface | - | Plan |  |
| [ ] | Enable rules, NAT and VPNs in the agreed order | - | Plan |  |
| [ ] | Decide go or roll back at the agreed time | - | Plan |  |

---
## 4. Post-cutover validation

### Connectivity

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Internet access from X0 (LAN) | - | Plan |  |
| [ ] | Internet access from X2 (WLAN) | - | Plan |  |

### Services and users

| Done | Setting | Value from this device | Source | Notes |
|---|---|---|---|---|
| [ ] | Remote access login and access to resources | - | Plan |  |
| [ ] | DHCP leases and DNS resolution | - | Plan |  |
| [ ] | Printing, VoIP and line-of-business applications | - | Plan |  |
| [ ] | Content filter and security services are working | - | Plan |  |
| [ ] | Logging, alerts and monitoring reach their destinations | - | Plan |  |
| [ ] | Backups of the new configuration are stored | - | Plan |  |

---
## 5. Do not migrate without review

Items that look unused, disabled, duplicated or weak. Skipping them keeps the new policy clean, but confirm each with its owner first.

| Reason | Item | Notes |
|---|---|---|
| Unused rule (zero hits) | Rule 0 Wifi to printer | Counters may have been reset. |
| Disabled VPN policy (unverified meaning) | VPN 0 Partner | Confirm it is really disabled. |
| Unused address object | v6 LAN | Network: 2001:db8:1::/64 |

---
## 6. Differences to expect on the new platform

Left for the technician to fill in for the chosen platform.

| Topic | What differs | Action |
|---|---|---|
| Rule evaluation order |  |  |
| NAT and rule model |  |  |
| Default deny behaviour |  |  |
| VPN defaults and crypto names |  |  |
| Logging and reporting |  |  |
| Licensing model |  |  |

