# SonicWallParser Project Checklist

Repo: faridlav/SonicWallParser (reviewed at commit 6d516ec, Nov 2026)

Priority legend: **P0** fix first (wrong or silently broken output), **P1** core new value, **P2** quality and polish, **P3** later.

Items marked **(verify)** depend on real SonicOS exports and must be confirmed against actual files, not assumed.

Working rules for every item: meaningful commits with good descriptions, tests written with the change (unit, integration, end to end where the item touches output), GitHub Actions kept minimal.

---

## 0. Decisions needed before building

- [ ] Decide where the security findings page lives: a dedicated page or section inside each report (HTML, PDF, Markdown), a standalone file, or both
- [ ] Decide where the migration checklist lives: same choice as above, plus whether it should be printable with tick boxes for the technician
- [ ] Decide the minimum supported SonicOS range (for example 5.9 through the latest 7.x) and whether SonicOSX and NSsp are in scope
- [ ] Decide paper size default for PDF (Letter for US use) and whether to offer a switch
- [ ] Collect real anonymized `.exp` samples (see section 2)

---

## 1. Correctness bugs (P0)

- [ ] **Serial number:** `SerialNumber` and `PreviousSerial` both read `prefsHistorySerialNumber`. Find the real current-serial key and separate the two (verify)
- [ ] **One definition of "unused rule":** Markdown, PDF and the CLI summary use zero hits plus no last-hit time (enabled or not). HTML uses zero hits plus enabled. Pick one definition, implement it once, use it everywhere, and make all outputs show the same count
- [ ] **NAT hit counts always 0:** the model has `HitCount` and the HTML shows a Hits column, but the extractor never fills it. Extract it (verify key name) or remove the column
- [ ] **Zone Guest flag always No:** `GuestServices` is never extracted. Extract it (verify key) or remove the column
- [ ] **Hit count caveat:** state in every report that hit counters reset on reboot or counter clear, so zero hits means "review", not "delete". Show uptime or last-reset info if the export contains it
- [ ] **Timestamps depend on the machine:** conversion uses local time. Switch to UTC (or state the zone) so the same file gives the same report anywhere
- [ ] **Index gaps create blank objects:** a missing `_1` produces an empty row. Decide the intended behavior, change the extractor or the test to match, and keep the diagnostic
- [ ] **Known-prefix list hides unparsed keys:** broad prefixes (`policy`, `vpn`, `ipsec`, `iface_`) mark every key under them as known even when never read. Replace with tracking of keys actually consumed, so unparsed keys always surface
- [ ] **CLI-export detection false positives:** words like "commit" or "configure" anywhere in decoded text trigger rejection. Tighten the detection
- [ ] **Silent empty sections:** if a core section (rules, zones, interfaces, address objects) returns zero items, emit a loud warning in the CLI and in the report, not a quiet zero
- [ ] **Markdown table pipes:** escape pipe characters in comments and names so tables do not break
- [ ] **Crypto and protocol mappings (verify against real exports):**
  - [ ] Encryption IDs 2 and 3 both map to 3DES: confirm
  - [ ] Add AES-GCM and null encryption if they exist
  - [ ] IKE exchange mode: add IKEv2 and confirm Main and Aggressive values
  - [ ] Service protocols: name GRE, ESP, AH and other common IP protocols instead of `Proto(n)`
  - [ ] Service port handling when Port1 is 0 and Port2 is set
  - [ ] Policy action values (Allow, Deny, Discard)
  - [ ] Zone type values and DH group list
- [ ] **Static mutable state in `TableExtractor`:** remove static diagnostics and line-number state so extraction is repeatable and thread-safe (needed for batch mode)
- [ ] **Console output from inside the extractor:** move to the CLI layer
- [ ] **Unused cancellation token in `Program.cs`:** either use it or remove it

---

## 2. Cover all SonicOS versions (P0)

Goal: every `.exp` from every SonicOS generation parses correctly, and anything unsupported fails loudly and clearly.

**Collect samples**
- [ ] Gather anonymized `.exp` files from as many versions as possible: 5.x, 6.0 to 6.5.x, 7.0, 7.1, 7.2, 7.3, newest 7.x, plus SonicOSX or NSsp if in scope
- [ ] Include varied device sizes (TZ, NSa, NSsp) and configs (HA pairs, many VLANs, VPN heavy, wireless, WAN failover)
- [ ] Write an anonymization procedure (what to scrub: public IPs, names, serials, usernames, comments, hostnames, certificates) and document it in the repo
- [ ] Store sanitized fixtures in the test project

