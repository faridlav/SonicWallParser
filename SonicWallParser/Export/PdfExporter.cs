using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SonicWallParser.Models;

namespace SonicWallParser.Export;

/// <summary>
/// Generates a professional landscape PDF report from the SonicWall configuration using QuestPDF.
/// </summary>
public static class PdfExporter
{
    private static readonly string HdrBg = "#1B2A4A";
    private static readonly string HdrFg = "#FFFFFF";
    private static readonly string RowAlt = "#F0F4FA";
    private static readonly string AccentAllow = "#2E7D32";
    private static readonly string AccentDeny = "#C62828";
    private static readonly string SectionColor = "#1B2A4A";
    private static readonly string SubSectionColor = "#2C5282";

    /// <summary>
    /// Generates a PDF report at the specified path from the given SonicWall configuration.
    /// </summary>
    public static void Generate(SonicWallConfig config, string outputPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("DejaVu Sans"));

                page.Header().Element(c => ComposeHeader(c, config));

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    if (config.FirmwareHistory.Count > 0 || !string.IsNullOrEmpty(config.Global.PreviousProduct))
                    {
                        col.Item().Element(c => SectionTitle(c, "Device & Firmware History"));
                        col.Item().Element(c => DeviceHistoryContent(c, config));
                    }

                    col.Item().Element(c => SectionTitle(c, "Zones"));
                    col.Item().Element(c => ZonesTable(c, config.Zones));

                    col.Item().PageBreak();
                    col.Item().Element(c => SectionTitle(c, "Interfaces"));
                    col.Item().Element(c => InterfacesTable(c, config.Interfaces));

                    col.Item().PageBreak();
                    col.Item().Element(c => SectionTitle(c, "Address Objects"));
                    col.Item().Element(c => AddressObjectsTable(c, config.AddressObjects));

