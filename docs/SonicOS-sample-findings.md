# SonicOS Sample Findings

Based on three distinct real exports (the fourth upload was a byte-identical copy of the 6.5.4 file):

| Sample | Device | Build | Keys |
|--------|--------|-------|------|
| A | TZ 400 | 6.5.4.15-117n | 33,805 |
| B | TZ 370 | 7.1.2-7019-R6288 | 43,986 |
| C | TZ 470 | 7.3.2-7010-R9118 | 42,681 |

No SonicOS 8.x sample exists yet. Per your decision the supported range is 6.x to 8.x; 8.x will be accepted but marked "unverified" until a sample is available.

All findings below were measured from these files, except items listed under "Needs confirmation from the SonicOS UI".

## 1. Good news: the core scheme is stable across 6.5 to 7.3

The indexed families the parser reads for these sections exist in all three versions: address objects, service objects, firewall rules (IPv4 and IPv6), NAT (IPv4 and IPv6), zones, interfaces, VPN, users, user groups, group memberships, schedules, WAN load balancing, content filter policies, bandwidth objects, SSLVPN profiles and firmware history. About 3,640 key families are common to all three versions, out of roughly 4,800 distinct families overall.

That means the parser was built against real files and the version-specific work is a set of targeted fixes, not a rewrite.

## 2. Confirmed bugs (evidence from the files)

| Bug | Evidence | Fix |
|-----|----------|-----|
| **Serial number is always empty** | `prefsHistorySerialNumber` does not exist in any sample. The real keys are `serialNumber` and `serial` | Read `serialNumber`. Treat migration history as optional |
| **Migration and previous-device fields never populate** | `prefsHistory*` keys absent in all three | Keep as optional, only show when present |
| **DHCP scopes are always empty** | Code reads `prefs_dhdynIpStart` and similar. Real keys are `prefs_dhdynstart`, `prefs_dhdynend`, `prefs_dhdynrouter`, `prefs_dhdynsubnetmask`, `prefs_dhdynlease`, `prefs_dhdyndomainname`, `prefs_dhdyndns0` to `dns2`, `prefs_dhdynwins0` and `1`, `prefs_dhdynscopeactive`, `prefs_dhdynbootp`, plus several more | Rewrite the DHCP extractor with the real names |
| **Zone IPS flag is wrong on 7.x** | `zoneObjSecProfile` exists only in 6.5.4. It is absent in 7.1.2 and 7.3.2, so IPS always shows as off on 7.x | Find the 7.x source of the IPS state (see section 4) and show "unknown" until confirmed rather than a false "off" |
| **Zone Guest flag never read** | `zoneObjEnableGuestServices` exists in all three | Extract it |
| **Interface link speed always 0** | Code reads `eth_link_speed`. The real key is `eth_link_ability` (and `eth_link_ability_override` on 7.x) | Use the real keys |
| **"Default rule" logic is wrong in every version** | `policyDefaultRule` is a three-state value, not a boolean. In 6.5.4 and 7.1.2, user rules and auto-generated rules both carry `1`. In 7.3.2, the built-in zone-to-zone defaults carry `2` and are named "Default Access Rule" | Replace the boolean with a derived rule origin (see section 3) |
| **Unused-rule audit is therefore wrong** | On 6.5.4, almost every rule is excluded because it carries `1`. On 7.3.2, the built-in defaults carry `2`, so they are wrongly listed as unused cleanup candidates | Only evaluate custom rules |
| **NAT hit counts never read** | No `natPolicyHitCount` family exists in any sample | Remove the always-zero column. NAT hits are not in these exports |
| **Policy action label for value 0 is probably wrong** | Value 0 appears on the built-in WAN-to-LAN, WAN-to-DMZ and similar default rules, which SonicOS normally shows as Deny, but the code labels 0 as "Discard". Value 1 is never seen | Confirm in the UI (section 5), then fix the labels |
| **Crypto labels cannot be trusted for the IKEv2 policy** | The 7.3.2 file has IKE exchange `3`, encryption `251`, authentication `6` and DH `3`. The current tables have no entry for exchange 3 or encryption 251 | Make tables explicit, show "Unknown(n)" for unmapped values, and fill in after confirmation (section 5) |

## 3. Rule classification (derived, since the flag is unreliable)

A rule's origin should be derived from several fields, not from `policyDefaultRule` alone.

- **Auto-generated:** the comment starts with "Auto added", "Auto-added" or "Auto generated" (these also carry typical `policyProps` values such as 6159, 2063, 8213, 16777213, 268435453).
- **Built-in default zone rule:** any source, any destination and any service, and either the comment starts with "IPv4:From Any to Any" or the name is "Default Access Rule" with `policyDefaultRule` of 2.
- **Custom rule:** everything else. On 7.1.2 these are typically named "Custom Access Rule" or a user-chosen name. On 6.5.4 the name is blank, so they are identified by having specific objects and no auto comment.

Result on the samples: 6.5.4 has 8 custom rules, 7.1.2 has 13, and 7.3.2 has none (all 91 rules are auto-generated or defaults). Only custom rules should feed unused-rule and cleanup findings.

## 4. Version differences worth handling