**Understand the formats**
- [ ] For each version, diff the decoded key sets and record which keys exist, which were renamed, and which are new
- [ ] Document the per-generation differences (rules, NAT, zones, security profiles, VPN, users) in a repo doc
- [ ] Confirm how each version encodes the file (Base64 variants, terminators, line wrapping, BOM, character encoding)

**Build version handling**
- [ ] Detect the firmware generation and build from the file header and report it prominently
- [ ] Introduce per-version key maps (or a mapping layer) instead of one hardcoded key set
- [ ] Add a per-version coverage report: sections found, sections empty, keys unparsed
- [ ] Warn when the firmware is newer than the newest tested version, so the reader knows coverage may be incomplete
- [ ] Add a fallback mode that lists unparsed keys grouped by feature when the version is unknown
- [ ] Decide what to do with unsupported formats (CLI text export, tech support report, cloud-managed templates): clear error message, or add parsers
- [ ] Add a "supported versions" table to the README and keep it current

**Test**
- [ ] One golden test per fixture: expected counts per section plus spot checks of specific values
- [ ] Test that a file from an unsupported or unknown version produces a clear warning, not empty output
- [ ] Regression test every bug fixed from real files

---

## 3. Parsing coverage gaps (P1)

Fields and features present in exports but missing from the model or the report. Each item: add to model, extract, show in all formats, add tests.

**Firewall rules (IPv4 and IPv6)**
- [ ] Rule name, logging flag, schedule, users and groups
- [ ] Source service (currently ignored)
- [ ] Per-rule security profiles (GAV, IPS, Anti-Spyware, App Control, Content Filter, DPI-SSL, Botnet, GeoIP) (verify keys)
- [ ] Connection limits, bandwidth management, DSCP, and other advanced options
- [ ] Display and sort by priority (evaluation order), not just index
- [ ] Created and updated dates, last hit date

**NAT**
- [ ] Hit counts, priority, and full option set
- [ ] Show interface names rather than numbers where possible (IPv6 NAT stores numbers)

**VPN**
- [ ] Policy name
- [ ] Peer gateway address (primary and secondary)
- [ ] Enabled or disabled state
- [ ] Authentication method, IKE version, DPD, keep-alive, XAUTH
- [ ] Resolved local and remote network names to real subnets
- [ ] Remote access VPN (SSLVPN, NetExtender, GroupVPN, Global VPN Client) settings: ports, auth, certificates in use, client IP pools, routes, user and group access
- [ ] Never extract or print pre-shared keys or passwords

**Users and authentication**
- [ ] Decode `PrivilegeMask` and `Properties` (admin rights, VPN access, SSLVPN access, guest)
- [ ] Local admin accounts (name, privilege level, MFA setting if present)
- [ ] LDAP, RADIUS, TACACS, SSO agent and other authentication servers (never print secrets)
- [ ] Admin lockout, password policy, session timeouts

**Network**
- [ ] WAN addressing mode (static, DHCP, PPPoE, L2TP, PPTP) and its parameters
- [ ] Static, policy-based and dynamic routes (RIP, OSPF, BGP if present)
- [ ] Interface details: LAG, port mirroring, PortShield, VLAN subinterfaces, IPv6 settings, wireless
- [ ] DHCP: reservations, options, relay, scope-to-interface association, lease time in readable units
- [ ] DNS settings, DNS proxy, split DNS, DDNS
- [ ] High availability (mode, peer, monitoring, preempt)
- [ ] WAN failover and load balancing (already partly present: check completeness)
- [ ] Network monitor probes
- [ ] Wireless (SonicWave and integrated APs): SSIDs, security, VLANs, schedules
- [ ] SonicWall switch settings if present

**Security services**
- [ ] Zone-level service state and per-service settings (GAV, IPS, Anti-Spyware, App Control, CFS, DPI-SSL client and server, Botnet, GeoIP, Capture ATP)
- [ ] CFS profile contents (categories, allowed and forbidden lists, custom categories, keywords)
- [ ] App Control policies and signature exclusions
- [ ] DPI-SSL exclusions and certificate handling
- [ ] Flood protection, anti-spoofing, MAC-IP anti-spoof, SYN, UDP and ICMP limits
- [ ] Bandwidth management global and per-interface settings
- [ ] Schedules: decode `DaysOfWeek` and show real days and times (currently shows "see name")

