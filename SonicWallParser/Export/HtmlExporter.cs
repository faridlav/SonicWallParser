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
  color-scheme: light;
  --bg: #f3f5f9; --bg2: #ffffff; --bg3: #eaeef5;
  --fg: #172033; --fg2: #46506a; --fg3: #7a859e;
  --border: #dde2ec; --border2: #c9d0de;
  --accent: #2563eb; --accent2: #1d4ed8; --accent-soft: rgba(37,99,235,.10);
  --allow: #15803d; --deny: #b91c1c;
  --hdr-bg: #16213a; --hdr-bg2: #22335c; --hdr-fg: #f1f5f9;
  --row-alt: #f7f9fc;
  --shadow: 0 1px 2px rgba(16,24,40,.06), 0 2px 8px rgba(16,24,40,.04);
  --radius: 12px;
  --font: 'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, sans-serif;
  --mono: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
}
[data-theme='dark'] {
  color-scheme: dark;
  --bg: #0d1118; --bg2: #161b26; --bg3: #212838;
  --fg: #e5e8f0; --fg2: #a9b0c4; --fg3: #7b849b;
  --border: #283042; --border2: #364058;
  --accent: #5b9bff; --accent2: #8bb8ff; --accent-soft: rgba(91,155,255,.14);
  --allow: #4ade80; --deny: #f87171;
  --hdr-bg: #0a0e16; --hdr-bg2: #18233f; --hdr-fg: #d6dae6;
  --row-alt: #1a2030;
  --shadow: 0 1px 2px rgba(0,0,0,.4), 0 2px 8px rgba(0,0,0,.25);
}
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
html { scroll-behavior: smooth; scroll-padding-top: 118px; }
body {
  font-family: var(--font); font-size: 14px; line-height: 1.55;
  background: var(--bg); color: var(--fg);
  -webkit-font-smoothing: antialiased;
  transition: background .25s, color .25s;
}
main { max-width: 1400px; margin: 0 auto; padding: 24px 24px 64px; }
:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }

/* Toolbar */
.toolbar {
  position: sticky; top: 0; z-index: 100;
  background: linear-gradient(120deg, var(--hdr-bg), var(--hdr-bg2)); color: var(--hdr-fg);
  padding: 12px 28px; display: flex; align-items: center; gap: 14px; flex-wrap: wrap;
  box-shadow: 0 2px 12px rgba(0,0,0,.25);
}
.toolbar h1 { font-size: 17px; font-weight: 700; letter-spacing: -0.2px; }
.toolbar .meta { font-size: 12px; color: rgba(241,245,249,.65); flex: 1; min-width: 200px; }
.toolbar input[type=search] {
  background: rgba(255,255,255,.08); border: 1px solid rgba(255,255,255,.18);
  color: var(--hdr-fg); padding: 7px 12px; border-radius: 8px;
  font-size: 13px; width: 240px; outline: none;
  transition: border-color .2s, background .2s;
}
.toolbar input[type=search]:focus { border-color: var(--accent2); background: rgba(255,255,255,.14); }
.toolbar input[type=search]::placeholder { color: rgba(255,255,255,.45); }
.tbtn {
  background: rgba(255,255,255,.08); border: 1px solid rgba(255,255,255,.18);
  color: var(--hdr-fg); cursor: pointer; padding: 6px 12px;
  border-radius: 8px; font-size: 12px; font-family: inherit; transition: background .2s;
}
.tbtn:hover { background: rgba(255,255,255,.2); }

/* Contents bar */
.nav {
  position: sticky; top: 56px; z-index: 90;
  background: var(--bg2); border-bottom: 1px solid var(--border);
  padding: 8px 28px; display: flex; gap: 6px; overflow-x: auto; white-space: nowrap;
  scrollbar-width: thin;
}
.nav a {
  font-size: 12px; color: var(--fg2); text-decoration: none;
  padding: 4px 11px; border-radius: 999px; border: 1px solid transparent;
  transition: background .15s, color .15s;
}
.nav a:hover { background: var(--bg3); color: var(--fg); }
.nav a.active { background: var(--accent-soft); color: var(--accent); border-color: var(--accent-soft); font-weight: 600; }

