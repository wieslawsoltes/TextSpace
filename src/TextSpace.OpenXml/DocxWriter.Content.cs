using System.Text;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.Documents;
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
                case ColumnBreakBlock: yield return E("p", E("r", E("br", new XAttribute(W + "type", "column")))); break;
                case VisualBlock visual: yield return VisualObject(visual); break;
                case TableBlock table: yield return Table(table); break;
            }
        }
    }
    private XElement WriteParagraph(Paragraph paragraph)
    {
        var result = E("p", ParagraphProperties(paragraph.Format, columnWidth: _availableTableWidth)); var start = _index.StartOf(paragraph); var position = 0;
        static bool Locked(DocumentField field)
        {
            try { return field.Locked || !FieldInstruction.Parse(field.Instruction).Supported; }
            catch (FormatException) { return true; }
        }
        var fields = _document.Fields.Where(f => f.Start >= start && f.End <= start + paragraph.Length).ToArray();
        void Mark(int offset)
        {
            foreach (var field in fields.Where(f => f.End == start + offset)) result.Add(E("r", E("fldChar", new XAttribute(W + "fldCharType", "end"))));
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
            foreach (var field in fields.Where(f => f.Start == start + offset))
                result.Add(E("r", E("fldChar", new XAttribute(W + "fldCharType", "begin"), Locked(field) ? new XAttribute(W + "fldLock", "true") : null)),
                    E("r", E("instrText", new XAttribute(XNamespace.Xml + "space", "preserve"), " " + field.Instruction + " ")),
                    E("r", E("fldChar", new XAttribute(W + "fldCharType", "separate"))));
        }
        Mark(0);
        foreach (var run in paragraph.Runs)
        {
            var boundaries = _document.Comments.SelectMany(c => new[] { c.Start - start, c.End - start })
                .Concat(_document.Bookmarks.SelectMany(b => new[] { b.Start - start, b.End - start }))
                .Concat(fields.SelectMany(f => new[] { f.Start - start, f.End - start }))
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
        foreach (var c in text)
        {
            var element = c switch { '\t' => "tab", '\u2028' => "br", '\u00ad' => "softHyphen", '\u2011' => "noBreakHyphen", _ => null };
            if (element is null) buffer.Append(c); else { Flush(); result.Add(E(element)); }
        }
        Flush(); return result;
    }
    internal static XElement RunProperties(TextStyle s) => E("rPr",
        E("rFonts", new XAttribute(W + "ascii", s.FontFamily), new XAttribute(W + "hAnsi", s.FontFamily), new XAttribute(W + "cs", s.FontFamily)),
        s.Bold ? E("b") : null, s.Italic ? E("i") : null, s.StrikeThrough ? E("strike") : null,
        E("color", V(Hex(s.Color))), E("sz", V((int)Math.Round(s.FontSize * 2))), E("szCs", V((int)Math.Round(s.FontSize * 2))),
        s.Underline || s.Hyperlink is not null ? E("u", V("single")) : null,
        s.Highlight is not null ? E("shd", V("clear"), new XAttribute(W + "fill", Hex(s.Highlight))) : null,
        s.Superscript || s.Subscript ? E("vertAlign", V(s.Superscript ? "superscript" : "subscript")) : null);
    internal static XElement ParagraphProperties(ParagraphFormat f, bool includeStyle = true, double columnWidth = 468) => E("pPr",
        includeStyle ? E("pStyle", V(f.StyleName.Replace(" ", ""))) : null,
        E("keepNext", V(f.KeepWithNext ? "true" : "false")), E("keepLines", V(f.KeepLinesTogether ? "true" : "false")),
        E("pageBreakBefore", V(f.PageBreakBefore ? "true" : "false")), E("widowControl", V(f.WidowControl ? "true" : "false")),
        f.List != ListKind.None ? E("numPr", E("ilvl", V(f.ListLevel)), E("numId", V(f.List == ListKind.Bullet ? 1 : 2))) : null,
        f.BorderBottom ? E("pBdr", E("bottom", V("single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "8E9EAD"))) : null,
        f.Shading is not null ? E("shd", V("clear"), new XAttribute(W + "fill", Hex(f.Shading))) : null,
        WriteTabs(f, columnWidth),
        E("spacing", new XAttribute(W + "before", Twips(f.SpaceBefore)), new XAttribute(W + "after", Twips(f.SpaceAfter)), new XAttribute(W + "line", (int)Math.Round(f.LineSpacing * 240)), new XAttribute(W + "lineRule", "auto")),
        E("ind", new XAttribute(W + "left", Twips(f.LeftIndent + (f.List != ListKind.None ? 18 : 0))), new XAttribute(W + "right", Twips(f.RightIndent)), f.FirstLineIndent >= 0 ? new XAttribute(W + "firstLine", Twips(f.FirstLineIndent)) : new XAttribute(W + "hanging", Twips(-f.FirstLineIndent))),
        E("jc", V(f.Alignment == TextAlignment.Justify ? "both" : f.Alignment.ToString().ToLowerInvariant())), f.OutlineLevel > 0 ? E("outlineLvl", V(f.OutlineLevel - 1)) : null);
    private XElement HeaderFooter(string text, bool centered = false)
    {
        text = text.Replace("\r\n", "\u2028").Replace('\n', '\u2028').Replace('\r', '\u2028');
        var p = E("p", E("pPr", E("jc", V(centered ? "center" : "left")))); var style = new TextStyle { FontSize = 8, Color = "#777777" };
        foreach (var part in System.Text.RegularExpressions.Regex.Split(text, @"(\{PAGE\}|\{NUMPAGES\}|\{SECTION\}|\{SECTIONPAGES\}|\{TITLE\}|\{AUTHOR\})")) p.Add(part is "{PAGE}" or "{NUMPAGES}" or "{SECTION}" or "{SECTIONPAGES}" or "{TITLE}" or "{AUTHOR}" ? E("fldSimple", new XAttribute(W + "instr", part.Trim('{', '}')), Run(part == "{TITLE}" ? _document.Title : part == "{AUTHOR}" ? _document.Author : "1", style)) : Run(part, style)); return p;
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