**Management and device**
- [ ] Management ports and protocols (HTTPS, SSH, SNMP), allowed addresses
- [ ] SNMP settings (mask community strings)
- [ ] NTP, time zone, DNS servers
- [ ] Syslog, email alerts, logging and reporting settings, NetFlow
- [ ] Certificates present (names, expiry if available, never keys)
- [ ] Licenses and subscriptions, and their expiry, if present in the export
- [ ] SonicOS API settings (already extracted: also show in the report)
- [ ] Capture Client, Cloud Management, and other registration state if present

**Address and service objects**
- [ ] Address object properties and zone assignment fully shown
- [ ] FQDN and MAC address objects
- [ ] Service objects: ICMP types and codes, custom IP protocols, and service group nesting
- [ ] Created and updated dates

---

## 4. Analysis foundation (P1)

- [ ] **Name resolver:** resolve address and service groups (including nested) to real members, and rule, NAT and VPN references to real values
- [ ] **Reference graph:** which rules, NAT policies, VPN policies and groups reference each object
- [ ] **Dangling reference detection:** references to objects that do not exist
- [ ] **Unused object detection:** address objects, service objects, schedules, bandwidth objects and groups not referenced anywhere
- [ ] **Duplicate object detection:** different names with identical definitions
- [ ] **Rule order awareness:** rules evaluated by priority and specificity, not index
- [ ] **Shadowed and redundant rule detection:** rules that can never match, or that duplicate another
- [ ] **Shared report model:** compute all findings and views once, then render to all formats from the same model so the three outputs cannot drift

---

## 5. Security findings page: bad configs and weak points (P1)

A dedicated page in each report format. Each finding has: severity (Critical, High, Medium, Low, Info), what was found, where (rule number, interface, policy), why it matters, and a suggested fix. Summary counts at the top, and a clean-bill statement for categories with no findings.

**Firewall rules**
- [ ] Any-to-any allow rules
- [ ] Allow rules from WAN to LAN or other trusted zones, especially with broad source or service
- [ ] Allow rules with Any service, or very wide port ranges
- [ ] Rules with no logging
- [ ] Rules with no comment or no name
- [ ] Rules with no security profiles applied on allow traffic
- [ ] Disabled rules still present (cleanup candidates)
- [ ] Shadowed or redundant rules
- [ ] Rules with zero hits (with the reset caveat), and rules not hit for a long time
- [ ] Rules that reference deleted or missing objects
- [ ] Old rules with no recent update (age)
- [ ] Permissive intra-zone communication

**NAT and exposure**
- [ ] Port forwards exposing risky services (RDP, SMB, Telnet, FTP, VNC, databases, SSH, HTTP management) to the internet
- [ ] NAT policies with no matching restriction on source
- [ ] Stale or disabled NAT policies

**Management exposure**
- [ ] HTTPS, SSH, SNMP, ping or HTTP management enabled on WAN interfaces
- [ ] HTTP (not HTTPS) management or user login enabled
- [ ] Default admin account name still in use, or no admin lockout
- [ ] Weak or missing password policy, long or no session timeout
- [ ] Admin accounts without MFA (where determinable)
- [ ] SNMP v1 or v2c with community strings, or default community
- [ ] SonicOS API enabled with permissive CORS
- [ ] Management access allowed from any address

**VPN**
- [ ] Weak encryption (DES, 3DES), weak hashes (MD5, SHA1)
- [ ] Weak DH groups (1, 2, 5)
- [ ] Aggressive mode with pre-shared keys
- [ ] PFS disabled
- [ ] Very long or very short lifetimes
- [ ] IKEv1 where IKEv2 is available
- [ ] Disabled VPN policies still present
- [ ] GroupVPN or default settings in use
- [ ] Remote access without MFA, broad access to all networks
- [ ] Policies that allow NetBIOS or multicast without need

**Zones and security services**
- [ ] Zones with GAV, IPS, Anti-Spyware, App Control or CFS off where traffic is routed through
- [ ] DPI-SSL not enabled where expected
- [ ] Botnet and GeoIP filtering off
- [ ] Guest or wireless zones with access to trusted zones
- [ ] Flood protection or anti-spoofing disabled

