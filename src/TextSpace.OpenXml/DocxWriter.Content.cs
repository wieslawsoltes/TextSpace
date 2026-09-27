using System.Text;
using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxWriter
{
    private IEnumerable<XElement> Blocks(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph p: yield return WriteParagraph(p); break;
                case PageBreakBlock: yield return E("p", E("r", E("br", new XAttribute(W + "type", "page")))); break;
                case ImageBlock image: yield return Picture(image); break;
                case TableBlock table: yield return Table(table); break;
            }
        }
    }
    private XElement WriteParagraph(Paragraph paragraph)
    {
        var result = E("p", ParagraphProperties(paragraph.Format)); var start = _index.StartOf(paragraph); var position = 0;
        void Mark(int offset)
        {
            foreach (var (bookmark, id) in _document.Bookmarks.Select((b, i) => (b, i)))
            {
                if (bookmark.Start == start + offset) result.Add(E("bookmarkStart", new XAttribute(W + "id", id), new XAttribute(W + "name", bookmark.Name)));
                if (bookmark.End == start + offset) result.Add(E("bookmarkEnd", new XAttribute(W + "id", id)));
            }
            foreach (var (comment, id) in _document.Comments.Select((c, i) => (c, i)))
            {
                if (comment.Start == start + offset) result.Add(E("commentRangeStart", new XAttribute(W + "id", id)));
                if (comment.End == start + offset) result.Add(E("commentRangeEnd", new XAttribute(W + "id", id)), E("r", E("commentReference", new XAttribute(W + "id", id))));
            }
        }
        Mark(0);
        foreach (var run in paragraph.Runs)
        {
            var boundaries = _document.Comments.SelectMany(c => new[] { c.Start - start, c.End - start })
                .Concat(_document.Bookmarks.SelectMany(b => new[] { b.Start - start, b.End - start }))
                .Where(i => i > position && i < position + run.Text.Length).Append(position + run.Text.Length).Distinct().Order().ToArray(); var at = position;
            foreach (var end in boundaries)
            {
                var content = Run(run.Text.Substring(at - position, end - at), run.Style); var link = SafeLink(run.Style.Hyperlink);
                if (link?.StartsWith('#') == true) result.Add(E("hyperlink", new XAttribute(W + "anchor", link[1..]), content));
                else if (link is not null) result.Add(E("hyperlink", new XAttribute(R + "id", Relate("hyperlink", link, true)), content));
                else result.Add(content);
                at = end; Mark(end);
            }
            position += run.Text.Length;
        }
        if (paragraph.Runs.Count == 0) result.Add(E("r", RunProperties(paragraph.DefaultStyle))); return result;
    }
    internal static XElement Run(string text, TextStyle style)
    {
        var result = E("r", RunProperties(style)); var buffer = new StringBuilder();
        void Flush() { if (buffer.Length > 0) { result.Add(E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), buffer.ToString())); buffer.Clear(); } }
        foreach (var c in text) { if (c is '\t' or '\u2028') { Flush(); result.Add(E(c == '\t' ? "tab" : "br")); } else buffer.Append(c); }
        Flush(); return result;
    }
    internal static XElement RunProperties(TextStyle s) => E("rPr",
        E("rFonts", new XAttribute(W + "ascii", s.FontFamily), new XAttribute(W + "hAnsi", s.FontFamily), new XAttribute(W + "cs", s.FontFamily)),
        s.Bold ? E("b") : null, s.Italic ? E("i") : null, s.StrikeThrough ? E("strike") : null,
        E("color", V(Hex(s.Color))), E("sz", V((int)Math.Round(s.FontSize * 2))), E("szCs", V((int)Math.Round(s.FontSize * 2))),
        s.Underline || s.Hyperlink is not null ? E("u", V("single")) : null,
        s.Highlight is not null ? E("shd", V("clear"), new XAttribute(W + "fill", Hex(s.Highlight))) : null,
        s.Superscript || s.Subscript ? E("vertAlign", V(s.Superscript ? "superscript" : "subscript")) : null);
    internal static XElement ParagraphProperties(ParagraphFormat f, bool includeStyle = true) => E("pPr",
        includeStyle ? E("pStyle", V(f.StyleName.Replace(" ", ""))) : null,
        f.KeepWithNext ? E("keepNext") : null, f.PageBreakBefore ? E("pageBreakBefore") : null,
        f.List != ListKind.None ? E("numPr", E("ilvl", V(f.ListLevel)), E("numId", V(f.List == ListKind.Bullet ? 1 : 2))) : null,
        f.BorderBottom ? E("pBdr", E("bottom", V("single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "8E9EAD"))) : null,
        f.Shading is not null ? E("shd", V("clear"), new XAttribute(W + "fill", Hex(f.Shading))) : null,
        E("spacing", new XAttribute(W + "before", Twips(f.SpaceBefore)), new XAttribute(W + "after", Twips(f.SpaceAfter)), new XAttribute(W + "line", (int)Math.Round(f.LineSpacing * 240)), new XAttribute(W + "lineRule", "auto")),
        E("ind", new XAttribute(W + "left", Twips(f.LeftIndent + (f.List != ListKind.None ? 18 : 0))), new XAttribute(W + "right", Twips(f.RightIndent)), f.FirstLineIndent >= 0 ? new XAttribute(W + "firstLine", Twips(f.FirstLineIndent)) : new XAttribute(W + "hanging", Twips(-f.FirstLineIndent))),
        E("jc", V(f.Alignment == TextAlignment.Justify ? "both" : f.Alignment.ToString().ToLowerInvariant())), f.OutlineLevel > 0 ? E("outlineLvl", V(f.OutlineLevel - 1)) : null);
    private static XElement HeaderFooter(string text, bool centered = false)
    {
        var p = E("p", E("pPr", E("jc", V(centered ? "center" : "left")))); var style = new TextStyle { FontSize = 8, Color = "#777777" };
        foreach (var part in System.Text.RegularExpressions.Regex.Split(text, @"(\{PAGE\}|\{NUMPAGES\})")) p.Add(part is "{PAGE}" or "{NUMPAGES}" ? E("fldSimple", new XAttribute(W + "instr", part.Trim('{', '}')), Run("1", style)) : Run(part, style)); return p;
    }
    private XElement Table(TableBlock table)
    {
        var count = table.Rows.Max(r => r.Cells.Count); var weights = table.ColumnWidths.Count == count && table.ColumnWidths.All(w => w > 0 && double.IsFinite(w)) ? table.ColumnWidths.ToArray() : Enumerable.Repeat(1d, count).ToArray();
        var widths = weights.Select(w => Twips(_document.Page.ColumnWidth * w / weights.Sum())).ToArray();
        var borders = E("tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side => E(side, V("single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "A8B7C8"))));
        var margins = E("tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(side => E(side, new XAttribute(W + "w", Twips(table.CellPadding)), new XAttribute(W + "type", "dxa"))));
        var result = E("tbl", E("tblPr", E("tblW", new XAttribute(W + "w", widths.Sum()), new XAttribute(W + "type", "dxa")), borders, E("tblLayout", new XAttribute(W + "type", "fixed")), margins), E("tblGrid", widths.Select(w => E("gridCol", new XAttribute(W + "w", w)))));
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = E("tr", table.HeaderRow && r == 0 ? E("trPr", E("tblHeader")) : null);
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                var cell = table.Rows[r].Cells[c]; var fill = cell.Shading ?? (table.HeaderRow && r == 0 ? "#D9E5F5" : table.BandedRows && r % 2 == 0 ? "#F3F6FA" : null);
                var element = E("tc", E("tcPr", E("tcW", new XAttribute(W + "w", widths[c]), new XAttribute(W + "type", "dxa")), fill is null ? null : E("shd", V("clear"), new XAttribute(W + "fill", Hex(fill)))), Blocks(cell.Blocks));
                if (cell.Blocks.LastOrDefault() is not Paragraph) element.Add(E("p")); row.Add(element);
            }
            result.Add(row);
        }
        return result;
    }
    private XElement Picture(ImageBlock image)
    {
        var extension = image.ContentType == "image/jpeg" ? "jpg" : image.ContentType == "image/gif" ? "gif" : "png";
        var path = "media/image" + (_media.Count + 1) + "." + extension; var id = Relate("image", path); _media.Add((path, image.Data, image.ContentType));
        var cx = (long)(image.Width * 12700); var cy = (long)(image.Height * 12700);
        var nonVisual = new XElement(Pic + "nvPicPr", new XElement(Pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", image.AltText)), new XElement(Pic + "cNvPicPr"));
        var fill = new XElement(Pic + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", id)), new XElement(A + "stretch", new XElement(A + "fillRect")));
        var transform = new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy)));
        var shape = new XElement(Pic + "spPr", transform, new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst")));
        var picture = new XElement(Pic + "pic", nonVisual, fill, shape);
        var inline = new XElement(Wp + "inline", new XElement(Wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)), new XElement(Wp + "docPr", new XAttribute("id", _media.Count), new XAttribute("name", "Picture " + _media.Count), new XAttribute("descr", image.AltText)), new XElement(Wp + "cNvGraphicFramePr", new XElement(A + "graphicFrameLocks", new XAttribute("noChangeAspect", 1))), new XElement(A + "graphic", new XElement(A + "graphicData", new XAttribute("uri", Pic.NamespaceName), picture)));
        return E("p", E("pPr", E("jc", V(image.Alignment.ToString().ToLowerInvariant()))), E("r", E("drawing", inline)));
    }
}
