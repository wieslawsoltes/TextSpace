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
    private ParagraphFormat _defaultParagraph = new();
    private int _textPosition;
    private void Warn(string text) { if (!_warnings.Contains(text)) _warnings.Add(text); }
    public DocxImportResult Read(byte[] bytes)
    {
        if (bytes.Length > DocumentJson.MaxFileBytes) throw new InvalidDataException("DOCX files must be smaller than 32 MB.");
        using var input = new MemoryStream(bytes, false); using var zip = new ZipArchive(input, ZipArchiveMode.Read); _zip = zip;
        if (zip.Entries.Count > 10_000 || zip.Entries.Sum(e => e.Length) > 64L * 1024 * 1024 || zip.Entries.Any(e => e.Length > 32L * 1024 * 1024)) throw new InvalidDataException("The expanded DOCX exceeds the package safety limits.");
        _warnings.Clear(); _styles.Clear(); _numbering.Clear(); _commentStarts.Clear(); _commentEnds.Clear(); _bookmarkStarts.Clear(); _bookmarkEnds.Clear(); _defaultStyle = new(); _defaultParagraph = new(); _textPosition = 0; _importedFields.Clear(); _sectionCursor = 0;
        var rootRelationships = Xml("_rels/.rels");
        _main = rootRelationships?.Root?.Elements(Rel + "Relationship").Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/officeDocument", StringComparison.Ordinal) == true && (string?)e.Attribute("TargetMode") != "External").Select(e => Resolve("", (string?)e.Attribute("Target") ?? "")).FirstOrDefault() ?? "word/document.xml";
        var xml = Xml(_main) ?? throw new InvalidDataException("The package does not contain a Word document.");
        if (xml.Root?.Name != W + "document") throw new InvalidDataException("Only transitional WordprocessingML DOCX documents are currently supported.");
        var mainDirectory = _main.Contains('/') ? _main[..(_main.LastIndexOf('/') + 1)] : ""; var mainName = _main[mainDirectory.Length..];
        _relations = ReadRelations(mainDirectory + "_rels/" + mainName + ".rels", _main);
        LoadStyles(); LoadNumbering();
        var body = xml.Root.Element(W + "body") ?? throw new InvalidDataException("The document body is missing.");
        ReadSectionDefinitions(body);
        var firstSection = _sections[0];
        var document = new DocumentModel
        {
            Blocks = ReadBlocks(body).ToList(), Page = firstSection.Page, Header = firstSection.Header ?? "", Footer = firstSection.Footer ?? "",
            SectionOptions = firstSection.Options, Fields = _importedFields.ToList(),
            DefaultTabStop = Number(Val(RelatedXml("settings")?.Root?.Element(W + "defaultTabStop")), 720) / 20
        };
        if (!document.Blocks.OfType<Paragraph>().Any()) document.Blocks.Add(new Paragraph());
        var core = Xml("docProps/core.xml"); XNamespace dc = "http://purl.org/dc/elements/1.1/";
        document.Title = core?.Root?.Element(dc + "title")?.Value ?? "Imported document"; document.Author = core?.Root?.Element(dc + "creator")?.Value ?? "You";
        document.Subject = core?.Root?.Element(dc + "subject")?.Value ?? "";
        XNamespace dcterms = "http://purl.org/dc/terms/";
        if (DateTimeOffset.TryParse(core?.Root?.Element(dcterms + "created")?.Value, out var created)) document.Created = created;
        if (DateTimeOffset.TryParse(core?.Root?.Element(dcterms + "modified")?.Value, out var modified)) document.Modified = modified;
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
                var hasColumnBreak = element.Descendants(W + "br").Any(b => (string?)b.Attribute(W + "type") == "column");
                var hasText = element.Descendants(W + "t").Any() || element.Descendants(W + "fldChar").Any() || element.Descendants(W + "fldSimple").Any();
                var pictures = element.Descendants(Wp + "inline").Concat(element.Descendants(Wp + "anchor")).ToArray();
                if ((hasPageBreak || hasColumnBreak) && !hasText && pictures.Length == 0)
                    yield return hasPageBreak ? new PageBreakBlock() : new ColumnBreakBlock();
                else
                {
                    if (hasText || pictures.Length == 0) yield return ReadParagraph(element);
                    foreach (var drawing in pictures) if (ReadPicture(drawing) is { } image) yield return image;
                    if (hasPageBreak) { yield return new PageBreakBlock(); Warn("Inline page breaks are normalized to block page breaks."); }
                    if (hasColumnBreak) { yield return new ColumnBreakBlock(); Warn("Inline column breaks are normalized to block column breaks."); }
                }
                if (parent.Name == W + "body" && element.Element(W + "pPr")?.Element(W + "sectPr") is not null)
                {
                    _sectionCursor++;
                    yield return new SectionBreakBlock { Kind = _sectionKinds[_sectionCursor], Section = _sections[_sectionCursor] };
                }
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
        var basis = _styles.TryGetValue(styleId, out var style) ? style : (_defaultStyle, _defaultParagraph);
        var paragraph = new Paragraph { DefaultStyle = basis.Item1, Format = ReadParagraphFormat(properties, basis.Item2) };
        var numId = Val(properties?.Element(W + "numPr")?.Element(W + "numId"));
        if (numId is not null) paragraph.Format = paragraph.Format with { List = _numbering.GetValueOrDefault(numId, ListKind.Number), LeftIndent = Math.Max(0, paragraph.Format.LeftIndent - 18) };
        var fieldStack = new Stack<PendingField>();
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
                    foreach (var token in child.Elements())
                    {
                        if (ReadFieldToken(token, paragraph, fieldStack)) continue;
                        if (fieldStack.Any(f => !f.InResult)) continue;
                        var text = token.Name == W + "t" ? token.Value : token.Name == W + "tab" ? "\t"
                            : token.Name == W + "noBreakHyphen" ? "\u2011" : token.Name == W + "softHyphen" ? "\u00ad"
                            : token.Name == W + "cr" || token.Name == W + "br" && (string?)token.Attribute(W + "type") is not ("page" or "column") ? "\u2028" : "";
                        if (text.Length > 0) paragraph.Runs.Add(new(text, runStyle));
                    }
                }
                else if (child.Name == W + "fldSimple")
                {
                    var start = _textPosition + paragraph.Length; var countBefore = _importedFields.Count;
                    Walk(child, hyperlink);
                    if (fieldStack.Count == 0 && countBefore == _importedFields.Count)
                        AddImportedField((string?)child.Attribute(W + "instr") ?? "", start, paragraph, FlagAttribute(child, "fldLock"));
                    else Warn("Nested fields retain their visible results; overlapping field behavior is not preserved.");
                }
                else if (child.Name == W + "hyperlink")
                {
                    var id = (string?)child.Attribute(R + "id"); var anchor = (string?)child.Attribute(W + "anchor");
                    var link = anchor is not null ? "#" + anchor : id is not null && _relations.TryGetValue(id, out var relation) ? SafeLink(relation.Target) : null; Walk(child, link);
                }
                else Walk(child, hyperlink);
            }
        }
        Walk(element);
        if (fieldStack.Count > 0) Warn("An unclosed field was imported as visible cached text.");
        paragraph.Normalize(); _textPosition += paragraph.Length + 1; return paragraph;
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
        var stack = new Stack<(System.Text.StringBuilder Code, bool Result, bool Substitute)>();
        static string? Placeholder(string code)
        {
            try
            {
                var parsed = FieldInstruction.Parse(code);
                return parsed.Supported && parsed.NumberFormat is null && parsed.Name is "PAGE" or "NUMPAGES" or "SECTION" or "SECTIONPAGES" or "TITLE" or "AUTHOR" ? "{" + parsed.Name + "}" : null;
            }
            catch (FormatException) { return null; }
        }
        void Append(string value)
        {
            if (stack.Any(f => !f.Result || f.Substitute)) return;
            if (text.Length + value.Length > 8000) throw new InvalidDataException("Header or footer exceeds 8,000 characters.");
            text.Append(value);
        }
        void Walk(XElement node)
        {
            foreach (var child in node.Elements())
            {
                if (child.Name == W + "del" || child.Name == W + "pPr" || child.Name == W + "rPr") continue;
                if (child.Name == W + "fldSimple")
                {
                    var placeholder = Placeholder((string?)child.Attribute(W + "instr") ?? "");
                    if (placeholder is not null) Append(placeholder);
                    else { Warn("Unsupported header/footer field formatting is retained as cached text."); Walk(child); }
                }
                else if (child.Name == W + "fldChar")
                {
                    var kind = (string?)child.Attribute(W + "fldCharType");
                    if (kind == "begin")
                    {
                        if (stack.Count >= 64) throw new InvalidDataException("Header/footer field nesting exceeds 64.");
                        stack.Push((new(), false, false));
                    }
                    else if (kind == "separate" && stack.Count > 0)
                    {
                        var field = stack.Pop(); var placeholder = Placeholder(field.Code.ToString());
                        if (placeholder is not null) Append(placeholder);
                        else Warn("Unsupported header/footer field instructions are retained as cached text.");
                        stack.Push((field.Code, true, placeholder is not null));
                    }
                    else if (kind == "end" && stack.Count > 0) stack.Pop();
                }
                else if (child.Name == W + "instrText")
                {
                    if (stack.TryPeek(out var field))
                    {
                        if (field.Code.Length + child.Value.Length > 1024) throw new InvalidDataException("Header/footer field instruction exceeds 1,024 characters.");
                        field.Code.Append(child.Value);
                    }
                }
                else if (child.Name == W + "t") Append(child.Value);
                else if (child.Name == W + "tab") Append("\t");
                else if (child.Name == W + "softHyphen") Append("\u00ad");
                else if (child.Name == W + "noBreakHyphen") Append("\u2011");
                else if (child.Name == W + "br" || child.Name == W + "cr") Append("\n");
                else if (child.Name == W + "drawing" || child.Name == W + "object" || child.Name == W + "tbl")
                    Warn("Rich header/footer objects are not imported; supported story text is retained.");
                else Walk(child);
            }
        }
        var first = true;
        foreach (var paragraph in xml.Root.Elements(W + "p"))
        {
            if (!first) Append("\n"); first = false; Walk(paragraph);
        }
        if (stack.Count > 0) Warn("An incomplete header/footer field was normalized to supported visible text.");
        if (xml.Root.Elements().Any(e => e.Name == W + "tbl")) Warn("Header/footer tables are not imported.");
        return text.ToString();
    }
}
