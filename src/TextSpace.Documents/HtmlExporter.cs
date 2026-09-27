using System.Globalization;
using System.Net;
using System.Text;
using TextSpace.Core;

namespace TextSpace.Documents;

/// <summary>Self-contained, non-executing HTML with safe links and named bookmark targets.</summary>
public static class HtmlExporter
{
    private static string Esc(string value) => WebUtility.HtmlEncode(value);
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Color(string? value, string fallback = "#202020") => value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit) ? value : fallback;
    private static string Font(string value)
    {
        var result = new StringBuilder("'");
        foreach (var c in value)
            if (c is '\\' or '\'' || char.IsControl(c)) result.Append('\\').Append(((int)c).ToString("x", CultureInfo.InvariantCulture)).Append(' ');
            else result.Append(c);
        return result.Append("',sans-serif").ToString();
    }
    private static string? Link(DocumentModel document, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        if (target.StartsWith('#'))
        {
            var name = Uri.UnescapeDataString(target[1..]);
            var bookmark = document.Bookmarks.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return bookmark is null ? null : "#" + bookmark.Name;
        }
        return Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto" ? uri.AbsoluteUri : null;
    }
    public static string Export(DocumentModel document)
    {
        DocumentJson.Validate(document);
        var index = new TextIndex(document);
        var bookmarks = document.Bookmarks.GroupBy(b => b.Start).ToDictionary(g => g.Key, g => g.ToArray());
        var body = new StringBuilder();
        void Mark(int position)
        {
            if (!bookmarks.TryGetValue(position, out var found)) return;
            foreach (var bookmark in found) body.Append("<span id=\"").Append(Esc(bookmark.Name)).Append("\"></span>");
        }
        void Run(string text, TextStyle style)
        {
            var link = Link(document, style.Hyperlink);
            if (link is not null) body.Append("<a href=\"").Append(Esc(link)).Append("\" rel=\"noopener noreferrer\">");
            var css = new StringBuilder("font-family:").Append(Font(style.FontFamily)).Append(";font-size:").Append(N(style.EffectiveSize)).Append("pt;color:").Append(Color(style.Color)).Append(';');
            if (style.Bold) css.Append("font-weight:bold;");
            if (style.Italic) css.Append("font-style:italic;");
            if (style.Underline || style.StrikeThrough) css.Append("text-decoration:").Append(style.Underline ? "underline " : "").Append(style.StrikeThrough ? "line-through" : "").Append(';');
            if (style.Highlight is not null) css.Append("background:").Append(Color(style.Highlight, "transparent")).Append(';');
            if (style.Superscript || style.Subscript) css.Append("vertical-align:").Append(style.Superscript ? "super" : "sub").Append(';');
            body.Append("<span style=\"").Append(Esc(css.ToString())).Append("\">").Append(Esc(text).Replace("\u2028", "<br>").Replace("\t", "&#9;")).Append("</span>");
            if (link is not null) body.Append("</a>");
        }
        void Blocks(IEnumerable<Block> blocks)
        {
            var counters = new int[9];
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case Paragraph p:
                        var f = p.Format;
                        var tag = f.OutlineLevel is > 0 and <= 6 ? "h" + f.OutlineLevel : "p";
                        var css = $"margin:{N(f.SpaceBefore)}pt {N(f.RightIndent)}pt {N(f.SpaceAfter)}pt {N(f.LeftIndent)}pt;line-height:{N(f.LineSpacing)};text-align:{f.Alignment.ToString().ToLowerInvariant()};font-weight:normal;text-indent:{N(f.FirstLineIndent)}pt;";
                        if (f.Shading is not null) css += "background:" + Color(f.Shading, "transparent") + ";";
                        if (f.BorderBottom) css += "border-bottom:.5pt solid #8e9ead;";
                        if (f.PageBreakBefore) css += "break-before:page;";
                        if (f.KeepWithNext) css += "break-after:avoid;";
                        body.Append('<').Append(tag).Append(" style=\"").Append(Esc(css)).Append("\">");
                        var start = index.StartOf(p); var offset = 0;
                        Mark(start);
                        var level = Math.Clamp(f.ListLevel, 0, 8);
                        if (f.List == ListKind.Number)
                        {
                            counters[level]++;
                            Array.Clear(counters, level + 1, counters.Length - level - 1);
                            body.Append(counters[level]).Append(". ");
                        }
                        else if (f.List == ListKind.Bullet) body.Append("&#8226; ");
                        else Array.Clear(counters);
                        var positions = document.Bookmarks.Where(b => b.Start > start && b.Start <= start + p.Length).Select(b => b.Start - start).Distinct().Order().ToArray();
                        foreach (var run in p.Runs)
                        {
                            var end = offset + run.Text.Length; var at = offset;
                            foreach (var boundary in positions.Where(b => b > offset && b <= end).Append(end).Distinct())
                            {
                                Run(run.Text.Substring(at - offset, boundary - at), run.Style);
                                Mark(start + boundary); at = boundary;
                            }
                            offset = end;
                        }
                        if (p.Runs.Count == 0) body.Append("<br>");
                        body.Append("</").Append(tag).Append('>'); break;
                    case TableBlock table:
                        Array.Clear(counters); body.Append("<table>");
                        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                        {
                            body.Append("<tr>");
                            foreach (var cell in table.Rows[rowIndex].Cells)
                            {
                                var cellTag = table.HeaderRow && rowIndex == 0 ? "th" : "td";
                                var fill = cell.Shading ?? (table.HeaderRow && rowIndex == 0 ? "#D9E5F5" : table.BandedRows && rowIndex % 2 == 0 ? "#F3F6FA" : "#FFFFFF");
                                body.Append('<').Append(cellTag).Append(" style=\"padding:").Append(N(table.CellPadding)).Append("pt;background:").Append(Color(fill, "#FFFFFF")).Append("\">");
                                Blocks(cell.Blocks); body.Append("</").Append(cellTag).Append('>');
                            }
                            body.Append("</tr>");
                        }
                        body.Append("</table>"); break;
                    case ImageBlock image when image.ContentType is "image/png" or "image/jpeg" or "image/gif":
                        body.Append("<img alt=\"").Append(Esc(image.AltText)).Append("\" style=\"max-width:100%;width:").Append(N(image.Width)).Append("pt\" src=\"data:").Append(image.ContentType).Append(";base64,").Append(Convert.ToBase64String(image.Data)).Append("\">"); break;
                    case PageBreakBlock: body.Append("<div style=\"break-after:page\"></div>"); break;
                    case ColumnBreakBlock: body.Append("<div style=\"break-after:column\"></div>"); break;
                }
            }
        }
        var styles = new StringBuilder();
        var sections = DocumentSections.Definitions(document); var number = 0; var blocks = new List<Block>();
        void Section(SectionBreakKind? kind)
        {
            var page = sections[number].Page;
            styles.Append("@page section").Append(number).Append("{size:").Append(N(page.Width)).Append("pt ").Append(N(page.Height)).Append("pt;margin:")
                .Append(N(page.MarginTop)).Append("pt ").Append(N(page.MarginRight)).Append("pt ").Append(N(page.MarginBottom)).Append("pt ").Append(N(page.MarginLeft)).Append("pt}");
            body.Append("<section style=\"page:section").Append(number).Append(";max-width:").Append(N(page.ContentWidth)).Append("pt;margin:0 auto;column-count:").Append(page.Columns).Append(";column-gap:").Append(N(page.ColumnGap)).Append("pt;");
            if (kind is not null) body.Append("break-before:").Append(kind == SectionBreakKind.OddPage ? "right" : kind == SectionBreakKind.EvenPage ? "left" : "page").Append(';');
            body.Append("\">"); Blocks(blocks); body.Append("</section>"); blocks.Clear(); number++;
        }
        SectionBreakKind? preceding = null;
        foreach (var block in document.Blocks)
        {
            if (block is SectionBreakBlock boundary) { Section(preceding); preceding = boundary.Kind; }
            else blocks.Add(block);
        }
        Section(preceding);
        var page = document.Page;
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>" + Esc(document.Title)
            + "</title><style>body{font-family:Arial,sans-serif;font-size:11pt;margin:40px auto;max-width:" + N(sections.Max(s => s.Page.ContentWidth)) + "pt;white-space:pre-wrap}table{border-collapse:collapse;width:100%;white-space:normal}td,th{border:1px solid #a8b7c8;vertical-align:top;font-weight:normal;text-align:left}td p,th p{margin:0}img{display:block;margin:12pt auto}@page{size:"
            + N(page.Width) + "pt " + N(page.Height) + "pt;margin:" + N(page.MarginTop) + "pt " + N(page.MarginRight) + "pt " + N(page.MarginBottom) + "pt " + N(page.MarginLeft) + "pt}" + styles + "@media print{body{margin:0;max-width:none}section{max-width:none!important}}</style></head><body>" + body + "</body></html>";
    }
}
