using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.Documents;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

/// <summary>Writes a standards-based .docx package. Local tracked-edit history is deliberately not represented as Word revisions.</summary>
public sealed class DocxWriter
{
    private readonly List<XElement> _relationships = [];
    private readonly List<(string path, byte[] bytes, string type)> _media = [];
    private int _nextId;
    private DocumentModel _document = null!;
    private TextIndex _index = null!;
    private string Relate(string kind, string target, bool external = false)
    {
        var id = "rId" + ++_nextId;
        _relationships.Add(new(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", RelationshipType(kind)), new XAttribute("Target", target), external ? new XAttribute("TargetMode", "External") : null));
        return id;
    }
    public byte[] Write(DocumentModel document)
    {
        DocumentJson.Validate(document); _document = document; _index = new(document); _relationships.Clear(); _media.Clear(); _nextId = 0;
        Relate("styles", "styles.xml"); Relate("numbering", "numbering.xml"); Relate("settings", "settings.xml");
        var headerId = string.IsNullOrEmpty(document.Header) ? null : Relate("header", "header1.xml");
        var footerId = string.IsNullOrEmpty(document.Footer) ? null : Relate("footer", "footer1.xml");
        if (document.Comments.Count > 0) Relate("comments", "comments.xml");
        var body = E("body", Blocks(document.Blocks)); var page = document.Page;
        body.Add(E("sectPr", headerId is null ? null : E("headerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", headerId)), footerId is null ? null : E("footerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", footerId)),
            E("pgSz", new XAttribute(W + "w", Twips(page.Width)), new XAttribute(W + "h", Twips(page.Height)), page.Width > page.Height ? new XAttribute(W + "orient", "landscape") : null),
            E("pgMar", new XAttribute(W + "top", Twips(page.MarginTop)), new XAttribute(W + "right", Twips(page.MarginRight)), new XAttribute(W + "bottom", Twips(page.MarginBottom)), new XAttribute(W + "left", Twips(page.MarginLeft)), new XAttribute(W + "header", Twips(page.HeaderDistance)), new XAttribute(W + "footer", Twips(page.FooterDistance)), new XAttribute(W + "gutter", 0)),
            E("cols", new XAttribute(W + "num", page.Columns), new XAttribute(W + "space", Twips(page.ColumnGap)))));
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip, "word/document.xml", new XDocument(E("document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), new XAttribute(XNamespace.Xmlns + "wp", Wp), new XAttribute(XNamespace.Xmlns + "a", A), new XAttribute(XNamespace.Xmlns + "pic", Pic), body)));
            Add(zip, "word/styles.xml", Styles()); Add(zip, "word/numbering.xml", Numbering()); Add(zip, "word/settings.xml", new(E("settings", E("zoom", new XAttribute(W + "percent", 100)), E("defaultTabStop", V(720)), E("compat"))));
            if (headerId is not null) Add(zip, "word/header1.xml", new(E("hdr", HeaderFooter(document.Header))));
            if (footerId is not null) Add(zip, "word/footer1.xml", new(E("ftr", HeaderFooter(document.Footer, true))));
            if (document.Comments.Count > 0)
                Add(zip, "word/comments.xml", new(E("comments", document.Comments.Select((c, i) => E("comment", new XAttribute(W + "id", i), new XAttribute(W + "author", c.Author), new XAttribute(W + "date", c.Created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")), E("p", E("r", E("t", c.Text + (c.Replies.Count > 0 ? "\n\n" + string.Join("\n", c.Replies) : ""))))))));
            Add(zip, "word/_rels/document.xml.rels", new(new XElement(Rel + "Relationships", _relationships)));
            foreach (var (path, bytes, _) in _media) { using var stream = zip.CreateEntry("word/" + path, CompressionLevel.Optimal).Open(); stream.Write(bytes); }
            XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties", dc = "http://purl.org/dc/elements/1.1/", dct = "http://purl.org/dc/terms/", xsi = "http://www.w3.org/2001/XMLSchema-instance";
            Add(zip, "docProps/core.xml", new(new XElement(cp + "coreProperties", new XAttribute(XNamespace.Xmlns + "dc", dc), new XAttribute(XNamespace.Xmlns + "dcterms", dct), new XAttribute(XNamespace.Xmlns + "xsi", xsi), new XElement(dc + "title", document.Title), new XElement(dc + "creator", document.Author), new XElement(dc + "subject", document.Subject), new XElement(dct + "created", new XAttribute(xsi + "type", "dcterms:W3CDTF"), document.Created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")), new XElement(dct + "modified", new XAttribute(xsi + "type", "dcterms:W3CDTF"), document.Modified.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")))));
            XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
            Add(zip, "docProps/app.xml", new(new XElement(ep + "Properties", new XElement(ep + "Application", "TextSpace"), new XElement(ep + "AppVersion", "0.1"))));
            Add(zip, "_rels/.rels", new(new XElement(Rel + "Relationships", new XElement(Rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", RelationshipType("officeDocument")), new XAttribute("Target", "word/document.xml")), new XElement(Rel + "Relationship", new XAttribute("Id", "rId2"), new XAttribute("Type", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"), new XAttribute("Target", "docProps/core.xml")), new XElement(Rel + "Relationship", new XAttribute("Id", "rId3"), new XAttribute("Type", RelationshipType("extended-properties")), new XAttribute("Target", "docProps/app.xml")))));
            var types = new XElement(Ct + "Types", new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(Ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")));
            void Override(string path, string type) => types.Add(new XElement(Ct + "Override", new XAttribute("PartName", "/" + path), new XAttribute("ContentType", type)));
            Override("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
            foreach (var part in new[] { "styles", "numbering", "settings" }) Override("word/" + part + ".xml", "application/vnd.openxmlformats-officedocument.wordprocessingml." + part + "+xml");
            if (headerId is not null) Override("word/header1.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml");
            if (footerId is not null) Override("word/footer1.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml");
            if (document.Comments.Count > 0) Override("word/comments.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml");
            Override("docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml"); Override("docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
            foreach (var media in _media) Override("word/" + media.path, media.type);
            Add(zip, "[Content_Types].xml", new(types));
        }
        return output.ToArray();
    }
    private static void Add(ZipArchive zip, string path, XDocument document)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open(); using var writer = new StreamWriter(stream, new UTF8Encoding(false)); document.Save(writer, SaveOptions.DisableFormatting);
    }
    private IEnumerable<XElement> Blocks(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph p: yield return Paragraph(p); break;
                case PageBreakBlock: yield return E("p", E("r", E("br", new XAttribute(W + "type", "page")))); break;
                case ImageBlock image: yield return Picture(image); break;
                case TableBlock table: yield return Table(table); break;
            }
        }
    }
    private XElement Paragraph(Paragraph paragraph)
    {
        var result = E("p", ParagraphProperties(paragraph.Format)); var start = _index.StartOf(paragraph); var position = 0;
        void Mark(int offset)
        {
            foreach (var (comment, id) in _document.Comments.Select((c, i) => (c, i)))
            {
                if (comment.Start == start + offset) result.Add(E("commentRangeStart", new XAttribute(W + "id", id)));
                if (comment.End == start + offset) result.Add(E("commentRangeEnd", new XAttribute(W + "id", id)), E("r", E("commentReference", new XAttribute(W + "id", id))));
            }
        }
        Mark(0);
        foreach (var run in paragraph.Runs)
        {
            var boundaries = _document.Comments.SelectMany(c => new[] { c.Start - start, c.End - start }).Where(i => i > position && i < position + run.Text.Length).Append(position + run.Text.Length).Distinct().Order().ToArray();
            var at = position;
            foreach (var end in boundaries)
            {
                var content = Run(run.Text.Substring(at - position, end - at), run.Style);
                var link = SafeLink(run.Style.Hyperlink);
                if (link?.StartsWith('#') == true) result.Add(E("hyperlink", new XAttribute(W + "anchor", link[1..]), content));
                else if (link is not null) result.Add(E("hyperlink", new XAttribute(R + "id", Relate("hyperlink", link, true)), content));
                else result.Add(content);
                at = end; Mark(end);
            }
            position += run.Text.Length;
        }
        if (paragraph.Runs.Count == 0) result.Add(E("r", RunProperties(paragraph.DefaultStyle)));
        return result;
    }
    internal static XElement Run(string text, TextStyle style)
    {
        var result = E("r", RunProperties(style)); var buffer = new StringBuilder();
        void Flush() { if (buffer.Length > 0) { result.Add(E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), buffer.ToString())); buffer.Clear(); } }
        foreach (var c in text) { if (c is '\t' or '\u2028') { Flush(); result.Add(E(c == '\t' ? "tab" : "br")); } else buffer.Append(c); }
        Flush(); return result;
    }
    internal static XElement RunProperties(TextStyle s) => E("rPr", E("rFonts", new XAttribute(W + "ascii", s.FontFamily), new XAttribute(W + "hAnsi", s.FontFamily), new XAttribute(W + "cs", s.FontFamily)), s.Bold ? E("b") : null, s.Italic ? E("i") : null, s.StrikeThrough ? E("strike") : null, E("color", V(Hex(s.Color))), E("sz", V((int)Math.Round(s.FontSize * 2))), E("szCs", V((int)Math.Round(s.FontSize * 2))), s.Underline || s.Hyperlink is not null ? E("u", V("single")) : null, s.Highlight is not null ? E("shd", V("clear"), new XAttribute(W + "fill", Hex(s.Highlight))) : null, s.Superscript || s.Subscript ? E("vertAlign", V(s.Superscript ? "superscript" : "subscript")) : null);
    internal static XElement ParagraphProperties(ParagraphFormat f) => E("pPr", E("pStyle", V(f.StyleName.Replace(" ", ""))), f.KeepWithNext ? E("keepNext") : null, f.PageBreakBefore ? E("pageBreakBefore") : null, f.List != ListKind.None ? E("numPr", E("ilvl", V(f.ListLevel)), E("numId", V(f.List == ListKind.Bullet ? 1 : 2))) : null, f.BorderBottom ? E("pBdr", E("bottom", V("single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "8E9EAD"))) : null, f.Shading is not null ? E("shd", V("clear"), new XAttribute(W + "fill", Hex(f.Shading))) : null, E("spacing", new XAttribute(W + "before", Twips(f.SpaceBefore)), new XAttribute(W + "after", Twips(f.SpaceAfter)), new XAttribute(W + "line", (int)Math.Round(f.LineSpacing * 240)), new XAttribute(W + "lineRule", "auto")), E("ind", new XAttribute(W + "left", Twips(f.LeftIndent + (f.List != ListKind.None ? 18 : 0))), new XAttribute(W + "right", Twips(f.RightIndent)), f.FirstLineIndent >= 0 ? new XAttribute(W + "firstLine", Twips(f.FirstLineIndent)) : new XAttribute(W + "hanging", Twips(-f.FirstLineIndent))), E("jc", V(f.Alignment == TextAlignment.Justify ? "both" : f.Alignment.ToString().ToLowerInvariant())), f.OutlineLevel > 0 ? E("outlineLvl", V(f.OutlineLevel - 1)) : null);
    private static XElement HeaderFooter(string text, bool centered = false)
    {
        var p = E("p", E("pPr", E("jc", V(centered ? "center" : "left")))); var style = new TextStyle { FontSize = 8, Color = "#777777" };
        foreach (var part in System.Text.RegularExpressions.Regex.Split(text, @"(\{PAGE\}|\{NUMPAGES\})"))
            p.Add(part is "{PAGE}" or "{NUMPAGES}" ? E("fldSimple", new XAttribute(W + "instr", part.Trim('{', '}')), Run("1", style)) : Run(part, style));
        return p;
    }
    private XElement Table(TableBlock table)
    {
        var count = table.Rows.Max(r => r.Cells.Count); var weights = table.ColumnWidths.Count == count && table.ColumnWidths.All(w => w > 0 && double.IsFinite(w)) ? table.ColumnWidths.ToArray() : Enumerable.Repeat(1d, count).ToArray();
        var widths = weights.Select(w => Twips(_document.Page.ColumnWidth * w / weights.Sum())).ToArray();
        var borders = E("tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side => E(side, V("single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "A8B7C8"))));
        var result = E("tbl", E("tblPr", E("tblW", new XAttribute(W + "w", widths.Sum()), new XAttribute(W + "type", "dxa")), borders, E("tblLayout", new XAttribute(W + "type", "fixed")), E("tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(side => E(side, new XAttribute(W + "w", Twips(table.CellPadding)), new XAttribute(W + "type", "dxa"))))), E("tblGrid", widths.Select(w => E("gridCol", new XAttribute(W + "w", w)))));
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
        var picture = new XElement(Pic + "pic", new XElement(Pic + "nvPicPr", new XElement(Pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", image.AltText)), new XElement(Pic + "cNvPicPr")), new XElement(Pic + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", id)), new XElement(A + "stretch", new XElement(A + "fillRect"))), new XElement(Pic + "spPr", new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))), new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst"))));
        return E("p", E("pPr", E("jc", V(image.Alignment.ToString().ToLowerInvariant()))), E("r", E("drawing", new XElement(Wp + "inline", new XElement(Wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)), new XElement(Wp + "docPr", new XAttribute("id", _media.Count), new XAttribute("name", "Picture " + _media.Count), new XAttribute("descr", image.AltText)), new XElement(Wp + "cNvGraphicFramePr", new XElement(A + "graphicFrameLocks", new XAttribute("noChangeAspect", 1))), new XElement(A + "graphic", new XElement(A + "graphicData", new XAttribute("uri", Pic.NamespaceName), picture))))));
    }
    private static XDocument Styles() => new(E("styles", E("docDefaults", E("rPrDefault", RunProperties(new())), E("pPrDefault", ParagraphProperties(new()))), DocumentStyles.BuiltIn.Select(s => E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", s.Name.Replace(" ", "")), s.Name == "Normal" ? new XAttribute(W + "default", 1) : null, E("name", V(s.Name)), s.Name != "Normal" ? E("basedOn", V("Normal")) : null, E("next", V("Normal")), E("qFormat"), ParagraphProperties(s.Paragraph with { StyleName = s.Name }), RunProperties(s.Character)))));
    private static XDocument Numbering()
    {
        var root = E("numbering");
        for (var kind = 0; kind < 2; kind++)
        {
            var abs = E("abstractNum", new XAttribute(W + "abstractNumId", kind), E("multiLevelType", V("multilevel")));
            for (var level = 0; level < 9; level++) abs.Add(E("lvl", new XAttribute(W + "ilvl", level), E("start", V(1)), E("numFmt", V(kind == 0 ? "bullet" : "decimal")), E("lvlText", V(kind == 0 ? "•" : "%" + (level + 1) + ".")), E("lvlJc", V("left")), E("pPr", E("ind", new XAttribute(W + "left", 360 * (level + 1)), new XAttribute(W + "hanging", 180)))));
            root.Add(abs);
        }
        root.Add(E("num", new XAttribute(W + "numId", 1), E("abstractNumId", V(0))), E("num", new XAttribute(W + "numId", 2), E("abstractNumId", V(1)))); return new(root);
    }
}
