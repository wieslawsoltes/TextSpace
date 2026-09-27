using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class TypographyInterchangeTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static XDocument Part(byte[] bytes, string path)
    {
        using var stream = new MemoryStream(bytes); using var zip = new ZipArchive(stream); using var part = zip.GetEntry(path)!.Open(); return XDocument.Load(part);
    }
    private static byte[] Modify(byte[] bytes, string path, Action<XDocument> change)
    {
        using var input = new MemoryStream(bytes); using var source = new ZipArchive(input); using var output = new MemoryStream();
        using (var result = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open(); using var to = result.CreateEntry(entry.FullName).Open();
                if (entry.FullName != path) from.CopyTo(to);
                else { var document = XDocument.Load(from); change(document); document.Save(to); }
            }
        return output.ToArray();
    }
    private static void Valid(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes); using var package = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(package).Select(e => e.Description + " " + e.Path?.XPath).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors));
    }
    private static XElement Tab(int twips, string value, string? leader = null) => new(W + "tab", new XAttribute(W + "pos", twips), new XAttribute(W + "val", value), leader is null ? null : new XAttribute(W + "leader", leader));
    private static void SetTabs(XElement properties, params XElement[] tabs) { properties.Element(W + "tabs")?.Remove(); properties.Add(new XElement(W + "tabs", tabs)); }

    [Theory]
    [InlineData(TabAlignment.Left, TabLeader.None)]
    [InlineData(TabAlignment.Center, TabLeader.Dot)]
    [InlineData(TabAlignment.Right, TabLeader.Hyphen)]
    [InlineData(TabAlignment.Decimal, TabLeader.Underscore)]
    [InlineData(TabAlignment.Bar, TabLeader.Heavy)]
    [InlineData(TabAlignment.Right, TabLeader.MiddleDot)]
    public void EverySupportedTabAlignmentAndLeaderIsValidWordprocessingML(TabAlignment alignment, TabLeader leader)
    {
        var d = new DocumentModel { DefaultTabStop = 48, Blocks = [new Paragraph("Label\t123.45", format: new() { TabStops = [new() { Position = 144, Alignment = alignment, Leader = leader }], KeepLinesTogether = true, KeepWithNext = true, WidowControl = false })] };
        var bytes = new DocxWriter().Write(d); Valid(bytes); var imported = new DocxReader().Read(bytes).Document;
        Assert.Equal(48, imported.DefaultTabStop); var p = imported.Paragraphs().First(); Assert.True(p.Format.KeepLinesTogether); Assert.True(p.Format.KeepWithNext); Assert.False(p.Format.WidowControl);
        var tab = Assert.Single(p.Format.TabStops); Assert.Equal(144, tab.Position); Assert.Equal(alignment, tab.Alignment); Assert.Equal(leader, tab.Leader);
    }
    [Fact] public void DiscretionaryAndNonbreakingHyphensUseStandardRunElements()
    {
        var d = DocumentJson.FromText("Non\u2011breaking and option\u00adal"); var bytes = new DocxWriter().Write(d); Valid(bytes);
        var xml = Part(bytes, "word/document.xml"); Assert.Single(xml.Descendants(W + "noBreakHyphen")); Assert.Single(xml.Descendants(W + "softHyphen"));
        Assert.Equal(d.PlainText, new DocxReader().Read(bytes).Document.PlainText);
    }
    [Fact] public void DocumentDefaultsAndStyleTabClearsAreMerged()
    {
        var bytes = new DocxWriter().Write(DocumentJson.FromText("Test"));
        bytes = Modify(bytes, "word/styles.xml", xml =>
        {
            var defaults = xml.Root!.Element(W + "docDefaults")!.Element(W + "pPrDefault")!.Element(W + "pPr")!;
            SetTabs(defaults, Tab(720, "left"), Tab(1440, "right", "dot"));
            var normal = xml.Root.Elements(W + "style").Single(s => (string?)s.Attribute(W + "styleId") == "Normal").Element(W + "pPr")!;
            SetTabs(normal, Tab(720, "clear"), Tab(2160, "center"));
        });
        bytes = Modify(bytes, "word/document.xml", xml => SetTabs(xml.Descendants(W + "pPr").First(), Tab(1440, "clear"), Tab(2880, "decimal")));
        var p = new DocxReader().Read(bytes).Document.Paragraphs().First();
        Assert.Equal(new[] { 108d, 144d }, p.Format.TabStops.Select(t => t.Position)); Assert.Equal(TabAlignment.Center, p.Format.TabStops[0].Alignment); Assert.Equal(TabAlignment.Decimal, p.Format.TabStops[1].Alignment);
    }
    [Fact] public void ExplicitFalseOverridesInheritedPaginationFlags()
    {
        var d = DocumentJson.FromText("Heading"); d.Paragraphs().First().Format = new() { StyleName = "Heading 1", KeepWithNext = false, WidowControl = false };
        var bytes = new DocxWriter().Write(d); Valid(bytes);
        var p = new DocxReader().Read(bytes).Document.Paragraphs().First(); Assert.False(p.Format.KeepWithNext); Assert.False(p.Format.WidowControl);
    }
    [Fact] public void DefaultParagraphFlagsApplyWhenPropertiesAreOmitted()
    {
        var bytes = new DocxWriter().Write(DocumentJson.FromText("Test"));
        bytes = Modify(bytes, "word/styles.xml", xml =>
        {
            var properties = xml.Root!.Element(W + "docDefaults")!.Element(W + "pPrDefault")!.Element(W + "pPr")!;
            properties.Element(W + "keepLines")?.Remove(); properties.Add(new XElement(W + "keepLines")); properties.Element(W + "widowControl")?.Remove(); properties.Add(new XElement(W + "widowControl", new XAttribute(W + "val", 0)));
        });
        bytes = Modify(bytes, "word/document.xml", xml => xml.Descendants(W + "pPr").First().Remove());
        // Remove Normal's explicit flags so it inherits document defaults as Word would.
        bytes = Modify(bytes, "word/styles.xml", xml =>
        {
            var p = xml.Root!.Elements(W + "style").Single(s => (string?)s.Attribute(W + "styleId") == "Normal").Element(W + "pPr")!;
            p.Element(W + "keepLines")?.Remove(); p.Element(W + "widowControl")?.Remove();
        });
        var p = new DocxReader().Read(bytes).Document.Paragraphs().First(); Assert.True(p.Format.KeepLinesTogether); Assert.False(p.Format.WidowControl);
    }
    [Fact] public void RightRelativeStopsExportAgainstActualSectionAndTableCellWidth()
    {
        Paragraph Paragraph() => new("A\tB", format: new() { TabStops = [new() { RelativeToRightEdge = true, Alignment = TabAlignment.Right, Leader = TabLeader.Dot }] });
        var table = TableBlock.Create(1, 2); table.Rows[0].Cells[0].Blocks = [Paragraph()];
        var d = new DocumentModel { Blocks = [Paragraph(), new SectionBreakBlock { Section = new() { Page = new PageSettings().Landscape() } }, Paragraph(), table, new Paragraph()] };
        var bytes = new DocxWriter().Write(d); Valid(bytes);
        var stops = Part(bytes, "word/document.xml").Descendants(W + "tabs").SelectMany(t => t.Elements(W + "tab")).Select(t => (int)t.Attribute(W + "pos")!).ToArray();
        Assert.Equal(new[] { 468 * 20, 648 * 20, (324 - 12) * 20 }, stops);
    }
    [Fact] public void GeneratedContentsUseRightAlignedDotLeadersAndRemainLiveAfterRoundTrip()
    {
        var e = new EditorSession(DocumentJson.FromText("First\nBody\nSecond")); e.SetSelection(0, 0); e.ApplyStyle("Heading 1"); e.SetSelection(e.Index.Length, e.Index.Length); e.ApplyStyle("Heading 2"); e.SetSelection(0, 0);
        e.InsertTableOfContents();
        var contents = e.Document.Paragraphs().Where(p => p.Format.StyleName is "TOC1" or "TOC2").ToArray(); Assert.Equal(2, contents.Length);
        Assert.All(contents, p => { var tab = Assert.Single(p.Format.TabStops); Assert.Equal(TabAlignment.Right, tab.Alignment); Assert.Equal(TabLeader.Dot, tab.Leader); Assert.True(tab.RelativeToRightEdge); });
        var engine = new PageLayoutEngine(new MonospaceTextMetrics()); var layout = engine.Layout(e.Document);
        Assert.All(contents, p => { var line = layout.Lines.First(l => l.ParagraphId == p.Id); Assert.Equal(540, line.Chunks[^1].X + line.Chunks[^1].Width, 6); });
        var bytes = new DocxWriter().Write(e.Document); Valid(bytes); var imported = new DocxReader().Read(bytes).Document;
        Assert.Equal(4, imported.Fields.Count); Assert.Equal(2, imported.Paragraphs().Count(p => p.Format.TabStops.Any(t => t.Leader == TabLeader.Dot)));
    }
    [Theory]
    [InlineData("NaN")][InlineData("90000")][InlineData("invalid")]
    public void MalformedTabPositionsAreRejected(string position)
    {
        var bytes = new DocxWriter().Write(DocumentJson.FromText("Test")); bytes = Modify(bytes, "word/document.xml", xml =>
            SetTabs(xml.Descendants(W + "pPr").First(), new XElement(W + "tab", new XAttribute(W + "pos", position), new XAttribute(W + "val", "left"))));
        Assert.Throws<InvalidDataException>(() => new DocxReader().Read(bytes));
    }
    [Fact] public void UnknownTabAlignmentsProduceImportWarning()
    {
        var bytes = new DocxWriter().Write(DocumentJson.FromText("Test")); bytes = Modify(bytes, "word/document.xml", xml => SetTabs(xml.Descendants(W + "pPr").First(), Tab(720, "list")));
        var imported = new DocxReader().Read(bytes); Assert.Contains(imported.Warnings, w => w.Contains("tab alignment")); Assert.Empty(imported.Document.Paragraphs().First().Format.TabStops);
    }
}
