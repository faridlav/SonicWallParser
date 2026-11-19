using System.Text;
using System.Web;
using SonicWallParser.Analysis;
using SonicWallParser.Models;
using SonicWallParser.Reporting;

namespace SonicWallParser.Export;

/// <summary>
/// Generates a single interactive HTML file from a report document. The findings and migration pages are
/// extra sections inside the same file.
/// </summary>
public static class HtmlExporter
{
    /// <summary>
    /// Generates an interactive HTML report at the specified path from the given SonicWall configuration.
    /// </summary>
    public static void Generate(SonicWallConfig config, string outputPath)
        => Generate(ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Html), outputPath);

    /// <summary>Writes an already built document as HTML.</summary>
    public static void Generate(ReportDocument doc, string outputPath)
        => File.WriteAllText(outputPath, Render(doc));

    /// <summary>Renders a document to an HTML string.</summary>
    public static string Render(ReportDocument doc)
    {
        var sb = new StringBuilder(64_000);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        Head(sb, doc);
        sb.AppendLine("<body>");
        Toolbar(sb, doc);
        sb.AppendLine("<main>");

        foreach (var section in doc.Sections)
            RenderSection(sb, section);

        sb.AppendLine("</main>");
        Script(sb);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void Head(StringBuilder sb, ReportDocument doc)
    {
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{E(doc.Title)} - {E(doc.Subtitle)}</title>");
        sb.AppendLine(@"<style>
:root {
  --bg: #f5f6fa; --bg2: #ffffff; --bg3: #ebedf2;
  --fg: #1a1d27; --fg2: #4a4e5a; --fg3: #8b8fa3;
  --border: #d8dbe5; --border2: #c5c9d6;
  --accent: #2563eb; --accent2: #1d4ed8;
  --allow: #16a34a; --deny: #dc2626;
  --hdr-bg: #1e293b; --hdr-fg: #f1f5f9;
  --row-alt: #f8f9fc;
  --shadow: 0 1px 3px rgba(0,0,0,.08);
  --radius: 8px;
  --font: 'Segoe UI', system-ui, -apple-system, sans-serif;
  --mono: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
}
[data-theme='dark'] {
  --bg: #0f1117; --bg2: #1a1d27; --bg3: #252836;
  --fg: #e2e4eb; --fg2: #a0a4b8; --fg3: #6b6f82;
  --border: #2e3142; --border2: #3a3e52;
  --accent: #3b82f6; --accent2: #60a5fa;
  --allow: #4ade80; --deny: #f87171;
  --hdr-bg: #0d1017; --hdr-fg: #c8ccd8;
  --row-alt: #1f2230;
  --shadow: 0 1px 3px rgba(0,0,0,.3);
}
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
html { scroll-behavior: smooth; }
body {
  font-family: var(--font); font-size: 14px; line-height: 1.5;
  background: var(--bg); color: var(--fg);
  transition: background .25s, color .25s;
}
main { max-width: 1400px; margin: 0 auto; padding: 20px; }

/* Toolbar */
.toolbar {
  position: sticky; top: 0; z-index: 100;
  background: var(--hdr-bg); color: var(--hdr-fg);
  padding: 12px 24px; display: flex; align-items: center; gap: 16px;
  box-shadow: 0 2px 8px rgba(0,0,0,.2);
}
.toolbar h1 { font-size: 16px; font-weight: 700; letter-spacing: -0.3px; flex: 1; }
.toolbar .meta { font-size: 11px; color: var(--fg3); }
.toolbar input[type=search] {
  background: rgba(255,255,255,.1); border: 1px solid rgba(255,255,255,.15);
  color: var(--hdr-fg); padding: 6px 12px; border-radius: 6px;
  font-size: 12px; width: 220px; outline: none;
  transition: border-color .2s;
}
.toolbar input[type=search]:focus { border-color: var(--accent); }
.toolbar input[type=search]::placeholder { color: rgba(255,255,255,.4); }

/* Theme toggle */
.theme-toggle {
  background: rgba(255,255,255,.1); border: 1px solid rgba(255,255,255,.15);
  color: var(--hdr-fg); cursor: pointer; padding: 5px 10px;
  border-radius: 6px; font-size: 14px; transition: background .2s;
}
.theme-toggle:hover { background: rgba(255,255,255,.2); }

/* Sections */
.section {
  background: var(--bg2); border: 1px solid var(--border);
  border-radius: var(--radius); margin-bottom: 16px;
  box-shadow: var(--shadow); overflow: hidden;
  transition: background .25s, border-color .25s;
}
.section-header {
  display: flex; align-items: center; gap: 10px;
  padding: 14px 18px; cursor: pointer; user-select: none;
  border-bottom: 1px solid transparent;
  transition: background .15s;
}
.section-header:hover { background: var(--bg3); }
.section-header h2 {
  font-size: 14px; font-weight: 700; flex: 1;
  letter-spacing: -0.2px;
}
.section-header .badge {
  background: var(--accent); color: #fff; font-size: 11px;
  padding: 2px 8px; border-radius: 10px; font-weight: 600;
}
.section-header .chevron {
  font-size: 12px; color: var(--fg3); transition: transform .2s;
}
.section.collapsed .chevron { transform: rotate(-90deg); }
.section.collapsed .section-header { border-bottom-color: transparent; }
.section-header.open { border-bottom-color: var(--border); }
.section-body { padding: 0; }
.section.collapsed .section-body { display: none; }

/* Info grid */
.info-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 12px; padding: 16px 18px;
}
.info-item { display: flex; flex-direction: column; gap: 2px; }
.info-label { font-size: 11px; color: var(--fg3); text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600; }
.info-value { font-size: 13px; font-weight: 500; }

/* Tables */
table {
  width: 100%; border-collapse: collapse; font-size: 12px;
}
thead { background: var(--hdr-bg); }
th {
  color: var(--hdr-fg); padding: 8px 10px; text-align: left;
  font-weight: 600; font-size: 11px; text-transform: uppercase;
  letter-spacing: 0.4px; white-space: nowrap;
  position: sticky; top: 0;
}
td {
  padding: 7px 10px; border-bottom: 1px solid var(--border);
  vertical-align: top; transition: background .15s;
}
tr:nth-child(even) td { background: var(--row-alt); }
tr:hover td { background: var(--bg3); }

/* Action badges */
.act-allow { color: var(--allow); font-weight: 700; }
.act-deny { color: var(--deny); font-weight: 700; }
.tag {
  display: inline-block; font-size: 10px; padding: 1px 6px;
  border-radius: 4px; font-weight: 600;
}
.tag-yes { background: rgba(22,163,74,.12); color: var(--allow); }
.tag-no { background: rgba(220,38,38,.08); color: var(--fg3); }
.tag-on { background: rgba(37,99,235,.1); color: var(--accent); }
.severity-info { color: var(--accent); font-weight: 700; }
.severity-warning { color: #b45309; font-weight: 700; }
.severity-error { color: var(--deny); font-weight: 700; }

/* VPN detail blocks */
.vpn-detail {
  padding: 16px 18px; border-bottom: 1px solid var(--border);
}
.vpn-detail:last-child { border-bottom: none; }
.vpn-title { font-size: 13px; font-weight: 700; margin-bottom: 6px; }
.vpn-meta { font-size: 11px; color: var(--fg2); margin-bottom: 10px; }
.vpn-flags { font-size: 11px; color: var(--fg2); margin-top: 8px; }

/* DHCP blocks */
.dhcp-block {
  padding: 14px 18px; border-bottom: 1px solid var(--border);
}
.dhcp-block:last-child { border-bottom: none; }

/* Warning banner */
.note { color: var(--fg3); font-size: .85rem; margin: 6px 0; }
.audit-banner {
  background: rgba(220,38,38,.08); border: 1px solid rgba(220,38,38,.2);
  border-radius: 6px; padding: 10px 14px; margin: 12px 18px;
  font-size: 12px; color: var(--deny);
}

/* Responsive */
@media (max-width: 768px) {
  .toolbar { flex-wrap: wrap; }
  .toolbar input[type=search] { width: 100%; order: 10; }
  main { padding: 10px; }
  .info-grid { grid-template-columns: 1fr; }
  td, th { padding: 5px 6px; font-size: 11px; }
}

/* Print */
@media print {
  .toolbar { position: static; }
  .theme-toggle, .toolbar input[type=search] { display: none; }
  .section.collapsed .section-body { display: block; }
  .section { break-inside: avoid; }
}

.sev { display: inline-block; padding: 1px 8px; border-radius: 10px; font-size: 11px; font-weight: 700; }
.sev-critical { background: #7f1d1d; color: #fff; }
.sev-high { background: #dc2626; color: #fff; }
.sev-medium { background: #f59e0b; color: #1a1d27; }
.sev-low { background: #3b82f6; color: #fff; }
.sev-info { background: var(--bg3); color: var(--fg2); }
.muted { color: var(--fg3); }
h3.subhead { font-size: 13px; margin: 14px 0 6px; color: var(--fg2); }
td a { color: var(--accent); text-decoration: none; }
td a:hover { text-decoration: underline; }
input.chk { width: 16px; height: 16px; }
tr:target td { background: rgba(245,158,11,.25); }
</style>");
        sb.AppendLine("</head>");
    }



    private static void Toolbar(StringBuilder sb, ReportDocument doc)
    {
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine($"  <h1>{E(doc.Title)}</h1>");
        sb.AppendLine($"  <span class=\"meta\">{E(doc.Subtitle)}{string.Concat(doc.Meta.Select(m => " &bull; " + E(m)))}</span>");
        sb.AppendLine("  <input type=\"search\" id=\"globalSearch\" placeholder=\"Filter tables...\" autocomplete=\"off\">");
        sb.AppendLine("  <button class=\"theme-toggle\" onclick=\"toggleTheme()\" title=\"Toggle dark/light mode\">&#9681;</button>");
        sb.AppendLine("</div>");
    }

    private static void RenderSection(StringBuilder sb, ReportSection section)
    {
        sb.AppendLine($"<div class=\"section{(section.Collapsed ? " collapsed" : "")}\" id=\"{E(section.Id)}\">");
        sb.AppendLine($"  <div class=\"section-header{(section.Collapsed ? "" : " open")}\" onclick=\"toggleSection(this)\">");
        sb.AppendLine("    <span class=\"chevron\">&#9660;</span>");
        sb.AppendLine($"    <h2>{E(section.Title)}</h2>");
        if (section.Count >= 0)
            sb.AppendLine($"    <span class=\"badge\">{section.Count}</span>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"section-body\">");

        foreach (var block in section.Blocks)
            RenderBlock(sb, block);

        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
    }

    private static void RenderBlock(StringBuilder sb, Block block)
    {
        switch (block)
        {
            case TableBlock t:
                sb.AppendLine("<table><thead><tr>" + string.Concat(t.Headers.Select(h => $"<th>{E(h)}</th>")) + "</tr></thead><tbody>");
                foreach (var row in t.Rows)
                {
                    var id = row.Id is null ? string.Empty : $" id=\"{E(row.Id)}\"";
                    sb.AppendLine($"<tr{id}>" + string.Concat(row.Cells.Select(c => $"<td>{CellHtml(c)}</td>")) + "</tr>");
                }

                sb.AppendLine("</tbody></table>");
                break;
            case KeyValueBlock kv:
                sb.AppendLine("<div class=\"info-grid\">");
                foreach (var item in kv.Items)
                    sb.AppendLine($"<div class=\"info-item\"><span class=\"info-label\">{E(item.Key)}</span><span class=\"info-value\">{CellHtml(item.Value)}</span></div>");
                sb.AppendLine("</div>");
                break;
            case ParagraphBlock p when p.IsWarning:
                sb.AppendLine($"<div class=\"audit-banner\">{E(p.Text)}</div>");
                break;
            case ParagraphBlock p:
                sb.AppendLine($"<p class=\"{(p.IsNote ? "note" : "para")}\">{E(p.Text)}</p>");
                break;
            case SubHeadingBlock h:
                sb.AppendLine($"<h3 class=\"subhead\"{(h.Id is null ? "" : $" id=\"{E(h.Id)}\"")}>{E(h.Text)}</h3>");
                break;
        }
    }

    private static string CellHtml(Cell c)
    {
        var text = E(c.Text);
        var inner = c.Style switch
        {
            CellStyle.Yes => $"<span class=\"tag tag-yes\">{text}</span>",
            CellStyle.No => $"<span class=\"tag tag-no\">{text}</span>",
            CellStyle.Unknown => $"<span class=\"tag\">{text}</span>",
            CellStyle.Allow => $"<span class=\"act-allow\">{text}</span>",
            CellStyle.Deny => $"<span class=\"act-deny\">{text}</span>",
            CellStyle.Strong => $"<strong>{text}</strong>",
            CellStyle.Code => $"<code>{text}</code>",
            CellStyle.Muted => $"<span class=\"muted\">{text}</span>",
            CellStyle.Critical => $"<span class=\"sev sev-critical\">{text}</span>",
            CellStyle.High => $"<span class=\"sev sev-high\">{text}</span>",
            CellStyle.Medium => $"<span class=\"sev sev-medium\">{text}</span>",
            CellStyle.Low => $"<span class=\"sev sev-low\">{text}</span>",
            CellStyle.Info => $"<span class=\"sev sev-info\">{text}</span>",
            CellStyle.Checkbox => "<input type=\"checkbox\" class=\"chk\" aria-label=\"done\">",
            _ => text
        };

        return c.LinkTo is null ? inner : $"<a href=\"#{E(c.LinkTo)}\">{inner}</a>";
    }

    private static void Script(StringBuilder sb)
    {
        sb.AppendLine(@"<script>
function toggleTheme() {
  const html = document.documentElement;
  const current = html.getAttribute('data-theme');
  const next = current === 'dark' ? 'light' : 'dark';
  html.setAttribute('data-theme', next);
  localStorage.setItem('sw-theme', next);
}
(function() {
  const saved = localStorage.getItem('sw-theme') ||
    (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  document.documentElement.setAttribute('data-theme', saved);
})();

function toggleSection(header) {
  const section = header.closest('.section');
  section.classList.toggle('collapsed');
  header.classList.toggle('open');
}

document.getElementById('globalSearch').addEventListener('input', function() {
  const q = this.value.toLowerCase();
  document.querySelectorAll('table tbody tr').forEach(row => {
    row.style.display = row.textContent.toLowerCase().includes(q) ? '' : 'none';
  });
});
</script>");
    }


    private static string E(string? text) => HttpUtility.HtmlEncode(text ?? "");
}
