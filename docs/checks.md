# Security checks

This file is generated from the check registry. Do not edit it by hand.
Regenerate it with `SonicWallParser --list-checks > docs/checks.md`.

Each check has a stable id. Disable checks with `--skip-checks R004,R005`. Severity shown is the usual one;
some checks raise or lower it per item (for example by zone or by how broad a rule is).
Checks that depend on a value mapping not yet confirmed in the SonicOS UI are marked Unverified mapping in the report.

## Firewall rules

### R001: Any-to-any allow rule

- **Usual severity:** High
- **Why it matters:** A rule that allows any source to any destination on any service removes the firewall's value between those zones. From the internet it exposes everything behind the firewall.
- **Suggested fix:** Replace it with rules that name the sources, destinations and services that are actually needed.

### R002: Inbound allow from any internet address

- **Usual severity:** Medium
- **Why it matters:** A rule that accepts traffic from any address on the WAN exposes the destination to the whole internet.
- **Suggested fix:** Limit the source to known addresses or a geography where possible, and make sure the service is hardened and patched.

### R003: Allow rule with any service

- **Usual severity:** Low
- **Why it matters:** A rule that allows every service gives more access than needed and makes the rule hard to review.
- **Suggested fix:** List the specific services required, or use a service group.

### R004: Allow rule without logging

- **Usual severity:** Low
- **Why it matters:** Without logging there is no record of what the rule permitted, which hurts investigation and auditing.
- **Suggested fix:** Enable logging on the rule, at least for rules that expose services or cross a trust boundary.

### R005: Rule without a name or comment

- **Usual severity:** Info
- **Why it matters:** A rule nobody can explain is hard to review and tends to survive long after its purpose is gone.
- **Suggested fix:** Give the rule a name or comment that states its purpose and owner.

### R006: Disabled rule still present

- **Usual severity:** Info
- **Why it matters:** Disabled rules clutter the policy and can be re-enabled by mistake.
- **Suggested fix:** Delete rules that are no longer needed, after confirming with the owner.

### R007: Shadowed or redundant rule

- **Usual severity:** Medium
- **Why it matters:** A rule that an earlier rule already covers can never match, so it either does nothing or hides a mistake.
- **Suggested fix:** Remove the redundant rule, or reorder the rules so the intended one is evaluated first.

### R008: Custom rule with zero hits

- **Usual severity:** Info
- **Why it matters:** A rule that has never matched may be obsolete. Hit counters reset on reboot or when cleared, so this is a reason to review, not to delete.
- **Suggested fix:** Confirm with the owner whether the rule is still needed, and check logs over a longer period before removing it.

### R009: Reference to an object that does not exist

- **Usual severity:** Medium
- **Why it matters:** A rule, NAT or VPN policy that names a missing object may not behave as intended.
- **Suggested fix:** Recreate the object or correct the reference.

### R010: Custom rule not changed for a long time

- **Usual severity:** Info
- **Why it matters:** Old rules are more likely to reflect a retired system or a forgotten exception.
- **Suggested fix:** Review the rule with its owner and record the review.

### R011: Intra-zone traffic allowed in an exposed zone

- **Usual severity:** Low
- **Why it matters:** Allowing hosts in the same DMZ or wireless zone to talk to each other lets one compromised host reach the others.
- **Suggested fix:** Disable intra-zone traffic unless the hosts need it, or use rules to control it.

## NAT and exposure

### N001: Risky service published to the internet

- **Usual severity:** High
- **Why it matters:** Services such as RDP, SMB, Telnet and databases are heavily attacked when reachable from the internet.
- **Suggested fix:** Remove the port forward and use a VPN, or restrict the source addresses and enable strong authentication.

### N002: Inbound NAT policy with no service restriction

- **Usual severity:** Medium
- **Why it matters:** A port forward that does not name a service forwards every port to the internal host.
- **Suggested fix:** Limit the NAT policy to the specific service that must be published.

### N003: Disabled NAT policy still present

- **Usual severity:** Info
- **Why it matters:** Disabled NAT policies clutter the configuration and can be re-enabled by mistake.
- **Suggested fix:** Delete NAT policies that are no longer needed.

## Management exposure

### M001: Management service enabled on a WAN interface

- **Usual severity:** High
- **Why it matters:** Administration reachable from the internet is a common way into firewalls.
- **Suggested fix:** Disable HTTPS, SSH and SNMP management on WAN interfaces, and manage the device from the LAN or over a VPN.

### M002: Unencrypted HTTP management or login enabled

- **Usual severity:** Medium
- **Why it matters:** HTTP sends credentials and sessions without encryption.
- **Suggested fix:** Disable HTTP management and HTTP user login, and use HTTPS.

### M003: Admin login lockout disabled

- **Usual severity:** Medium
- **Why it matters:** Without lockout an attacker can guess admin passwords without limit.
- **Suggested fix:** Enable login failure lockout with a sensible threshold.

### M004: Short minimum admin password length

- **Usual severity:** Medium
- **Why it matters:** Short passwords are easy to guess or crack.
- **Suggested fix:** Require a longer minimum password length.

### M005: SonicOS API enabled

- **Usual severity:** Low
- **Why it matters:** The API is another management surface. Permissive CORS widens who can call it from a browser.
- **Suggested fix:** Disable the API if unused, and turn off CORS unless a known web application needs it.

### M006: API basic authentication enabled

- **Usual severity:** Medium
- **Why it matters:** Basic authentication sends reusable credentials with every request.
- **Suggested fix:** Use a stronger API authentication method and disable basic authentication.