/* Sections */
.section {
  background: var(--bg2); border: 1px solid var(--border);
  border-radius: var(--radius); margin-bottom: 18px;
  box-shadow: var(--shadow); overflow: hidden;
  transition: background .25s, border-color .25s;
}
.section.no-match { display: none; }
.section-header {
  display: flex; align-items: center; gap: 12px;
  padding: 15px 20px; cursor: pointer; user-select: none;
  border-bottom: 1px solid transparent;
  transition: background .15s;
}
.section-header:hover { background: var(--bg3); }
.section-header h2 { font-size: 15px; font-weight: 700; flex: 1; letter-spacing: -0.1px; }
.section-header .badge {
  background: var(--accent-soft); color: var(--accent); font-size: 12px;
  padding: 2px 10px; border-radius: 999px; font-weight: 700;
}
.section-header .chevron { font-size: 11px; color: var(--fg3); transition: transform .2s; }
.section.collapsed .chevron { transform: rotate(-90deg); }
.section-header.open { border-bottom-color: var(--border); }
.section-body { padding: 0; }
.section.collapsed .section-body { display: none; }
.section-body > p.para { padding: 10px 20px 0; color: var(--fg2); }
.section-body > p.para:last-child, .section-body > p.note:last-child { padding-bottom: 12px; }
.section-body > p.note { padding: 6px 20px 0; }

/* Info grid */
.info-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 10px; padding: 16px 20px;
}
.info-item {
  display: flex; flex-direction: column; gap: 3px;
  background: var(--bg); border: 1px solid var(--border); border-radius: 10px;
  padding: 10px 14px;
}
.info-label { font-size: 11px; color: var(--fg3); text-transform: uppercase; letter-spacing: 0.6px; font-weight: 600; }
.info-value { font-size: 14px; font-weight: 600; overflow-wrap: anywhere; }

/* Tables */
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 12.5px; }
thead { background: var(--hdr-bg); }
th {
  color: var(--hdr-fg); padding: 9px 12px; text-align: left;
  font-weight: 600; font-size: 11px; text-transform: uppercase;
  letter-spacing: 0.5px; white-space: nowrap;
  cursor: pointer; user-select: none;
}
th:hover { background: var(--hdr-bg2); }
th[data-dir='asc']::after { content: ' \25B2'; font-size: 9px; }
th[data-dir='desc']::after { content: ' \25BC'; font-size: 9px; }
td {
  padding: 8px 12px; border-bottom: 1px solid var(--border);
  vertical-align: top; transition: background .15s; overflow-wrap: break-word;
}
tbody tr:last-child td { border-bottom: none; }
tr:nth-child(even) td { background: var(--row-alt); }
tr:hover td { background: var(--accent-soft); }