**Users and authentication**
- [ ] Local users with admin rights
- [ ] Stale or unused local users, accounts with no recent change
- [ ] Guest accounts enabled
- [ ] Shared or generic account names

**Device and lifecycle**
- [ ] Firmware older than the current supported release, or end of life
- [ ] Known vulnerable firmware builds (needs a maintained lookup: decide how to keep current)
- [ ] Expired or soon-to-expire licenses and certificates (if available)
- [ ] Time sync (NTP) not configured, logging or syslog not configured
- [ ] No HA on a critical site (informational)

**Output behavior**
- [ ] Findings configurable (enable or disable checks, adjust thresholds)
- [ ] Each finding links to the exact item in the main report
- [ ] Document each check and its rationale in the repo

---

## 6. Migration checklist page (P1)

Purpose: when a technician moves a client to a different platform, this is the nice checklist of everything that must be rebuilt, with real values filled in, tick boxes, and a clear marker for anything the export cannot give them.

**Format**
- [ ] A dedicated page or file that is printable and has tick boxes
- [ ] Each item shows: setting, current value from this device, a "captured from export" or "must be collected manually" flag, notes, and a done box
- [ ] Group by migration phase: pre-migration collection, build, cutover, post-cutover validation
- [ ] Platform neutral: describes the setting, not any vendor's menu path
- [ ] Items found in the export are pre-filled and counted. Items not present in the export appear as manual-collection prompts

**Sections and what to capture**
- [ ] **Device and licensing:** model, serial, firmware, registration, active subscriptions and expiry, support contract. (Often not in export: manual)
- [ ] **Interfaces:** name, zone, IP and mask, VLAN, role, management settings, MTU, speed and duplex, PortShield or switch ports
- [ ] **WAN and ISP:** addressing mode, static IPs, gateways, DNS, PPPoE credentials (manual), failover and load balancing order and probes
- [ ] **Routing:** static routes, policy routes, dynamic routing
- [ ] **Zones:** list, trust model, intra-zone rules, and what services each had
- [ ] **Objects:** address objects, groups, services, service groups, schedules (resolved values)
- [ ] **Firewall rules:** ordered, with source, destination, service, action, logging, schedule, users, profiles, comments, and which rules are unused or disabled (so the technician can skip them)
- [ ] **NAT:** port forwards, 1:1 NAT, outbound NAT, special cases, hairpin or reflexive NAT
- [ ] **Site-to-site VPNs:** peer addresses, local and remote networks, IKE version, Phase 1 and 2 crypto, lifetimes, IDs, PFS. Pre-shared keys and certificates are marked "obtain from the other side or the client"
- [ ] **Remote access VPN:** type, ports, auth source, user and group access, IP pools, routes, DNS, client software and licenses, MFA
- [ ] **Users and groups:** local users and groups, directory integration, RADIUS, SSO, MFA, admin accounts (passwords: manual)
- [ ] **DHCP and DNS:** scopes, ranges, options, reservations, relay, DNS servers, DDNS
- [ ] **Security services:** what was licensed and enabled, with settings and exclusions: GAV, IPS, Anti-Spyware, App Control, content filtering categories and overrides, DPI-SSL, Botnet, GeoIP, sandbox
- [ ] **Bandwidth and QoS:** objects, per-interface limits, rules using them
- [ ] **Wireless and switching:** SSIDs, security, VLANs, AP inventory
- [ ] **High availability:** pair details, virtual IPs, monitoring
- [ ] **Management and monitoring:** admin access settings, SNMP, syslog destinations, NTP, email alerts, NetFlow, backups, scheduled tasks
- [ ] **Certificates:** list and purpose (private keys: manual, may need re-issue)
- [ ] **Third-party dependencies:** services that rely on the firewall (VoIP and SIP settings, cameras, remote support, cloud services allow-listing, Wake-on-LAN, hairpin access)
- [ ] **Documentation and handoff:** current config backup stored, change window, rollback plan, contact list, cutover test list

**Validation list (post-cutover)**
- [ ] Internet access per VLAN
- [ ] Each site-to-site tunnel up and traffic passing
- [ ] Each port forward reachable from outside
- [ ] Remote access login and resource access
- [ ] DHCP leases, DNS resolution
- [ ] Printing, VoIP, line-of-business apps
- [ ] Content filter and security services working
- [ ] Logging, alerts and monitoring reaching their destinations

