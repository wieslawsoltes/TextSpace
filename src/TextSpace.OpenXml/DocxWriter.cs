using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.Documents;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

/// <summary>Writes a standards-based DOCX package. Local tracked-edit history is not represented as Word revisions.</summary>
public sealed partial class DocxWriter
{
    private readonly List<XElement> _relationships = [];
    private readonly List<(string Path, byte[] Bytes, string Type)> _media = [];
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
        DocumentJson.Validate(document);
        _document = document; _index = new(document); _relationships.Clear(); _media.Clear(); _nextId = 0;
        Relate("styles", "styles.xml"); Relate("numbering", "numbering.xml"); Relate("settings", "settings.xml");
        _storyParts.Clear();
        if (document.Comments.Count > 0) Relate("comments", "comments.xml");
        var body = WriteSectionBody(document);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip, "word/document.xml", new(E("document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), new XAttribute(XNamespace.Xmlns + "wp", Wp), new XAttribute(XNamespace.Xmlns + "a", A), new XAttribute(XNamespace.Xmlns + "pic", Pic), body)));
            Add(zip, "word/styles.xml", Styles());
            Add(zip, "word/numbering.xml", Numbering());
            Add(zip, "word/settings.xml", new(E("settings", E("zoom", new XAttribute(W + "percent", 100)), E("defaultTabStop", V(720)), DocumentSections.Definitions(document).Any(s => s.Options.DifferentOddAndEven) ? E("evenAndOddHeaders") : null, E("compat"))));
            foreach (var story in _storyParts) Add(zip, "word/" + story.Path, story.Xml);
            if (document.Comments.Count > 0) Add(zip, "word/comments.xml", Comments());
            Add(zip, "word/_rels/document.xml.rels", new(new XElement(Rel + "Relationships", _relationships)));
            foreach (var media in _media)
            {
                using var stream = zip.CreateEntry("word/" + media.Path, CompressionLevel.Optimal).Open();
                stream.Write(media.Bytes);
            }
            AddProperties(zip, document);
            Add(zip, "_rels/.rels", RootRelationships());
            Add(zip, "[Content_Types].xml", ContentTypes());
        }
        return output.ToArray();
    }

    private XDocument Comments()
    {
        var root = E("comments");
        for (var i = 0; i < _document.Comments.Count; i++)
        {
            var comment = _document.Comments[i];
            var text = comment.Text + (comment.Replies.Count > 0 ? "\n\n" + string.Join("\n", comment.Replies) : "");
            root.Add(E("comment", new XAttribute(W + "id", i), new XAttribute(W + "author", comment.Author), new XAttribute(W + "date", comment.Created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")), E("p", E("r", E("t", text)))));
        }
        return new(root);
    }

    private static void Add(ZipArchive zip, string path, XDocument document)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        document.Save(writer, SaveOptions.DisableFormatting);
    }

    private static void AddProperties(ZipArchive zip, DocumentModel document)
    {
        XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        XNamespace dc = "http://purl.org/dc/elements/1.1/", dct = "http://purl.org/dc/terms/", xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var properties = new XElement(cp + "coreProperties", new XAttribute(XNamespace.Xmlns + "dc", dc), new XAttribute(XNamespace.Xmlns + "dcterms", dct), new XAttribute(XNamespace.Xmlns + "xsi", xsi),
            new XElement(dc + "title", document.Title), new XElement(dc + "creator", document.Author), new XElement(dc + "subject", document.Subject),
            new XElement(dct + "created", new XAttribute(xsi + "type", "dcterms:W3CDTF"), document.Created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")),
            new XElement(dct + "modified", new XAttribute(xsi + "type", "dcterms:W3CDTF"), document.Modified.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));
        Add(zip, "docProps/core.xml", new(properties));
        XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        Add(zip, "docProps/app.xml", new(new XElement(ep + "Properties", new XElement(ep + "Application", "TextSpace"), new XElement(ep + "AppVersion", "0.2"))));
    }

    private static XDocument RootRelationships()
    {
        XElement Relationship(string id, string type, string target) => new(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", target));
        return new(new XElement(Rel + "Relationships",
            Relationship("rId1", RelationshipType("officeDocument"), "word/document.xml"),
            Relationship("rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml"),
            Relationship("rId3", RelationshipType("extended-properties"), "docProps/app.xml")));
    }

    private XDocument ContentTypes()
    {
        var types = new XElement(Ct + "Types",
            new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(Ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")));
        void Override(string path, string type) => types.Add(new XElement(Ct + "Override", new XAttribute("PartName", "/" + path), new XAttribute("ContentType", type)));
        Override("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
        foreach (var part in new[] { "styles", "numbering", "settings" }) Override("word/" + part + ".xml", "application/vnd.openxmlformats-officedocument.wordprocessingml." + part + "+xml");
        foreach (var story in _storyParts) Override("word/" + story.Path, "application/vnd.openxmlformats-officedocument.wordprocessingml." + story.Kind + "+xml");
        if (_document.Comments.Count > 0) Override("word/comments.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml");
        Override("docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
        Override("docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
        foreach (var media in _media) Override("word/" + media.Path, media.Type);
        return new(types);
    }

    private static XDocument Styles()
    {
        var root = E("styles", E("docDefaults", E("rPrDefault", RunProperties(new())), E("pPrDefault", ParagraphProperties(new(), false))));
        foreach (var style in DocumentStyles.BuiltIn)
        {
            root.Add(E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", style.Name.Replace(" ", "")), style.Name == "Normal" ? new XAttribute(W + "default", 1) : null,
                E("name", V(style.Name)), style.Name != "Normal" ? E("basedOn", V("Normal")) : null, E("next", V("Normal")), E("qFormat"),
                ParagraphProperties(style.Paragraph, false), RunProperties(style.Character)));
        }
        return new(root);
    }

    private static XDocument Numbering()
    {
        var root = E("numbering");
        for (var kind = 0; kind < 2; kind++)
        {
            var abstractNumber = E("abstractNum", new XAttribute(W + "abstractNumId", kind), E("multiLevelType", V("multilevel")));
            for (var level = 0; level < 9; level++)
            {
                abstractNumber.Add(E("lvl", new XAttribute(W + "ilvl", level), E("start", V(1)), E("numFmt", V(kind == 0 ? "bullet" : "decimal")),
                    E("lvlText", V(kind == 0 ? "•" : "%" + (level + 1) + ".")), E("lvlJc", V("left")),
                    E("pPr", E("ind", new XAttribute(W + "left", 360 * (level + 1)), new XAttribute(W + "hanging", 180)))));
            }
            root.Add(abstractNumber);
        }
        root.Add(E("num", new XAttribute(W + "numId", 1), E("abstractNumId", V(0))), E("num", new XAttribute(W + "numId", 2), E("abstractNumId", V(1))));
        return new(root);
    }
}