- **Rules gain many fields on 7.x.** New families include Network Security options (`policyNg*`), SAML authentication (`policySamlAuth*`), and GeoIP and botnet block flags. Present in all versions: logging, schedule (`policyTime`), user/group (`policyWhom`), connection limits (`policyConnPercent`, `policyConnLmt*`), bandwidth management (`policyBwm*`), DSCP and 802.1p tagging, SIP and H.323 helpers, DPI bypass flags, and `policyPriType`.
- **Zone security profiles.** 7.x adds a default-options bitmask (`zoneObjDefaultRuleOptions` is 15 on 7.x, 0 on 6.5.4). The IPS state on 7.x is not exposed under the old key. See section 5.
- **Service objects.** Service groups carry IP type `0` on 6.5.4 but `171` (7.1.2) or `170` (7.3.2) on 7.x. The group flag (`svcObjType` of 2) is consistent, so protocol detection must key off the type, not the IP type.
- **VPN.** 7.3.2 adds IKEv2 policies (exchange value 3). Version, name, peer gateway (`ipsecGwAddr`, `ipsecSecGwAddr`) and enabled state (`ipsecSaDisabled`) all exist in every sample but are not read today. The default GroupVPN policies show `ipsecSaDisabled` as `on`, which matches "disabled by default".
- **Users.** User objects carry TOTP and password fields (`userObjCryptPass`, `userObjCryptTotpKey`, `userObjOtpReq`). Read only the harmless fields. The privilege mask is 0 for all 43 users in sample C, so no decoding can be validated from these files.
- **Schedules.** Day-of-week values such as 127 (all days), 62 (weekdays) and 65 (weekend) show a 7-bit day mask. Type 1 and 2 both appear, and a few values are very large (seen on type 2 only).
- **Interfaces.** Unassigned ports have an empty zone. WAN mode keys exist (`iface_pppoe_*`, `iface_dhcp_*`, `iface_wire_mode`, `iface_one_arm_mode`) and are not read.
- **Firmware history** grows with the device's age: 2 entries on 6.5.4, 3 on 7.1.2, 7 on 7.3.2.

## 5. Needs confirmation from the SonicOS UI

Please confirm these from the web UI of the matching device so the mappings are not guessed:

1. **Policy action for the built-in WAN-to-LAN default rule:** does the UI show Deny or Discard? (Export value is 0. Allow is 2.)
2. **VPN policy on sample C (TZ 470, 7.3.2) that uses IKEv2** (export exchange 3, encryption 251, authentication 6, DH 3): what does the UI show for IKE version, DH group, encryption, authentication, and the Phase 2 encryption and authentication?
3. **A default site-to-site policy on any sample** (export exchange 2, DH 1, encryption 3, authentication 3 on all three files): does the UI show Aggressive Mode, DH Group 2, 3DES, SHA1? That would tell us whether authentication 3 is SHA1 or SHA-256, and what DH 1 means.
4. **Zone IPS on 7.x:** on the TZ 470 or TZ 370, which zones show Intrusion Prevention enabled? The new options bitmask and per-zone values may encode it.

## 6. Data hygiene (important)

The sample files contain real client data: IP addresses and subnets, a domain name, user names, VPN names, the device serial, firmware dates and rule comments. Fixtures committed to the repo must be synthetic or fully sanitized, never copies of these files.

The raw exports also contain secrets. The parser must never read or print any of them:

- Interface PPPoE credentials (`iface_pppoe_username` and `iface_pppoe_password`).
- User password hashes and TOTP data (`userObjCryptPass`, `userObjCryptTotpKey`, `userObjTotpScratchCode`, `userObjClearPassCryptSecrt`).
- Zone RADIUS, LDAP and Active Directory secrets (`zoneObjRadClientSecr`, `zoneObjIdentityDNSecr`, `zoneObjADAdminUserPassword`, `zoneObjExtAAAMsgAuthSecret`).
- Admin TOTP and ClearPass fields (`adminObjCryptTotpKey` and related).
- NTP server passwords (`NTPServerPassword`) and VPN keys (`ipsecESPKey`, `ipsecAHKey`).

Add a hard deny-list in the extractor so secret keys can never reach any model or report, and a test that proves it.

## 7. Settings now confirmed present (good material for findings and the migration checklist)

Management and admin hardening: login failure lockout (`loginFailLockoutEnable`, `loginFailMaxRate`, `loginFailLockoutTime`), password complexity and age (`passwordComplexity`, `passwordUpdtPeriod`, `pwd*Num`), admin login timeout, admin OTP requirement, CLI login attempts, SonicOS API settings. Services: NTP (`ntp_*`), syslog (`syslogServerName` and port), SSLVPN global settings (`sslvpn*`, `Sslvpns*`), certificate expiry alert, service license counts and expiry (`ceSvcLicenseCount`, `ceSvcExpiration`), OSPF router IDs, route policy options, GAV and anti-spyware action objects (`peActObj*`), and per-rule GeoIP and botnet flags. DHCP scope details, firmware history and the WAN addressing mode keys are also present.

## 8. Coverage notes

- Two 6.x samples were identical, so only one distinct 6.x export has been seen. Samples from 6.0 to 6.4, other models (NSa, NSsp) and an HA pair would strengthen the 6.x range.
- 8.x is unverified.
- Everything above was measured with Python against the raw exports. Nothing has been compiled or run through the .NET tool yet (see the build blocker explained in the chat).
