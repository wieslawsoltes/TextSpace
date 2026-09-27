using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.Documents;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed record DocxImportResult(DocumentModel Document, IReadOnlyList<string> Warnings);

/// <summary>Bounded, non-executing DOCX reader. External relationships are never downloaded.</summary>
public sealed partial class DocxReader
{
    private sealed record Relation(string Target, string Type, bool External);
    private ZipArchive _zip = null!;
    private string _main = "word/document.xml";
    private Dictionary<string, Relation> _relations = [];
    private readonly Dictionary<string, (TextStyle character, ParagraphFormat paragraph)> _styles = [];
    private readonly Dictionary<string, ListKind> _numbering = [];
    private readonly List<string> _warnings = [];
    private readonly Dictionary<string, int> _commentStarts = [], _commentEnds = [];
    private readonly Dictionary<string, (string Name, int Start)> _bookmarkStarts = [];
    private readonly Dictionary<string, int> _bookmarkEnds = [];
    private TextStyle _defaultStyle = new();
    private int _textPosition;
    private void Warn(string text) { if (!_warnings.Contains(text)) _warnings.Add(text); }
    public DocxImportResult Read(byte[] bytes)
    {
        if (bytes.Length > DocumentJson.MaxFileBytes) throw new InvalidDataException("DOCX files must be smaller than 32 MB.");
        using var input = new MemoryStream(bytes, false); using var zip = new ZipArchive(input, ZipArchiveMode.Read); _zip = zip;
        if (zip.Entries.Count > 10_000 || zip.Entries.Sum(e => e.Length) > 64L * 1024 * 1024 || zip.Entries.Any(e => e.Length > 32L * 1024 * 1024)) throw new InvalidDataException("The expanded DOCX exceeds the package safety limits.");
        _warnings.Clear(); _styles.Clear(); _numbering.Clear(); _commentStarts.Clear(); _commentEnds.Clear(); _bookmarkStarts.Clear(); _bookmarkEnds.Clear(); _defaultStyle = new(); _textPosition = 0;
        var rootRelationships = Xml("_rels/.rels");
        _main = rootRelationships?.Root?.Elements(Rel + "Relationship").Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/officeDocument", StringComparison.Ordinal) == true && (string?)e.Attribute("TargetMode") != "External").Select(e => Resolve("", (string?)e.Attribute("Target") ?? "")).FirstOrDefault() ?? "word/document.xml";
        var xml = Xml(_main) ?? throw new InvalidDataException("The package does not contain a Word document.");
        if (xml.Root?.Name != W + "document") throw new InvalidDataException("Only transitional WordprocessingML DOCX documents are currently supported.");
        var mainDirectory = _main.Contains('/') ? _main[..(_main.LastIndexOf('/') + 1)] : ""; var mainName = _main[mainDirectory.Length..];
        _relations = ReadRelations(mainDirectory + "_rels/" + mainName + ".rels", _main);
        LoadStyles(); LoadNumbering();
        var body = xml.Root.Element(W + "body") ?? throw new InvalidDataException("The document body is missing.");
        var document = new DocumentModel { Blocks = ReadBlocks(body).ToList() };
        if (!document.Blocks.OfType<Paragraph>().Any()) document.Blocks.Add(new Paragraph());
        var section = body.Descendants(W + "sectPr").LastOrDefault(); if (body.Descendants(W + "sectPr").Skip(1).Any()) Warn("Multiple sections use the last section's page settings throughout this document.");
        document.Page = ReadPage(section);
        document.Header = ReadHeaderFooter(section?.Elements(W + "headerReference").FirstOrDefault()); document.Footer = ReadHeaderFooter(section?.Elements(W + "footerReference").FirstOrDefault());
        var core = Xml("docProps/core.xml"); XNamespace dc = "http://purl.org/dc/elements/1.1/";
        document.Title = core?.Root?.Element(dc + "title")?.Value ?? "Imported document"; document.Author = core?.Root?.Element(dc + "creator")?.Value ?? "You";
        LoadComments(document); LoadBookmarks(document);
        if (xml.Descendants(W + "ins").Any() || xml.Descendants(W + "del").Any()) Warn("Word tracked changes were imported as their current visible text; revision history is not preserved.");
        if (xml.Descendants(W + "footnoteReference").Any() || xml.Descendants(W + "endnoteReference").Any()) Warn("Footnotes and endnotes are not imported in this version.");
        if (xml.Descendants(W + "altChunk").Any() || xml.Descendants(W + "object").Any()) Warn("Embedded objects and alternative-format content are not imported or executed.");
        DocumentJson.Validate(document); return new(document, _warnings.ToArray());
    }
    private static string Resolve(string basis, string target)
    {
        var root = new Uri("https://package.invalid/"); var uri = new Uri(new Uri(root, basis), target);
        if (uri.Host != root.Host) throw new InvalidDataException("Invalid internal package relationship.");
        return Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
    }
    private XDocument? Xml(string path)
    {
        var entry = _zip.GetEntry(path); if (entry is null) return null;
        using var stream = entry.Open(); using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 }); return XDocument.Load(reader);
    }
    private Dictionary<string, Relation> ReadRelations(string path, string basis)
    {
        var result = new Dictionary<string, Relation>(); var xml = Xml(path);
        foreach (var e in xml?.Root?.Elements(Rel + "Relationship") ?? [])
        {
            var id = (string?)e.Attribute("Id"); var target = (string?)e.Attribute("Target"); if (id is null || target is null) continue;
            var external = (string?)e.Attribute("TargetMode") == "External"; result[id] = new(external ? target : Resolve(basis, target), (string?)e.Attribute("Type") ?? "", external);
        }
        return result;
    }
    private XDocument? RelatedXml(string type) => _relations.Values.FirstOrDefault(r => !r.External && r.Type.EndsWith("/" + type, StringComparison.Ordinal)) is { } relation ? Xml(relation.Target) : null;
    private IEnumerable<Block> ReadBlocks(XElement parent)
    {
        foreach (var element in parent.Elements())
        {
            if (element.Name == W + "p")
            {
                var hasPageBreak = element.Descendants(W + "br").Any(b => (string?)b.Attribute(W + "type") == "page");
                var hasText = element.Descendants(W + "t").Any();
                var pictures = element.Descendants(Wp + "inline").Concat(element.Descendants(Wp + "anchor")).ToArray();
                if (hasPageBreak && !hasText && pictures.Length == 0) { yield return new PageBreakBlock(); continue; }
                if (hasText || pictures.Length == 0) yield return ReadParagraph(element);
                foreach (var drawing in pictures) if (ReadPicture(drawing) is { } image) yield return image;
                if (hasPageBreak) { yield return new PageBreakBlock(); Warn("Inline page breaks are normalized to block page breaks."); }
            }
            else if (element.Name == W + "tbl") yield return ReadTable(element);
            else if (element.Name == W + "sdt")
            {
                Warn("Content controls are imported as editable content without their control behavior.");
                if (element.Element(W + "sdtContent") is { } content) foreach (var block in ReadBlocks(content)) yield return block;
            }
        }
    }
    private Paragraph ReadParagraph(XElement element)
    {
        var properties = element.Element(W + "pPr"); var styleId = Val(properties?.Element(W + "pStyle")) ?? "Normal";
        var basis = _styles.TryGetValue(styleId, out var style) ? style : (_defaultStyle, new ParagraphFormat());
        var paragraph = new Paragraph { DefaultStyle = basis.Item1, Format = ReadParagraphFormat(properties, basis.Item2) };
        var numId = Val(properties?.Element(W + "numPr")?.Element(W + "numId"));
        if (numId is not null) paragraph.Format = paragraph.Format with { List = _numbering.GetValueOrDefault(numId, ListKind.Number), LeftIndent = Math.Max(0, paragraph.Format.LeftIndent - 18) };
        void Walk(XElement node, string? hyperlink = null)
        {
            foreach (var child in node.Elements())
            {
                if (child.Name == W + "del" || child.Name == W + "pPr") continue;
                if (child.Name == W + "bookmarkStart")
                {
                    var id = (string?)child.Attribute(W + "id"); var name = (string?)child.Attribute(W + "name");
                    if (id is not null && name is not null)
                    {
                        if (!_bookmarkStarts.TryAdd(id, (name, _textPosition + paragraph.Length))) Warn("A duplicate bookmark identifier was omitted.");
                    }
                }
                else if (child.Name == W + "bookmarkEnd")
                {
                    var id = (string?)child.Attribute(W + "id"); if (id is not null) _bookmarkEnds.TryAdd(id, _textPosition + paragraph.Length);
                }
                else if (child.Name == W + "commentRangeStart") { var id = (string?)child.Attribute(W + "id"); if (id is not null) _commentStarts[id] = _textPosition + paragraph.Length; }
                else if (child.Name == W + "commentRangeEnd") { var id = (string?)child.Attribute(W + "id"); if (id is not null) _commentEnds[id] = _textPosition + paragraph.Length; }
                else if (child.Name == W + "r")
                {
                    var runStyle = ReadTextStyle(child.Element(W + "rPr"), basis.Item1); if (hyperlink is not null) runStyle = runStyle with { Hyperlink = hyperlink, Underline = true, Color = "#0563C1" };
                    var text = string.Concat(child.Elements().Select(c => c.Name == W + "t" ? c.Value : c.Name == W + "tab" ? "\t" : c.Name == W + "br" && (string?)c.Attribute(W + "type") != "page" || c.Name == W + "cr" ? "\u2028" : ""));
                    if (text.Length > 0) paragraph.Runs.Add(new(text, runStyle));
                }
                else if (child.Name == W + "hyperlink")
                {
                    var id = (string?)child.Attribute(R + "id"); var anchor = (string?)child.Attribute(W + "anchor");
                    var link = anchor is not null ? "#" + anchor : id is not null && _relations.TryGetValue(id, out var relation) ? SafeLink(relation.Target) : null; Walk(child, link);
                }
                else Walk(child, hyperlink);
            }
        }
        Walk(element); paragraph.Normalize(); _textPosition += paragraph.Length + 1; return paragraph;
    }
    private TableBlock ReadTable(XElement element)
    {
        var table = new TableBlock { Rows = [], BandedRows = false, HeaderRow = false, ColumnWidths = element.Element(W + "tblGrid")?.Elements(W + "gridCol").Select(c => Number((string?)c.Attribute(W + "w"), 1)).ToList() ?? [] };
        if (element.Descendants(W + "vMerge").Any() || element.Descendants(W + "gridSpan").Any()) Warn("Merged table cells are normalized to independent cells.");
        foreach (var rowElement in element.Elements(W + "tr"))
        {
            var row = new TableRow(); if (table.Rows.Count == 0) table.HeaderRow = rowElement.Element(W + "trPr")?.Element(W + "tblHeader") is not null;
            foreach (var cellElement in rowElement.Elements(W + "tc"))
            {
                var blocks = ReadBlocks(cellElement).ToList(); if (blocks.Count == 0) { blocks.Add(new Paragraph()); _textPosition++; }
                if (blocks.Any(b => b is TableBlock)) Warn("Nested tables retain their structure but their inner layout is simplified.");
                var fill = (string?)cellElement.Element(W + "tcPr")?.Element(W + "shd")?.Attribute(W + "fill");
                row.Cells.Add(new() { Blocks = blocks, Shading = fill is { Length: 6 } && fill.All(Uri.IsHexDigit) ? "#" + fill : null });
            }
            if (row.Cells.Count > 0) table.Rows.Add(row);
        }
        if (table.Rows.Count == 0) return TableBlock.Create(1, 1); return table;
    }
    private ImageBlock? ReadPicture(XElement drawing)
    {
        var blip = drawing.Descendants(A + "blip").FirstOrDefault(); var id = (string?)blip?.Attribute(R + "embed");
        if (id is null || !_relations.TryGetValue(id, out var relation) || relation.External) { Warn("Linked or unsupported pictures were not downloaded."); return null; }
        var entry = _zip.GetEntry(relation.Target); if (entry is null || entry.Length > 16 * 1024 * 1024) { Warn("A missing or oversized picture was skipped."); return null; }
        var extension = Path.GetExtension(relation.Target).ToLowerInvariant(); var type = extension is ".jpg" or ".jpeg" ? "image/jpeg" : extension == ".png" ? "image/png" : extension == ".gif" ? "image/gif" : null;
        if (type is null) { Warn("Only PNG, JPEG and GIF pictures are imported."); return null; }
        using var stream = entry.Open(); using var bytes = new MemoryStream(); stream.CopyTo(bytes); var extent = drawing.Element(Wp + "extent");
        if (drawing.Name == Wp + "anchor") Warn("Floating pictures are imported as in-flow picture blocks.");
        return new() { Data = bytes.ToArray(), ContentType = type, Width = Math.Clamp(Number((string?)extent?.Attribute("cx"), 4572000) / 12700, 1, 4000), Height = Math.Clamp(Number((string?)extent?.Attribute("cy"), 3048000) / 12700, 1, 4000), AltText = (string?)drawing.Element(Wp + "docPr")?.Attribute("descr") ?? "Picture" };
    }
    private void LoadComments(DocumentModel document)
    {
        foreach (var comment in RelatedXml("comments")?.Root?.Elements(W + "comment") ?? [])
        {
            var id = (string?)comment.Attribute(W + "id"); if (id is null) continue;
            var start = Math.Clamp(_commentStarts.GetValueOrDefault(id), 0, document.PlainText.Length); var end = Math.Clamp(_commentEnds.GetValueOrDefault(id, start), start, document.PlainText.Length);
            document.Comments.Add(new() { Start = start, End = end, Author = (string?)comment.Attribute(W + "author") ?? "Reviewer", Text = string.Join("\n", comment.Elements(W + "p").Select(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)))) });
        }
    }
    private string ReadHeaderFooter(XElement? reference)
    {
        var id = (string?)reference?.Attribute(R + "id"); if (id is null || !_relations.TryGetValue(id, out var relation) || relation.External) return "";
        var xml = Xml(relation.Target); if (xml?.Root is null) return "";
        var text = new System.Text.StringBuilder();
        foreach (var p in xml.Root.Elements(W + "p"))
        {
            if (text.Length > 0) text.Append(' ');
            foreach (var child in p.Elements())
            {
                if (child.Name == W + "fldSimple")
                {
                    var instruction = ((string?)child.Attribute(W + "instr") ?? "").Trim(); text.Append(instruction.StartsWith("NUMPAGES") ? "{NUMPAGES}" : instruction.StartsWith("PAGE") ? "{PAGE}" : string.Concat(child.Descendants(W + "t").Select(t => t.Value)));
                }
                else text.Append(string.Concat(child.Descendants(W + "t").Select(t => t.Value)));
            }
        }
        return text.ToString();
    }
}
