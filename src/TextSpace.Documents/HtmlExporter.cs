using System.Globalization;
using System.Net;
using System.Text;
using TextSpace.Core;

namespace TextSpace.Documents;

public static class HtmlExporter
{
    private static string Esc(string value) => WebUtility.HtmlEncode(value);
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    public static string Export(DocumentModel document)
    {
        var body = new StringBuilder();
        void Blocks(IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case Paragraph p:
                        var tag = p.Format.OutlineLevel is > 0 and <= 6 ? "h" + p.Format.OutlineLevel : "p";
                        body.Append('<').Append(tag).Append(" style=\"margin:").Append(N(p.Format.SpaceBefore)).Append("pt ").Append(N(p.Format.RightIndent)).Append("pt ").Append(N(p.Format.SpaceAfter)).Append("pt ").Append(N(p.Format.LeftIndent)).Append("pt;line-height:").Append(N(p.Format.LineSpacing)).Append(";text-align:").Append(p.Format.Alignment.ToString().ToLowerInvariant()).Append(";font-weight:normal\">");
                        if (p.Format.List != ListKind.None) body.Append(p.Format.List == ListKind.Bullet ? "&#8226; " : "1. ");
                        foreach (var run in p.Runs)
                        {
                            var s = run.Style; body.Append("<span style=\"font-family:").Append(Esc(s.FontFamily)).Append(",sans-serif;font-size:").Append(N(s.FontSize)).Append("pt;color:").Append(Esc(s.Color)).Append(';');
                            if (s.Bold) body.Append("font-weight:bold;"); if (s.Italic) body.Append("font-style:italic;"); if (s.Underline) body.Append("text-decoration:underline;"); if (s.StrikeThrough) body.Append("text-decoration:line-through;"); if (s.Highlight is not null) body.Append("background:").Append(Esc(s.Highlight)).Append(';');
                            body.Append("\">").Append(Esc(run.Text).Replace("\u2028", "<br>").Replace("\t", "&#9;")).Append("</span>");
                        }
                        if (p.Runs.Count == 0) body.Append("<br>"); body.Append("</").Append(tag).Append('>'); break;
                    case TableBlock table:
                        body.Append("<table>"); foreach (var row in table.Rows) { body.Append("<tr>"); foreach (var cell in row.Cells) { body.Append("<td>"); Blocks(cell.Blocks); body.Append("</td>"); } body.Append("</tr>"); } body.Append("</table>"); break;
                    case ImageBlock image:
                        body.Append("<img alt=\"").Append(Esc(image.AltText)).Append("\" style=\"max-width:100%;width:").Append(N(image.Width)).Append("pt\" src=\"data:").Append(Esc(image.ContentType)).Append(";base64,").Append(Convert.ToBase64String(image.Data)).Append("\">"); break;
                    case PageBreakBlock: body.Append("<div style=\"break-after:page\"></div>"); break;
                }
            }
        }
        Blocks(document.Blocks); var page = document.Page;
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>" + Esc(document.Title) + "</title><style>body{font-family:Arial,sans-serif;font-size:11pt;margin:40px auto;max-width:" + N(page.ContentWidth) + "pt;white-space:pre-wrap}table{border-collapse:collapse;width:100%;white-space:normal}td{border:1px solid #a8b7c8;padding:6pt;vertical-align:top}td p{margin:0}img{display:block;margin:12pt auto}@page{size:" + N(page.Width) + "pt " + N(page.Height) + "pt;margin:" + N(page.MarginTop) + "pt " + N(page.MarginRight) + "pt " + N(page.MarginBottom) + "pt " + N(page.MarginLeft) + "pt}@media print{body{margin:0;max-width:none}}</style></head><body>" + body + "</body></html>";
    }
}