/* Action badges */
.act-allow { color: var(--allow); font-weight: 700; }
.act-deny { color: var(--deny); font-weight: 700; }
.tag {
  display: inline-block; font-size: 11px; padding: 1px 8px;
  border-radius: 999px; font-weight: 600; background: var(--bg3); color: var(--fg2);
}
.tag-yes { background: rgba(22,163,74,.14); color: var(--allow); }
.tag-no { background: var(--bg3); color: var(--fg3); }
.tag-on { background: var(--accent-soft); color: var(--accent); }
.severity-info { color: var(--accent); font-weight: 700; }
.severity-warning { color: #b45309; font-weight: 700; }
.severity-error { color: var(--deny); font-weight: 700; }
code { font-family: var(--mono); font-size: 12px; background: var(--bg3); padding: 1px 5px; border-radius: 4px; }

/* VPN detail blocks */
.vpn-detail { padding: 16px 20px; border-bottom: 1px solid var(--border); }
.vpn-detail:last-child { border-bottom: none; }
.vpn-title { font-size: 13px; font-weight: 700; margin-bottom: 6px; }
.vpn-meta { font-size: 12px; color: var(--fg2); margin-bottom: 10px; }
.vpn-flags { font-size: 12px; color: var(--fg2); margin-top: 8px; }

/* DHCP blocks */
.dhcp-block { padding: 14px 20px; border-bottom: 1px solid var(--border); }
.dhcp-block:last-child { border-bottom: none; }

/* Notes and warnings */
.note { color: var(--fg3); font-size: .85rem; margin: 6px 0; }
.audit-banner {
  background: rgba(217,119,6,.10); border: 1px solid rgba(217,119,6,.35);
  border-left: 4px solid #d97706; border-radius: 8px;
  padding: 10px 14px; margin: 14px 20px;
  font-size: 13px; color: var(--fg);
}

.sev { display: inline-block; padding: 1px 9px; border-radius: 999px; font-size: 11px; font-weight: 700; letter-spacing: .2px; }
.sev-critical { background: #7f1d1d; color: #fff; }
.sev-high { background: #dc2626; color: #fff; }
.sev-medium { background: #f59e0b; color: #1a1d27; }
.sev-low { background: #3b82f6; color: #fff; }
.sev-info { background: var(--bg3); color: var(--fg2); }
.muted { color: var(--fg3); }
h3.subhead { font-size: 13px; margin: 0; padding: 16px 20px 8px; color: var(--fg2); font-weight: 700; }
td a { color: var(--accent); text-decoration: none; }
td a:hover { text-decoration: underline; }
input.chk { width: 16px; height: 16px; accent-color: var(--accent); cursor: pointer; }
tr:target td { background: rgba(245,158,11,.28); }

/* Responsive */
@media (max-width: 768px) {
  .toolbar { padding: 10px 14px; }
  .toolbar input[type=search] { width: 100%; order: 10; }
  .nav { position: static; padding: 8px 14px; }
  main { padding: 12px 10px 40px; }
  .info-grid { grid-template-columns: 1fr; }
  td, th { padding: 6px 8px; font-size: 11.5px; }
  html { scroll-padding-top: 12px; }
}
@media (prefers-reduced-motion: reduce) {
  html { scroll-behavior: auto; }
  * { transition: none !important; }
}

/* Print */
@media print {
  body { background: #fff; }
  .toolbar { position: static; background: #fff; color: #000; box-shadow: none; padding: 0 0 12px; }
  .toolbar .meta { color: #444; }
  .tbtn, .toolbar input[type=search], .nav { display: none; }
  .section.collapsed .section-body { display: block; }
  .section { break-inside: avoid; box-shadow: none; }
  .table-wrap { overflow: visible; }
}
</style>");
        sb.AppendLine("</head>");
    }



    private static void Toolbar(StringBuilder sb, ReportDocument doc)
    {
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine($"  <h1>{E(doc.Title)}</h1>");
        sb.AppendLine($"  <span class=\"meta\">{E(doc.Subtitle)}{string.Concat(doc.Meta.Select(m => " &bull; " + E(m)))}</span>");
        sb.AppendLine("  <input type=\"search\" id=\"globalSearch\" placeholder=\"Filter tables...\" autocomplete=\"off\">");
        sb.AppendLine("  <button class=\"tbtn\" onclick=\"setAll(true)\" title=\"Expand all sections\">Expand all</button>");
        sb.AppendLine("  <button class=\"tbtn\" onclick=\"setAll(false)\" title=\"Collapse all sections\">Collapse all</button>");
        sb.AppendLine("  <button class=\"tbtn\" onclick=\"toggleTheme()\" title=\"Toggle dark/light mode\">&#9681; Theme</button>");
        sb.AppendLine("</div>");
        sb.AppendLine("<nav class=\"nav\" aria-label=\"Contents\">" + string.Concat(doc.Sections.Select(s => $"<a href=\"#{E(s.Id)}\">{E(s.Title)}</a>")) + "</nav>");
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
                sb.AppendLine("<div class=\"table-wrap\"><table><thead><tr>" + string.Concat(t.Headers.Select(h => $"<th>{E(h)}</th>")) + "</tr></thead><tbody>");
                foreach (var row in t.Rows)
                {
                    var id = row.Id is null ? string.Empty : $" id=\"{E(row.Id)}\"";
                    sb.AppendLine($"<tr{id}>" + string.Concat(row.Cells.Select(c => $"<td>{CellHtml(c)}</td>")) + "</tr>");
                }

                sb.AppendLine("</tbody></table></div>");
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
  const next = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
  html.setAttribute('data-theme', next);
  try { localStorage.setItem('sw-theme', next); } catch (e) {}
}
(function() {
  let saved = null;
  try { saved = localStorage.getItem('sw-theme'); } catch (e) {}
  if (!saved) saved = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  document.documentElement.setAttribute('data-theme', saved);
})();

function setOpen(section, open) {
  section.classList.toggle('collapsed', !open);
  const header = section.querySelector('.section-header');
  if (header) header.classList.toggle('open', open);
}

function toggleSection(header) {
  const section = header.closest('.section');
  setOpen(section, section.classList.contains('collapsed'));
}

function setAll(open) {
  document.querySelectorAll('.section').forEach(s => setOpen(s, open));
}

/* Contents bar: opening the target section before jumping to it */
document.querySelectorAll('.nav a').forEach(a => {
  a.addEventListener('click', () => {
    const target = document.getElementById(a.getAttribute('href').slice(1));
    if (target) setOpen(target, true);
  });
});
window.addEventListener('hashchange', () => {
  const el = document.getElementById(location.hash.slice(1));
  const section = el && el.closest('.section');
  if (section) setOpen(section, true);
});

/* Highlight the section being read */
if ('IntersectionObserver' in window) {
  const links = new Map();
  document.querySelectorAll('.nav a').forEach(a => links.set(a.getAttribute('href').slice(1), a));
  const io = new IntersectionObserver(entries => {
    entries.forEach(e => {
      if (e.isIntersecting && links.has(e.target.id)) {
        links.forEach(l => l.classList.remove('active'));
        links.get(e.target.id).classList.add('active');
      }
    });
  }, { rootMargin: '-120px 0px -70% 0px' });
  document.querySelectorAll('.section').forEach(s => io.observe(s));
}

/* Table filter: hides non matching rows, and sections whose tables have no match */
document.getElementById('globalSearch').addEventListener('input', function() {
  const q = this.value.toLowerCase();
  document.querySelectorAll('.section').forEach(section => {
    const rows = section.querySelectorAll('table tbody tr');
    let shown = 0;
    rows.forEach(row => {
      const hit = row.textContent.toLowerCase().includes(q);
      row.style.display = hit ? '' : 'none';
      if (hit) shown++;
    });
    section.classList.toggle('no-match', q !== '' && rows.length > 0 && shown === 0);
    if (q !== '' && shown > 0) setOpen(section, true);
  });
});

/* Sortable columns: numbers sort as numbers, everything else as text */
document.querySelectorAll('table').forEach(table => {
  const head = table.querySelectorAll('thead th');
  head.forEach((th, idx) => {
    th.title = 'Click to sort';
    th.addEventListener('click', () => {
      const dir = th.getAttribute('data-dir') === 'asc' ? 'desc' : 'asc';
      head.forEach(h => h.removeAttribute('data-dir'));
      th.setAttribute('data-dir', dir);
      const body = table.tBodies[0];
      const rows = Array.from(body.rows);
      const val = r => (r.cells[idx] ? r.cells[idx].textContent.trim() : '');
      const num = v => { const n = parseFloat(v.replace(/,/g, '')); return isNaN(n) || !/^-?[0-9][0-9,.]*$/.test(v) ? null : n; };
      const rank = { critical: 0, high: 1, medium: 2, low: 3, info: 4 };
      const sev = v => (v.toLowerCase() in rank) ? rank[v.toLowerCase()] : null;
      rows.sort((a, b) => {
        const x = val(a), y = val(b), nx = sev(x) !== null && sev(y) !== null ? sev(x) : num(x), ny = sev(x) !== null && sev(y) !== null ? sev(y) : num(y);
        const c = (nx !== null && ny !== null) ? nx - ny : x.localeCompare(y, undefined, { numeric: true, sensitivity: 'base' });
        return dir === 'asc' ? c : -c;
      });
      rows.forEach(r => body.appendChild(r));
    });
  });
});
</script>");
    }


    private static string E(string? text) => HttpUtility.HtmlEncode(text ?? "");
}
