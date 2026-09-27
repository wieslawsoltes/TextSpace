using System.IO.Compression;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class SectionInterchangeTests
{
    [Fact] public void StoryPlaceholdersAndLineBreaksRoundTrip()
    {
        var document = new DocumentModel { Header = "{TITLE}\n{AUTHOR}", Footer = "{PAGE}/{NUMPAGES} · {SECTION}/{SECTIONPAGES}" };
        var copy = new DocxReader().Read(new DocxWriter().Write(document)).Document;
        Assert.Equal(document.Header, copy.Header); Assert.Equal(document.Footer, copy.Footer);
    }
    [Fact] public void DocxTablesUseTheCurrentSectionWidth()
    {
        var document = new DocumentModel { Blocks = [new Paragraph("one"), new SectionBreakBlock { Section = new() { Page = new PageSettings().Landscape() } }, TableBlock.Create(1, 2)] };
        using var stream = new MemoryStream(new DocxWriter().Write(document)); using var zip = new ZipArchive(stream); using var entry = zip.GetEntry("word/document.xml")!.Open();
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var xml = XDocument.Load(entry); Assert.Equal("12960", (string?)xml.Descendants(w + "tblW").Single().Attribute(w + "w"));
    }
    [Fact] public void HtmlHasIndependentSectionPrintSizesAndColumnBreaks()
    {
        var document = new DocumentModel { Blocks = [new Paragraph("one"), new SectionBreakBlock { Kind = SectionBreakKind.OddPage, Section = new() { Page = new PageSettings().Landscape() with { Columns = 2 } } }, new Paragraph("two"), new ColumnBreakBlock(), new Paragraph("three")] };
        var html = HtmlExporter.Export(document);
        Assert.Contains("@page section0{size:612pt 792pt", html); Assert.Contains("@page section1{size:792pt 612pt", html);
        Assert.Contains("column-count:2", html); Assert.Contains("break-before:right", html); Assert.Contains("break-after:column", html);
    }
    [Fact] public void MailMergeMovesBookmarksAndUnaffectedFields()
    {
        var session = new EditorSession(DocumentJson.FromText("«Name»\nTarget\n")); session.SetSelection(7, 13); session.SetBookmark("Target");
        session.SetSelection(session.Index.Length, session.Index.Length); session.InsertField("REF Target");
        var copy = MailMerge.Merge(session.Document, new Dictionary<string, string> { ["Name"] = "Longer replacement" });
        Assert.Equal("Longer replacement\nTarget\nTarget", copy.PlainText);
        var bookmark = Assert.Single(copy.Bookmarks); Assert.Equal(19, bookmark.Start); Assert.Equal(25, bookmark.End);
        var field = Assert.Single(copy.Fields); Assert.Equal(26, field.Start); Assert.Equal(32, field.End);
        DocumentJson.Validate(copy);
    }
    [Fact] public void MailMergeUnlinksOnlyFieldsWhoseResultsItEdits()
    {
        var document = DocumentJson.FromText("«Name»\n1"); document.Fields.Add(new() { Start = 0, End = 6, Instruction = "TITLE" }); document.Fields.Add(new() { Start = 7, End = 8, Instruction = "PAGE" });
        var copy = MailMerge.Merge(document, new Dictionary<string, string> { ["Name"] = "Other" });
        var field = Assert.Single(copy.Fields); Assert.Equal("PAGE", field.Instruction); Assert.Equal(6, field.Start);
    }
    [Fact] public void FormattingViewHasSectionAndColumnBreakMarkers()
    {
        var document = new DocumentModel { Blocks = [new Paragraph("one"), new ColumnBreakBlock(), new SectionBreakBlock(), new Paragraph("two")] };
        var layout = new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document);
        Assert.Contains(layout.Pages.SelectMany(p => p.Breaks), b => b.Label == "Column Break");
        Assert.Contains(layout.Pages.SelectMany(p => p.Breaks), b => b.Label == "Section Break (NextPage)");
    }
}