### M007: Admin login does not require a one-time password

- **Usual severity:** Medium
- **Why it matters:** A password alone is a single point of failure for the most privileged accounts.
- **Suggested fix:** Require multi-factor authentication for administrators.

### M008: Management allowed over SSLVPN

- **Usual severity:** Medium
- **Why it matters:** Allowing web or SSH management from SSLVPN sessions extends the admin surface to every remote user's device.
- **Suggested fix:** Disable management over SSLVPN unless required, and restrict it to administrators.

### M009: SNMP enabled

- **Usual severity:** Info
- **Why it matters:** SNMP v1 and v2c send community strings in clear text. Community strings are never read by this tool, so their strength cannot be judged.
- **Suggested fix:** Use SNMPv3 with authentication and encryption, restrict the allowed sources, and avoid default community names.

## VPN

### V001: Weak VPN encryption

- **Usual severity:** Medium
- **Why it matters:** DES and 3DES are obsolete and weak by modern standards.
- **Suggested fix:** Use AES-128 or AES-256 (preferably AES-GCM where supported).

### V002: Weak VPN authentication hash

- **Usual severity:** Medium
- **Why it matters:** MD5 and SHA1 are considered weak.
- **Suggested fix:** Use SHA-256 or stronger.

### V003: Weak Diffie-Hellman group

- **Usual severity:** Medium
- **Why it matters:** Groups 1, 2 and 5 are too small to resist modern attacks.
- **Suggested fix:** Use group 14 (2048-bit) or higher, or an elliptic curve group.

### V004: Aggressive mode in use

- **Usual severity:** Medium
- **Why it matters:** Aggressive mode exposes more of the handshake, and with a pre-shared key it allows offline guessing. Whether a pre-shared key is used cannot be seen, because keys are never read.
- **Suggested fix:** Use main mode, or IKEv2, with a long random key or certificates.

### V005: Perfect forward secrecy disabled

- **Usual severity:** Medium
- **Why it matters:** Without PFS, recording the traffic and later obtaining the key exposes past sessions.
- **Suggested fix:** Enable PFS on the Phase 2 settings.

### V006: Very long phase 1 lifetime

- **Usual severity:** Low
- **Why it matters:** Long lifetimes keep the same keys in use for longer.
- **Suggested fix:** Use a lifetime of 24 hours or less.

### V007: Disabled VPN policy still present

- **Usual severity:** Info
- **Why it matters:** Disabled VPN policies clutter the configuration and keep old peer details around.
- **Suggested fix:** Delete policies that are no longer needed.

### V008: GroupVPN policy in use

- **Usual severity:** Medium
- **Why it matters:** GroupVPN policies are often left with default or shared settings and broad access.
- **Suggested fix:** Prefer SSLVPN or per-user authentication with multi-factor, and review the access granted.

### V009: NetBIOS or multicast allowed over a VPN

- **Usual severity:** Low
- **Why it matters:** Broadcast style traffic across a tunnel is rarely needed and increases exposure.
- **Suggested fix:** Disable it unless an application needs it.

## Zones and security services

### Z001: Security services off on a zone

- **Usual severity:** Low
- **Why it matters:** Gateway antivirus, anti-spyware, application control and content filtering are applied per zone. A zone with them off gets no inspection (if licensed).
- **Suggested fix:** Enable the services you are licensed for on LAN, DMZ and wireless zones.

### Z002: Wireless or guest zone can reach a trusted zone

- **Usual severity:** Medium
- **Why it matters:** Guest and wireless devices are less trusted. Allowing them into the LAN undermines segmentation.
- **Suggested fix:** Remove or narrow the rule so guest and wireless clients can reach only the internet and required services.

## Users and authentication

### U001: Guest account enabled

- **Usual severity:** Low
- **Why it matters:** Guest accounts are shared or loosely controlled access.
- **Suggested fix:** Disable guest accounts that are not needed.

### U002: Shared or generic account name

- **Usual severity:** Low
- **Why it matters:** Shared accounts make it impossible to tell who did what.
- **Suggested fix:** Replace generic accounts with named accounts.

## Device and lifecycle

### D001: Firmware generation is unverified or unknown

- **Usual severity:** Info
- **Why it matters:** Parsing of this firmware generation has not been tested against real exports, so the report may be incomplete.
- **Suggested fix:** Confirm the report against the SonicOS UI.

### D002: NTP time sync not enabled

- **Usual severity:** Low
- **Why it matters:** Wrong time breaks log correlation, certificate validation and schedules.
- **Suggested fix:** Enable NTP with reliable servers.

### D003: No syslog server configured

- **Usual severity:** Low
- **Why it matters:** Logs kept only on the device can be lost or altered.
- **Suggested fix:** Send logs to a syslog server or SIEM.

### D004: Service license expired or expiring

- **Usual severity:** Medium
- **Why it matters:** Expired security services stop protecting, and support may no longer be available.
- **Suggested fix:** Renew the license or plan the replacement.

## Not assessed

These cannot be judged from an export. The report lists them so a gap is never mistaken for a pass.

- Admin accounts, admin rights and per-account multi-factor (privilege masks are not decoded).
- SNMP community strings (credentials, never read).
- Known vulnerable or end-of-life firmware (needs a maintained lookup).
- Certificate expiry.
- Security profiles applied per rule (keys unconfirmed).
- Flood protection and anti-spoofing.
- IKE version in use and pre-shared key use.
- Remote access VPN multi-factor and breadth of access.
- High availability.
- Intrusion prevention per zone on SonicOS 7.x (source unconfirmed).
