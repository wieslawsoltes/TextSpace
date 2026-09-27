using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.OpenXml;
using TextSpace.Skia;
using Xunit;

namespace TextSpace.Tests;

public sealed class SectionTests
{
    private static DocumentLayout Layout(DocumentModel document) => new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document);
    private static DocumentModel Mixed(SectionBreakKind kind = SectionBreakKind.NextPage) => new()
    {
        Header = "Chapter header", Footer = "{PAGE} of {NUMPAGES}",
        Blocks = [new Paragraph("Portrait"), new SectionBreakBlock { Kind = kind, Section = new() { Page = new PageSettings().Landscape(), Options = new() { PageNumberStart = 10 } } }, new Paragraph("Landscape")]
    };

    [Fact] public void MixedPagesHaveIndependentGeometryAndCumulativeOffsets()
    {
        var layout = Layout(Mixed()); Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(612, layout.Pages[0].Settings.Width); Assert.Equal(792, layout.Pages[1].Settings.Width);
        Assert.Equal(816, layout.PageTop(1)); Assert.Equal(1428, layout.Height); Assert.Equal(90, layout.PageLeft(0)); Assert.Equal(0, layout.PageLeft(1));
        Assert.Equal(0, layout.PageAtY(815)); Assert.Equal(1, layout.PageAtY(816));
    }
    [Fact] public void MixedPagesHitTestAndCaretUseTheirOwnOrigin()
    {
        var document = Mixed(); var layout = Layout(document); var index = new TextIndex(document);
        foreach (var address in index.Paragraphs)
        {
            var caret = layout.Caret(address.Start + 3);
            Assert.Equal(address.Start + 3, layout.HitTest(layout.PageLeft(caret.PageIndex) + caret.X, layout.PageTop(caret.PageIndex) + caret.Y + caret.Height / 2));
        }
    }
    [Theory] [InlineData(SectionBreakKind.OddPage, 3)] [InlineData(SectionBreakKind.EvenPage, 2)]
    public void ParityBreaksInsertOnlyRequiredBlankPages(SectionBreakKind kind, int pages)
    {
        var layout = Layout(Mixed(kind)); Assert.Equal(pages, layout.Pages.Count);
        Assert.Equal(10, layout.Pages[^1].PageNumber); Assert.Equal(0, layout.Pages[^1].SectionPageIndex);
        if (pages == 3) { Assert.True(layout.Pages[1].IsParityBlank); Assert.Empty(layout.Pages[1].Header); Assert.Empty(layout.Pages[1].Lines); }
    }
    [Fact] public void SectionNumberingCanContinueOrRestartIndependently()
    {
        var document = Mixed(); var boundary = document.Blocks.OfType<SectionBreakBlock>().Single();
        boundary.Section = boundary.Section with { Options = new() { NumberStyle = PageNumberStyle.LowerRoman } };
        var layout = Layout(document); Assert.Equal(2, layout.Pages[1].PageNumber); Assert.Equal("ii", DocumentSections.FormatNumber(2, layout.Pages[1].Section.Options.NumberStyle));
    }
    [Fact] public void FirstEvenAndDefaultStoriesHaveCorrectPrecedence()
    {
        var document = new DocumentModel
        {
            Header = "odd", Footer = "body", SectionOptions = new() { DifferentFirstPage = true, DifferentOddAndEven = true, FirstHeader = "first", EvenHeader = "even", FirstFooter = "" },
            Blocks = [new Paragraph("one"), new PageBreakBlock(), new Paragraph("two"), new PageBreakBlock(), new Paragraph("three")]
        };
        var layout = Layout(document); Assert.Equal(new[] { "first", "even", "odd" }, layout.Pages.Select(p => p.Header));
        Assert.Equal("", layout.Pages[0].Footer); Assert.Equal(3, layout.Pages[2].SectionPageCount);
    }
    [Fact] public void StoriesCanBeLinkedOrExplicitlyBlank()
    {
        var document = Mixed(); var boundary = document.Blocks.OfType<SectionBreakBlock>().Single();
        Assert.Equal("Chapter header", Layout(document).Pages[1].Header);
        boundary.Section = boundary.Section with { Header = "" }; Assert.Equal("", Layout(document).Pages[1].Header);
    }
    [Fact] public void ColumnBreakAdvancesWithinThePage()
    {
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [new Paragraph("left"), new ColumnBreakBlock(), new Paragraph("right")] };
        var layout = Layout(document); Assert.Single(layout.Pages); Assert.True(layout.Lines.Last().X > 300);
    }
    [Fact] public void SectionInsertionPreservesRichTextAndIsOneUndoUnit()
    {
        var session = new EditorSession(DocumentJson.FromText("abcdef")); session.SetSelection(3, 3); session.InsertSectionBreak();
        Assert.Equal("abc\ndef", session.Document.PlainText); Assert.Equal(1, session.CurrentSectionIndex); Assert.Equal(1, session.UndoCount);
        session.SetPage(p => p.Landscape()); Assert.Equal(612, session.Document.Page.Width); Assert.Equal(792, session.CurrentSection.Page.Width);
        session.Undo(); session.Undo(); Assert.Equal("abcdef", session.Document.PlainText); Assert.Empty(session.Document.Blocks.OfType<SectionBreakBlock>());
    }
    [Fact] public void RemovingSectionBreakKeepsTextAndAdoptsPreviousSection()
    {
        var session = new EditorSession(Mixed()); session.SetSelection(session.Index.Length, session.Index.Length); session.RemoveCurrentSectionBreak();
        Assert.Equal("Portrait\nLandscape", session.Document.PlainText); Assert.Equal(0, session.CurrentSectionIndex); Assert.Single(Layout(session.Document).Pages);
    }
    [Fact] public void SectionBreaksCannotBeInsertedInsideTableCells()
    {
        var session = new EditorSession(new() { Blocks = [TableBlock.Create(1, 1)] });
        Assert.Throws<InvalidOperationException>(() => session.InsertSectionBreak()); Assert.Throws<InvalidOperationException>(session.InsertColumnBreak);
    }
    [Fact] public void MalformedSectionGeometryRollsBack()
    {
        var session = new EditorSession(Mixed()); session.SetSelection(session.Index.Length, session.Index.Length); var json = DocumentJson.Save(session.Document);
        Assert.Throws<InvalidDataException>(() => session.SetSection(s => s with { Page = s.Page with { Width = double.NaN } })); Assert.Equal(json, DocumentJson.Save(session.Document));
    }
    [Theory] [InlineData(4, PageNumberStyle.UpperRoman, "IV")] [InlineData(49, PageNumberStyle.LowerRoman, "xlix")]
    [InlineData(27, PageNumberStyle.UpperLetter, "AA")] [InlineData(52, PageNumberStyle.LowerLetter, "az")]
    [InlineData(4000, PageNumberStyle.UpperRoman, "4000")]
    public void BoundedPageNumberFormats(int value, PageNumberStyle style, string expected) => Assert.Equal(expected, DocumentSections.FormatNumber(value, style));
    [Fact] public void SectionSettingsAndStoriesRoundTripThroughNativeJson()
    {
        var document = Mixed(SectionBreakKind.OddPage); var copy = DocumentJson.Clone(document);
        Assert.Equal(DocumentJson.Save(document), DocumentJson.Save(copy));
    }
    [Fact] public void SectionsExportAsSchemaValidWordprocessingMl()
    {
        var document = Mixed(SectionBreakKind.OddPage); document.SectionOptions = new() { DifferentFirstPage = true, FirstHeader = "Cover", DifferentOddAndEven = true, EvenFooter = "Even {PAGE}" };
        var bytes = new DocxWriter().Write(document); using var stream = new MemoryStream(bytes); using var package = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(package).Select(e => e.Path?.XPath + ": " + e.Description).ToArray(); Assert.True(errors.Length == 0, string.Join("\n", errors));
        var copy = new DocxReader().Read(bytes).Document; Assert.Equal(document.PlainText, copy.PlainText);
        var boundary = Assert.Single(copy.Blocks.OfType<SectionBreakBlock>()); Assert.Equal(SectionBreakKind.OddPage, boundary.Kind); Assert.Equal(792, boundary.Section.Page.Width); Assert.Equal(10, boundary.Section.Options.PageNumberStart);
        Assert.Null(boundary.Section.Header); Assert.Equal("Cover", copy.SectionOptions.FirstHeader); Assert.True(copy.SectionOptions.DifferentOddAndEven);
    }
    [Fact] public void ColumnBreakRoundTripsWithoutAddingText()
    {
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [new Paragraph("left"), new ColumnBreakBlock(), new Paragraph("right")] };
        var copy = new DocxReader().Read(new DocxWriter().Write(document)).Document;
        Assert.Equal(document.PlainText, copy.PlainText); Assert.Single(copy.Blocks.OfType<ColumnBreakBlock>());
    }
    [Fact] public void MixedPaperPngUsesSelectedPageSize()
    {
        using var renderer = new DocumentRenderer(); using var bitmap = SkiaSharp.SKBitmap.Decode(renderer.ExportPng(Mixed(), 1, 1));
        Assert.Equal(792, bitmap.Width); Assert.Equal(612, bitmap.Height);
    }
    [Fact] public void PdfContainsBothPageSizes()
    {
        using var renderer = new DocumentRenderer(); var pdf = System.Text.Encoding.Latin1.GetString(renderer.ExportPdf(Mixed()));
        Assert.Contains("/MediaBox [0 0 612 792]", pdf); Assert.Contains("/MediaBox [0 0 792 612]", pdf);
    }
}