**Extras**
- [ ] Items flagged "do not migrate" (unused, disabled, duplicate, weak) pulled from the findings
- [ ] A short "differences to expect" section left generic for the technician to fill in per platform

---

## 7. Report layout and usefulness (P2)

**All formats**
- [ ] Executive summary first: device, firmware, counts, top findings by severity, coverage notes
- [ ] Group firewall rules by zone pair (LAN to WAN, WAN to LAN, and so on)
- [ ] Show resolved addresses next to object names
- [ ] Show logging, schedule and security profiles per rule
- [ ] Show last-hit date, rule age and the reset caveat beside hit counts
- [ ] Report which parser version and firmware generation produced the report
- [ ] Parse warnings and unparsed-groups section in every format (HTML only today)

**HTML**
- [ ] Sticky table of contents or sidebar navigation
- [ ] Sortable columns and per-table filters
- [ ] Global search expands matching sections, highlights hits, updates the badges
- [ ] Remember open and closed sections
- [ ] Severity badges for findings, linked to the rule or item
- [ ] Simple topology diagram (zones, interfaces, VPNs)
- [ ] Keep print styling working

**PDF**
- [ ] Letter size option (or default for US use)
- [ ] Larger readable font (7 pt is too small)
- [ ] Remove forced page breaks that leave near-empty pages
- [ ] Cover page, clickable table of contents, bookmarks
- [ ] Findings summary and parse-warning pages

**Markdown**
- [ ] Add diagnostics and findings sections to match HTML
- [ ] Escape pipes (see section 1)
- [ ] Replace one-table-per-VPN with a comparison table plus detail below
- [ ] Notion-compatible output check

---

## 8. Code quality and architecture (P2)

- [ ] Shared report model (see section 4) feeding all exporters
- [ ] Remove duplicated table definitions across exporters
- [ ] Per-version key maps (see section 2)
- [ ] Replace list `Contains` in `CountByPrefix` with a set
- [ ] Consistent null and empty handling, one helper set for "Any" and "None" display
- [ ] Clear separation: decode, extract, analyze, render
- [ ] XML docs on new public members, README project structure kept current

---

## 9. CLI and features (P2 to P3)

- [ ] CLI flags: choose formats, output names, quiet mode, include or exclude findings and migration pages
- [ ] Output file naming that avoids collisions
- [ ] JSON export of the full model and findings (for automation and tools like Notion)
- [ ] Redaction mode: mask IPs, names, serials, usernames for sharing
- [ ] Diff two exports: added, removed and changed items, with a change summary
- [ ] Batch mode: process a folder of exports and produce per-client reports plus a summary
- [ ] Baseline or policy comparison: check a device against a defined standard
- [ ] Optional: accept additional input formats (see section 2)

---

## 10. Tests and fixtures (cross-cutting, P0 alongside the work)

- [ ] Fixture library from section 2, stored in the test project
- [ ] Unit tests: every extractor, every enum mapping, decoding edge cases, the resolver, each finding check
- [ ] Integration tests: file to model to each report format using the fixtures
- [ ] End to end tests: run the real CLI against fixtures and check exit codes, output files and key content
- [ ] Golden-file tests for report output so unintended layout changes are caught
- [ ] Negative tests: corrupt files, unsupported versions, empty sections, huge files
- [ ] Add a test run to the existing release workflow instead of creating new workflows

---

## 11. Repo and release hygiene (P3)

- [ ] README: supported versions table, new pages and flags, anonymization guide, disclaimer kept
- [ ] CHANGELOG with each release
- [ ] Document the analysis checks and the migration checklist structure
- [ ] Confirm license and third-party license notes (QuestPDF community license terms)
- [ ] Contributing note on how to submit sanitized samples

---

## Suggested build order

1. Section 1 (bugs) and the loud-failure warning for empty sections
2. Section 2 (samples and version support), since everything else depends on correct parsing
3. Section 3 (coverage gaps) in the order rules, VPN, users and management, network, security services
4. Section 4 (resolver and shared model)
5. Section 5 (findings page) and section 6 (migration checklist), which both sit on top of 3 and 4
6. Section 7 (layout)
7. Sections 8 to 11 alongside, with section 10 running throughout