                    if (config.AddressGroupMemberships.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "Address Object Group Memberships"));
                        col.Item().Element(c => GroupMembershipsTable(c, config.AddressGroupMemberships));
                    }

                    col.Item().PageBreak();
                    col.Item().Element(c => SectionTitle(c, "Service Objects"));
                    col.Item().Element(c => ServiceObjectsTable(c, config.ServiceObjects));

                    if (config.ServiceGroupMemberships.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "Service Object Group Memberships"));
                        col.Item().Element(c => GroupMembershipsTable(c, config.ServiceGroupMemberships));
                    }

                    if (config.Schedules.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "Schedule Objects"));
                        col.Item().Element(c => SchedulesTable(c, config.Schedules));
                    }

                    col.Item().PageBreak();
                    col.Item().Element(c => SectionTitle(c, "Firewall Policies (IPv4)"));
                    col.Item().Element(c => FirewallPoliciesTable(c, config.FirewallPolicies));

                    if (config.FirewallPoliciesV6.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "Firewall Policies (IPv6)"));
                        col.Item().Element(c => FirewallPoliciesV6Table(c, config.FirewallPoliciesV6));
                    }

                    col.Item().PageBreak();
                    col.Item().Element(c => SectionTitle(c, "NAT Policies (IPv4)"));
                    col.Item().Element(c => NatPoliciesTable(c, config.NatPolicies));

                    if (config.NatPoliciesV6.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "NAT Policies (IPv6)"));
                        col.Item().Element(c => NatPoliciesV6Table(c, config.NatPoliciesV6));
                    }

                    if (config.VpnPolicies.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "VPN Policies (IPSec)"));
                        col.Item().Element(c => VpnPoliciesContent(c, config.VpnPolicies));
                    }

                    if (config.Users.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "Local Users"));
                        col.Item().Element(c => UsersTable(c, config.Users));
                    }

                    if (config.UserGroups.Count > 0)
                    {
                        col.Item().Element(c => SectionTitle(c, "User Groups"));
                        col.Item().Element(c => UserGroupsTable(c, config.UserGroups));
                    }

                    if (config.UserGroupMemberships.Count > 0)
                    {
                        col.Item().Element(c => SectionTitle(c, "User → Group Memberships"));
                        col.Item().Element(c => GroupMembershipsTable(c, config.UserGroupMemberships));
                    }

                    if (config.DhcpScopes.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "DHCP Server Scopes"));
                        col.Item().Element(c => DhcpContent(c, config.DhcpScopes));
                    }

                    if (config.WanLbGroups.Count > 0 || config.WanLbMembers.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "WAN Load Balancing"));
                        if (config.WanLbGroups.Count > 0)
                            col.Item().Element(c => WanLbGroupsTable(c, config.WanLbGroups));
                        if (config.WanLbMembers.Count > 0)
                            col.Item().Element(c => WanLbMembersTable(c, config.WanLbMembers));
                    }

                    if (config.ContentFilterPolicies.Count > 0)
                    {
                        col.Item().Element(c => SectionTitle(c, "Content Filter Policies"));
                        col.Item().Element(c => CfsPoliciesTable(c, config.ContentFilterPolicies));
                    }

                    if (config.BandwidthObjects.Count > 0)
                    {
                        col.Item().Element(c => SectionTitle(c, "Bandwidth Management Objects"));
                        col.Item().Element(c => BandwidthObjectsTable(c, config.BandwidthObjects));
                    }

                    if (config.NacProfiles.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "SSLVPN / NetExtender Profiles"));
                        col.Item().Element(c => NacProfilesTable(c, config.NacProfiles));
                    }

                    var unused = config.FirewallPolicies
                        .Where(p => p.HitCount == 0 && p.TimeLastHit == DateTime.MinValue && !p.IsDefault)
                        .ToList();
                    if (unused.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => SectionTitle(c, "⚠ Unused Firewall Rules (0 Hits)"));
                        col.Item().PaddingBottom(4).Text("Non-default rules that have never matched traffic.")
                            .FontSize(7).Italic().FontColor("#666666");
                        col.Item().Element(c => UnusedRulesTable(c, unused));
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Page ").FontSize(7);
                        t.CurrentPageNumber().FontSize(7);
                        t.Span(" of ").FontSize(7);
                        t.TotalPages().FontSize(7);
                    });
            });
        }).GeneratePdf(outputPath);
    }


    private static void ComposeHeader(IContainer container, SonicWallConfig config)
    {
        container.PaddingBottom(10).Column(col =>
        {
            col.Item().Text("SonicWall Configuration Report").FontSize(16).Bold().FontColor(HdrBg);
            col.Item().Text($"{config.Global.ProductName}  •  Firmware {config.Global.BuildNumber}  •  " +
                            $"Serial {config.Global.SerialNumber}  •  " +
                            $"Generated {DateTime.Now:yyyy-MM-dd HH:mm}")
                .FontSize(8).FontColor("#666666");
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#CCCCCC");
        });
    }

    private static void SectionTitle(IContainer container, string title)
    {
        container.PaddingBottom(4).PaddingTop(8)
            .Text(title).FontSize(12).Bold().FontColor(SectionColor);
    }

    private static void SubSectionTitle(IContainer container, string title)
    {
        container.PaddingBottom(2).PaddingTop(6)
            .Text(title).FontSize(10).Bold().FontColor(SubSectionColor);
    }


    private static void HC(IContainer c, string text) =>
        c.Background(HdrBg).Padding(4).Text(text).FontSize(7).Bold().FontColor(HdrFg);

    private static void DC(IContainer c, string text, int row) =>
        c.Background(row % 2 == 1 ? RowAlt : "#FFFFFF").Padding(4)
         .Text(text ?? string.Empty).FontSize(7);

    private static void ActionCell(IContainer c, string label, int action, int row) =>
        c.Background(row % 2 == 1 ? RowAlt : "#FFFFFF").Padding(4)
         .Text(label).FontSize(7).Bold()
         .FontColor(action == 2 ? AccentAllow : AccentDeny);


    private static void DeviceHistoryContent(IContainer container, SonicWallConfig config)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrEmpty(config.Global.PreviousProduct))
            {
                col.Item().Text($"Migrated from {config.Global.PreviousProduct} " +
                                $"(firmware {config.Global.PreviousBuild}) " +
                                $"on {config.Global.MigrationTimestamp}")
                    .FontSize(8);
            }

            if (config.FirmwareHistory.Count > 0)
            {
                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });
                    table.Header(h => { h.Cell().Element(c => HC(c, "Firmware")); h.Cell().Element(c => HC(c, "Date")); });
                    for (int i = 0; i < config.FirmwareHistory.Count; i++)
                    {
                        var fw = config.FirmwareHistory[i]; int r = i;
                        table.Cell().Element(c => DC(c, fw.BuildNumber, r));
                        table.Cell().Element(c => DC(c, fw.Timestamp, r));
                    }
                });
            }
        });
    }

    private static void ZonesTable(IContainer container, List<ZoneObject> zones)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1);
                c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "Zone")); h.Cell().Element(c => HC(c, "Type"));
                h.Cell().Element(c => HC(c, "Intra-Zone")); h.Cell().Element(c => HC(c, "GAV"));
                h.Cell().Element(c => HC(c, "IPS")); h.Cell().Element(c => HC(c, "App Ctrl"));
                h.Cell().Element(c => HC(c, "Anti-Spy")); h.Cell().Element(c => HC(c, "DPI-SSL"));
                h.Cell().Element(c => HC(c, "CFS"));
            });
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i]; int r = i;
                table.Cell().Element(c => DC(c, z.Name, r)); table.Cell().Element(c => DC(c, z.ZoneTypeLabel, r));
                table.Cell().Element(c => DC(c, YN(z.IntraZoneCommunication), r));
                table.Cell().Element(c => DC(c, YN(z.GatewayAntivirus), r));
                table.Cell().Element(c => DC(c, YN(z.IntrusionPrevention), r));
                table.Cell().Element(c => DC(c, YN(z.AppControl), r));
                table.Cell().Element(c => DC(c, YN(z.AntiSpyware), r));
                table.Cell().Element(c => DC(c, YN(z.DpiSslClient), r));
                table.Cell().Element(c => DC(c, YN(z.ContentFilter), r));
            }
        });
    }

    private static void InterfacesTable(IContainer container, List<InterfaceConfig> ifaces)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2);
                c.RelativeColumn(2); c.RelativeColumn(1); c.ConstantColumn(30); c.ConstantColumn(30);
                c.ConstantColumn(30); c.ConstantColumn(30);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "If")); h.Cell().Element(c => HC(c, "Description"));
                h.Cell().Element(c => HC(c, "Zone")); h.Cell().Element(c => HC(c, "IP Address"));
                h.Cell().Element(c => HC(c, "Subnet")); h.Cell().Element(c => HC(c, "VLAN"));
                h.Cell().Element(c => HC(c, "HTTPS")); h.Cell().Element(c => HC(c, "SSH"));
                h.Cell().Element(c => HC(c, "Ping")); h.Cell().Element(c => HC(c, "SNMP"));
            });
            for (int i = 0; i < ifaces.Count; i++)
            {
                var f = ifaces[i]; int r = i;
                var vlan = f.VlanTag > 0 ? f.VlanTag.ToString() : "—";
                table.Cell().Element(c => DC(c, f.Name, r)); table.Cell().Element(c => DC(c, f.Comment, r));
                table.Cell().Element(c => DC(c, f.ZoneName, r)); table.Cell().Element(c => DC(c, f.ActiveIp, r));
                table.Cell().Element(c => DC(c, f.ActiveMask, r)); table.Cell().Element(c => DC(c, vlan, r));
                table.Cell().Element(c => DC(c, YN(f.HttpsMgmt), r)); table.Cell().Element(c => DC(c, YN(f.SshMgmt), r));
                table.Cell().Element(c => DC(c, YN(f.PingMgmt), r)); table.Cell().Element(c => DC(c, YN(f.SnmpMgmt), r));
            }
        });
    }

    private static void AddressObjectsTable(IContainer container, List<AddressObject> objects)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            { c.ConstantColumn(30); c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(3); c.RelativeColumn(1); });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Name"));
                h.Cell().Element(c => HC(c, "Type")); h.Cell().Element(c => HC(c, "Address"));
                h.Cell().Element(c => HC(c, "Zone"));
            });
            for (int i = 0; i < objects.Count; i++)
            {
                var ao = objects[i]; int r = i;
                table.Cell().Element(c => DC(c, ao.Index.ToString(), r));
                table.Cell().Element(c => DC(c, ao.Name, r)); table.Cell().Element(c => DC(c, ao.TypeLabel, r));
                table.Cell().Element(c => DC(c, ao.FormattedAddress, r)); table.Cell().Element(c => DC(c, ao.Zone, r));
            }
        });
    }

    private static void ServiceObjectsTable(IContainer container, List<ServiceObject> objects)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            { c.ConstantColumn(30); c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(2); });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Name"));
                h.Cell().Element(c => HC(c, "Protocol")); h.Cell().Element(c => HC(c, "Ports"));
            });
            for (int i = 0; i < objects.Count; i++)
            {
                var s = objects[i]; int r = i;
                table.Cell().Element(c => DC(c, s.Index.ToString(), r)); table.Cell().Element(c => DC(c, s.Name, r));
                table.Cell().Element(c => DC(c, s.Protocol, r)); table.Cell().Element(c => DC(c, s.PortRange, r));
            }
        });
    }

    private static void GroupMembershipsTable(IContainer container, List<GroupMembership> memberships)
    {
        var sorted = memberships.OrderBy(g => g.Group).ToList();
        container.Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(1); });
            table.Header(h => { h.Cell().Element(c => HC(c, "Group")); h.Cell().Element(c => HC(c, "Member")); });
            for (int i = 0; i < sorted.Count; i++)
            {
                var gm = sorted[i]; int r = i;
                table.Cell().Element(c => DC(c, gm.Group, r)); table.Cell().Element(c => DC(c, gm.Member, r));
            }
        });
    }

    private static void SchedulesTable(IContainer container, List<ScheduleObject> schedules)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.RelativeColumn(4); c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); 
                h.Cell().Element(c => HC(c, "Name"));
                h.Cell().Element(c => HC(c, "Time Window"));
            });
            for (int i = 0; i < schedules.Count; i++)
            {
                var s = schedules[i]; int r = i;
                table.Cell().Element(c => DC(c, s.Index.ToString(), r));
                table.Cell().Element(c => DC(c, s.Name, r));
                table.Cell().Element(c => DC(c, s.TimeWindow, r));
            }
        });
    }

    private static void FirewallPoliciesTable(IContainer container, List<FirewallPolicy> policies)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.ConstantColumn(45); c.RelativeColumn(1); c.RelativeColumn(1);
                c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.ConstantColumn(30);
                c.ConstantColumn(55); c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Action"));
                h.Cell().Element(c => HC(c, "Src Zone")); h.Cell().Element(c => HC(c, "Dst Zone"));
                h.Cell().Element(c => HC(c, "Source")); h.Cell().Element(c => HC(c, "Dest"));
                h.Cell().Element(c => HC(c, "Service")); h.Cell().Element(c => HC(c, "On"));
                h.Cell().Element(c => HC(c, "Hits")); h.Cell().Element(c => HC(c, "Comment"));
            });
            for (int i = 0; i < policies.Count; i++)
            {
                var p = policies[i]; int r = i;
                table.Cell().Element(c => DC(c, p.Index.ToString(), r));
                table.Cell().Element(c => ActionCell(c, p.ActionLabel, p.Action, r));
                table.Cell().Element(c => DC(c, p.SourceZone, r)); table.Cell().Element(c => DC(c, p.DestZone, r));
                table.Cell().Element(c => DC(c, OrAny(p.SourceNet), r));
                table.Cell().Element(c => DC(c, OrAny(p.DestNet), r));
                table.Cell().Element(c => DC(c, OrAny(p.DestSvc), r));
                table.Cell().Element(c => DC(c, YN(p.Enabled), r));
                table.Cell().Element(c => DC(c, p.HitCount.ToString("N0"), r));
                table.Cell().Element(c => DC(c, p.Comment, r));
            }
        });
    }

    private static void FirewallPoliciesV6Table(IContainer container, List<FirewallPolicyV6> policies)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.ConstantColumn(45); c.RelativeColumn(1); c.RelativeColumn(1);
                c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.ConstantColumn(30);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Action"));
                h.Cell().Element(c => HC(c, "Src Zone")); h.Cell().Element(c => HC(c, "Dst Zone"));
                h.Cell().Element(c => HC(c, "Source")); h.Cell().Element(c => HC(c, "Dest"));
                h.Cell().Element(c => HC(c, "Service")); h.Cell().Element(c => HC(c, "On"));
                h.Cell().Element(c => HC(c, "Comment"));
            });
            for (int i = 0; i < policies.Count; i++)
            {
                var p = policies[i]; int r = i;
                table.Cell().Element(c => DC(c, p.Index.ToString(), r));
                table.Cell().Element(c => ActionCell(c, p.ActionLabel, p.Action, r));
                table.Cell().Element(c => DC(c, p.SourceZone, r)); table.Cell().Element(c => DC(c, p.DestZone, r));
                table.Cell().Element(c => DC(c, OrAny(p.SourceNet), r));
                table.Cell().Element(c => DC(c, OrAny(p.DestNet), r));
                table.Cell().Element(c => DC(c, OrAny(p.DestSvc), r));
                table.Cell().Element(c => DC(c, YN(p.Enabled), r));
                table.Cell().Element(c => DC(c, p.Comment, r));
            }
        });
    }

    private static void NatPoliciesTable(IContainer container, List<NatPolicy> policies)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2);
                c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.ConstantColumn(30);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Orig Src"));
                h.Cell().Element(c => HC(c, "Orig Dst")); h.Cell().Element(c => HC(c, "Orig Svc"));
                h.Cell().Element(c => HC(c, "Trans Src")); h.Cell().Element(c => HC(c, "Trans Dst"));
                h.Cell().Element(c => HC(c, "Trans Svc")); h.Cell().Element(c => HC(c, "On"));
                h.Cell().Element(c => HC(c, "Comment"));
            });
            for (int i = 0; i < policies.Count; i++)
            {
                var n = policies[i]; int r = i;
                table.Cell().Element(c => DC(c, n.Index.ToString(), r));
                table.Cell().Element(c => DC(c, OrAny(n.OriginalSource), r));
                table.Cell().Element(c => DC(c, OrAny(n.OriginalDest), r));
                table.Cell().Element(c => DC(c, OrAny(n.OriginalService), r));
                table.Cell().Element(c => DC(c, OrAny(n.TranslatedSource), r));
                table.Cell().Element(c => DC(c, OrAny(n.TranslatedDest), r));
                table.Cell().Element(c => DC(c, OrAny(n.TranslatedService), r));
                table.Cell().Element(c => DC(c, YN(n.Enabled), r));
                table.Cell().Element(c => DC(c, n.Comment, r));
            }
        });
    }

    private static void NatPoliciesV6Table(IContainer container, List<NatPolicyV6> policies)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2);
                c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.ConstantColumn(30);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Orig Src"));
                h.Cell().Element(c => HC(c, "Orig Dst")); h.Cell().Element(c => HC(c, "Orig Svc"));
                h.Cell().Element(c => HC(c, "Trans Src")); h.Cell().Element(c => HC(c, "Trans Dst"));
                h.Cell().Element(c => HC(c, "Trans Svc")); h.Cell().Element(c => HC(c, "On"));
                h.Cell().Element(c => HC(c, "Comment"));
            });
            for (int i = 0; i < policies.Count; i++)
            {
                var n = policies[i]; int r = i;
                table.Cell().Element(c => DC(c, n.Index.ToString(), r));
                table.Cell().Element(c => DC(c, OrAny(n.OriginalSource), r));
                table.Cell().Element(c => DC(c, OrAny(n.OriginalDest), r));
                table.Cell().Element(c => DC(c, OrAny(n.OriginalService), r));
                table.Cell().Element(c => DC(c, OrAny(n.TranslatedSource), r));
                table.Cell().Element(c => DC(c, OrAny(n.TranslatedDest), r));
                table.Cell().Element(c => DC(c, OrAny(n.TranslatedService), r));
                table.Cell().Element(c => DC(c, YN(n.Enabled), r));
                table.Cell().Element(c => DC(c, n.Comment, r));
            }
        });
    }

    private static void VpnPoliciesContent(IContainer container, List<VpnPolicy> policies)
    {
        container.Column(col =>
        {
            foreach (var vpn in policies)
            {
                var policyType = vpn.PolicyType switch
                { 0 => "Site-to-Site", 1 => "GroupVPN", _ => $"Type {vpn.PolicyType}" };

                col.Item().Element(c => SubSectionTitle(c, $"VPN Policy {vpn.Index} — {policyType}"));

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });

                    void Row(string label, string value, int r)
                    {
                        table.Cell().Element(c => DC(c, label, r));
                        table.Cell().Element(c => DC(c, value, r));
                    }

                    table.Header(h => { h.Cell().Element(c => HC(c, "Parameter")); h.Cell().Element(c => HC(c, "Value")); });

                    int r = 0;
                    Row("Remote ID", vpn.Phase1RemoteId, r++);
                    Row("Local Network", vpn.LocalNetwork, r++);
                    Row("Remote Network", vpn.RemoteNetwork, r++);
                    Row("Bound To", vpn.BoundToInterface, r++);
                    Row("P1 Exchange", vpn.P1Exchange == 1 ? "Main Mode" : vpn.P1Exchange == 2 ? "Aggressive" : $"Mode {vpn.P1Exchange}", r++);
                    Row("P1 DH Group", CryptoEnums.DhGroup(vpn.P1DhGroup), r++);
                    Row("P1 Encryption", CryptoEnums.EncryptionAlgorithm(vpn.P1CryptAlg), r++);
                    Row("P1 Auth", CryptoEnums.HashAlgorithm(vpn.P1AuthAlg), r++);
                    Row("P1 Lifetime", $"{vpn.P1LifeSecs:N0}s", r++);
                    Row("P2 Protocol", vpn.P2Protocol == 50 ? "ESP" : vpn.P2Protocol == 51 ? "AH" : $"{vpn.P2Protocol}", r++);
                    Row("P2 Encryption", CryptoEnums.EncryptionAlgorithm(vpn.P2CryptAlg), r++);
                    Row("P2 Auth", CryptoEnums.HashAlgorithm(vpn.P2AuthAlg), r++);
                    Row("P2 PFS", YN(vpn.PfsEnabled), r++);
                    Row("P2 Lifetime", $"{vpn.P2LifeSecs:N0}s", r++);
                    Row("NetBIOS", YN(vpn.AllowNetBIOS), r++);
                    Row("Multicast", YN(vpn.AllowMulticast), r++);
                    Row("Remote Clients", YN(vpn.RemoteClients), r);
                });

                col.Item().PaddingBottom(8);
            }
        });
    }

    private static void UsersTable(IContainer container, List<UserObject> users)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            { c.ConstantColumn(25); c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(2); });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Username"));
                h.Cell().Element(c => HC(c, "Comment")); h.Cell().Element(c => HC(c, "VPN Access"));
            });
            for (int i = 0; i < users.Count; i++)
            {
                var u = users[i]; int r = i;
                table.Cell().Element(c => DC(c, u.Index.ToString(), r)); table.Cell().Element(c => DC(c, u.Name, r));
                table.Cell().Element(c => DC(c, u.Comment, r));
                table.Cell().Element(c => DC(c, string.IsNullOrEmpty(u.VpnDestNet) ? "—" : u.VpnDestNet, r));
            }
        });
    }

    private static void UserGroupsTable(IContainer container, List<UserGroup> groups)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            { c.ConstantColumn(25); c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(2); });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Group"));
                h.Cell().Element(c => HC(c, "Comment")); h.Cell().Element(c => HC(c, "VPN Access"));
            });
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i]; int r = i;
                table.Cell().Element(c => DC(c, g.Index.ToString(), r)); table.Cell().Element(c => DC(c, g.Name, r));
                table.Cell().Element(c => DC(c, g.Comment, r));
                table.Cell().Element(c => DC(c, string.IsNullOrEmpty(g.VpnDestNet) ? "—" : g.VpnDestNet, r));
            }
        });
    }

    private static void DhcpContent(IContainer container, List<DhcpScope> scopes)
    {
        container.Column(col =>
        {
            foreach (var scope in scopes)
            {
                col.Item().Element(c => SubSectionTitle(c, $"DHCP Scope {scope.Index}"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    table.Header(h => { h.Cell().Element(c => HC(c, "Parameter")); h.Cell().Element(c => HC(c, "Value")); });

                    void Row(string label, string value, int r)
                    {
                        table.Cell().Element(c => DC(c, label, r));
                        table.Cell().Element(c => DC(c, value, r));
                    }

                    int r = 0;
                    Row("Range", $"{scope.IpStart} — {scope.IpEnd}", r++);
                    Row("Subnet Mask", scope.SubnetMask, r++);
                    Row("Gateway", scope.Gateway, r++);
                    Row("DNS 1", scope.Dns1, r++);
                    Row("DNS 2", scope.Dns2, r++);
                    Row("DNS 3", scope.Dns3, r++);
                    Row("Domain", scope.DomainName, r++);
                    Row("Lease Time", scope.LeaseTime.ToString(), r++);
                    Row("Enabled", YN(scope.Enabled), r);
                });
                col.Item().PaddingBottom(6);
            }
        });
    }

    private static void WanLbGroupsTable(IContainer container, List<WanLbGroup> groups)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1);
                c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Name"));
                h.Cell().Element(c => HC(c, "Preempt")); h.Cell().Element(c => HC(c, "Persist"));
                h.Cell().Element(c => HC(c, "Probe Int")); h.Cell().Element(c => HC(c, "Loss Thr"));
                h.Cell().Element(c => HC(c, "Recv Thr"));
            });
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i]; int r = i;
                table.Cell().Element(c => DC(c, g.Index.ToString(), r));
                table.Cell().Element(c => DC(c, g.Name, r));
                table.Cell().Element(c => DC(c, YN(g.Preempt), r));
                table.Cell().Element(c => DC(c, YN(g.Persist), r));
                table.Cell().Element(c => DC(c, $"{g.ProbeInterval}s", r));
                table.Cell().Element(c => DC(c, g.ProbeLossThreshold.ToString(), r));
                table.Cell().Element(c => DC(c, g.ProbeRecoveryThreshold.ToString(), r));
            }
        });
    }

    private static void WanLbMembersTable(IContainer container, List<WanLbMember> members)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(2);
                c.RelativeColumn(2); c.RelativeColumn(1);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Interface"));
                h.Cell().Element(c => HC(c, "Weight")); h.Cell().Element(c => HC(c, "Probe 1"));
                h.Cell().Element(c => HC(c, "Probe 2")); h.Cell().Element(c => HC(c, "Rank"));
            });
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i]; int r = i;
                table.Cell().Element(c => DC(c, m.Index.ToString(), r));
                table.Cell().Element(c => DC(c, m.Name, r));
                table.Cell().Element(c => DC(c, $"{m.LbPercentage}%", r));
                table.Cell().Element(c => DC(c, $"{m.ProbeTarget1}:{m.ProbePort1}", r));
                table.Cell().Element(c => DC(c, $"{m.ProbeTarget2}:{m.ProbePort2}", r));
                table.Cell().Element(c => DC(c, m.AdminRank.ToString(), r));
            }
        });
    }

    private static void CfsPoliciesTable(IContainer container, List<ContentFilterPolicy> policies)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2);
                c.RelativeColumn(1); c.RelativeColumn(2); c.ConstantColumn(30);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Name"));
                h.Cell().Element(c => HC(c, "Profile")); h.Cell().Element(c => HC(c, "Action"));
                h.Cell().Element(c => HC(c, "Zone")); h.Cell().Element(c => HC(c, "Users"));
                h.Cell().Element(c => HC(c, "On"));
            });
            for (int i = 0; i < policies.Count; i++)
            {
                var p = policies[i]; int r = i;
                table.Cell().Element(c => DC(c, p.Index.ToString(), r));
                table.Cell().Element(c => DC(c, p.Name, r)); table.Cell().Element(c => DC(c, p.ProfileObject, r));
                table.Cell().Element(c => DC(c, p.ActionObject, r)); table.Cell().Element(c => DC(c, p.DestZone, r));
                table.Cell().Element(c => DC(c, p.IncludedUsers, r)); table.Cell().Element(c => DC(c, YN(p.Enabled), r));
            }
        });
    }

    private static void BandwidthObjectsTable(IContainer container, List<BandwidthObject> objects)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            { c.ConstantColumn(25); c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(3); });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Name"));
                h.Cell().Element(c => HC(c, "Guaranteed")); h.Cell().Element(c => HC(c, "Maximum"));
                h.Cell().Element(c => HC(c, "Comment"));
            });
            for (int i = 0; i < objects.Count; i++)
            {
                var bw = objects[i]; int r = i;
                var guarU = bw.GuaranteedUnit == 1 ? "Mbps" : "kbps";
                var maxU = bw.MaxUnit == 1 ? "Mbps" : "kbps";
                table.Cell().Element(c => DC(c, bw.Index.ToString(), r));
                table.Cell().Element(c => DC(c, bw.Name, r));
                table.Cell().Element(c => DC(c, $"{bw.GuaranteedBw} {guarU}", r));
                table.Cell().Element(c => DC(c, $"{bw.MaxBw} {maxU}", r));
                table.Cell().Element(c => DC(c, bw.Comment, r));
            }
        });
    }

    private static void NacProfilesTable(IContainer container, List<NacProfile> profiles)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1);
                c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Description"));
                h.Cell().Element(c => HC(c, "IP Pool")); h.Cell().Element(c => HC(c, "DNS 1"));
                h.Cell().Element(c => HC(c, "DNS 2")); h.Cell().Element(c => HC(c, "Domain"));
                h.Cell().Element(c => HC(c, "Client Routes"));
            });
            for (int i = 0; i < profiles.Count; i++)
            {
                var n = profiles[i]; int r = i;
                table.Cell().Element(c => DC(c, n.Index.ToString(), r));
                table.Cell().Element(c => DC(c, n.Description, r));
                table.Cell().Element(c => DC(c, n.AddressObject, r));
                table.Cell().Element(c => DC(c, n.Dns1, r)); table.Cell().Element(c => DC(c, n.Dns2, r));
                table.Cell().Element(c => DC(c, n.DomainName, r));
                table.Cell().Element(c => DC(c, n.ClientRoutes, r));
            }
        });
    }

    private static void UnusedRulesTable(IContainer container, List<FirewallPolicy> unused)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(25); c.ConstantColumn(45); c.RelativeColumn(2);
                c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => HC(c, "#")); h.Cell().Element(c => HC(c, "Action"));
                h.Cell().Element(c => HC(c, "Src → Dst Zone")); h.Cell().Element(c => HC(c, "Source"));
                h.Cell().Element(c => HC(c, "Dest")); h.Cell().Element(c => HC(c, "Service"));
                h.Cell().Element(c => HC(c, "Comment"));
            });
            for (int i = 0; i < unused.Count; i++)
            {
                var p = unused[i]; int r = i;
                table.Cell().Element(c => DC(c, p.Index.ToString(), r));
                table.Cell().Element(c => ActionCell(c, p.ActionLabel, p.Action, r));
                table.Cell().Element(c => DC(c, $"{p.SourceZone} → {p.DestZone}", r));
                table.Cell().Element(c => DC(c, OrAny(p.SourceNet), r));
                table.Cell().Element(c => DC(c, OrAny(p.DestNet), r));
                table.Cell().Element(c => DC(c, OrAny(p.DestSvc), r));
                table.Cell().Element(c => DC(c, p.Comment, r));
            }
        });
    }


    private static string YN(bool val) => val ? "Yes" : "No";
    private static string OrAny(string val) => string.IsNullOrEmpty(val) ? "Any" : val;
}