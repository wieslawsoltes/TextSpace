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

public sealed class ContinuousSectionTests
{
    private static Paragraph P(string text, ParagraphFormat? format = null) => new(text, format: format ?? new() { SpaceAfter = 0, LineSpacing = 1 });
    private static SectionBreakBlock Break(SectionBreakKind kind = SectionBreakKind.Continuous, int columns = 1) =>
        new() { Kind = kind, Section = new() { Page = new() { Columns = columns } } };
    private static DocumentLayout Layout(DocumentModel document) => new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document);
    private static LayoutLine Line(DocumentLayout layout, Paragraph p) => layout.Lines.First(l => l.ParagraphId == p.Id);

    [Fact]
    public void ContinuousStartsOnSamePageBelowPrecedingParagraph()
    {
        var first = P("First"); var second = P("Second");
        var document = new DocumentModel { Blocks = [first, Break(), second] };
        var layout = Layout(document);
        Assert.Single(layout.Pages); Assert.Equal(2, layout.Pages[0].Regions.Count);
        Assert.True(Line(layout, second).Y >= Line(layout, first).Y + Line(layout, first).Height);
        Assert.Equal(1, Line(layout, second).Region!.SectionIndex);
        Assert.Equal(document.PlainText.Length - second.Length, Line(layout, second).Start);
    }

    [Fact]
    public void SamePageFieldsUseTheContainingSectionNotThePageOwner()
    {
        var first = P("One"); var second = P("Two"); var third = P("Three");
        var b = Break(); b.Section = b.Section with { Options = new() { PageNumberStart = 10, NumberStyle = PageNumberStyle.LowerRoman } };
        var document = new DocumentModel { Blocks = [first, b, second, Break(), third] };
        var layout = Layout(document); var index = new TextIndex(document);
        Assert.Equal(1, layout.FieldPageAt(index.StartOf(first)).SectionNumber);
        var field = layout.FieldPageAt(index.StartOf(second));
        Assert.Equal(2, field.SectionNumber); Assert.Equal(10, field.PageNumber); Assert.Equal(PageNumberStyle.LowerRoman, field.NumberStyle);
        Assert.Equal(1, field.PageCount); Assert.Equal(1, field.SectionPages);
        Assert.Equal(3, layout.FieldPageAt(index.StartOf(third)).SectionNumber);
        Assert.Equal(10, layout.FieldPageAt(index.StartOf(third)).PageNumber);
    }

    [Fact]
    public void NextColumnUsesNextColumnOnSamePageAndPreservesTextOrder()
    {
        var first = P("First column"); var second = P("Second column");
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [first, Break(SectionBreakKind.NextColumn, 2), second] };
        var layout = Layout(document);
        Assert.Single(layout.Pages); Assert.Equal(Line(layout, first).Y, Line(layout, second).Y);
        Assert.Equal(1, Line(layout, second).ColumnIndex); Assert.True(Line(layout, second).X > 300);
        Assert.Equal("First column\nSecond column", document.PlainText);
        Assert.Equal(2, layout.FieldPageAt(Line(layout, second).Start).SectionNumber);
    }

    [Fact]
    public void NextColumnInLastColumnStartsNextPage()
    {
        var a = P("a"); var b = P("b"); var c = P("c");
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [a, new ColumnBreakBlock(), b, Break(SectionBreakKind.NextColumn, 2), c] };
        var layout = Layout(document);
        Assert.Equal(2, layout.Pages.Count); Assert.Equal(1, Line(layout, c).PageIndex); Assert.Equal(0, Line(layout, c).ColumnIndex);
    }

    [Fact]
    public void ContinuousAfterNextColumnStartsBelowAllEarlierColumns()
    {
        var a = P(string.Join('\u2028', Enumerable.Repeat("Tall", 10))); var b = P("Short"); var c = P("Below");
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [a, Break(SectionBreakKind.NextColumn, 2), b, Break(), c] };
        var layout = Layout(document);
        var bottom = layout.Lines.Where(l => l.ParagraphId == a.Id).Max(l => l.Y + l.Height);
        Assert.Single(layout.Pages); Assert.True(Line(layout, c).Y >= bottom - 0.001);
    }

    [Theory]
    [InlineData(SectionBreakKind.Continuous)]
    [InlineData(SectionBreakKind.NextColumn)]
    public void PaperSizeChangeUsesNewPageWithoutChangingStoredBreakKind(SectionBreakKind kind)
    {
        var boundary = Break(kind); boundary.Section = boundary.Section with { Page = new PageSettings().Landscape() };
        var document = new DocumentModel { Blocks = [P("portrait"), boundary, P("landscape")] };
        var layout = Layout(document);
        Assert.Equal(2, layout.Pages.Count); Assert.Equal(792, layout.Pages[1].Settings.Width);
        Assert.Equal(kind, boundary.Kind); Assert.Contains(layout.Notices, n => n.Code == "section-paper-change");
    }

    [Fact]
    public void NextColumnGridChangeIsExplicitlyDiagnosed()
    {
        var document = new DocumentModel { Blocks = [P("One"), Break(SectionBreakKind.NextColumn, 2), P("Two")] };
        var layout = Layout(document);
        Assert.Equal(2, layout.Pages.Count); Assert.Contains(layout.Notices, n => n.Code == "section-column-grid-change");
    }

    [Fact]
    public void ContinuousMarginChangesUseRegionMarginsAndRuler()
    {
        var after = P("Second"); var boundary = Break();
        boundary.Section = boundary.Section with { Page = new() { MarginLeft = 100, MarginRight = 100, MarginTop = 50, MarginBottom = 60 } };
        var document = new DocumentModel { Blocks = [P("First"), boundary, after] };
        var layout = Layout(document); var line = Line(layout, after);
        Assert.Single(layout.Pages); Assert.Equal(100, line.X); Assert.Equal(412, layout.RulerAt(line.Start).ContentWidth);
        Assert.Equal(100, layout.RulerAt(line.Start).MarginLeft);
    }

    [Fact]
    public void RulerTracksSecondColumnInsteadOfThePageWideMargins()
    {
        var second = P("Second");
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [P("First"), new ColumnBreakBlock(), second] };
        var layout = Layout(document); var ruler = layout.RulerAt(Line(layout, second).Start);
        Assert.Equal(document.Page.ColumnWidth, ruler.ContentWidth); Assert.Equal(318, ruler.MarginLeft); Assert.Equal(1, ruler.Columns);
    }

    [Fact]
    public void ParagraphOnlyBandsBalanceBeforeContinuousBoundary()
    {
        var paragraphs = Enumerable.Range(0, 10).Select(i => P("Line " + i)).ToArray(); var tail = P("Full width again");
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [.. paragraphs, Break(), tail] };
        var layout = Layout(document);
        Assert.Single(layout.Pages); Assert.True(layout.Pages[0].Regions[0].Balanced);
        Assert.Equal(5, layout.Lines.Count(l => l.Region!.SectionIndex == 0 && l.ColumnIndex == 0));
        Assert.Equal(5, layout.Lines.Count(l => l.Region!.SectionIndex == 0 && l.ColumnIndex == 1));
        Assert.True(Line(layout, tail).Y < 150);
    }

    [Fact]
    public void SingleParagraphCanBalanceAtLineBoundaries()
    {
        var p = P(string.Join('\u2028', Enumerable.Repeat("Line", 10))); var tail = P("Tail");
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [p, Break(), tail] };
        var layout = Layout(document);
        Assert.Single(layout.Pages); Assert.Equal(5, layout.Lines.Count(l => l.ParagraphId == p.Id && l.ColumnIndex == 0));
        Assert.Equal(5, layout.Lines.Count(l => l.ParagraphId == p.Id && l.ColumnIndex == 1));
    }

    [Fact]
    public void BalanceRetainsKeepWithNextAndKeepLinesTogether()
    {
        var title = P("Heading", new() { KeepWithNext = true, SpaceAfter = 4 });
        var content = P("One\u2028Two\u2028Three", new() { KeepLinesTogether = true, SpaceAfter = 0 });
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [P("Before"), title, content, P("After"), Break(), P("Tail")] };
        var layout = Layout(document);
        Assert.Single(layout.Pages);
        Assert.Equal(Line(layout, title).ColumnIndex, Line(layout, content).ColumnIndex);
        Assert.Single(layout.Lines.Where(l => l.ParagraphId == content.Id).Select(l => l.ColumnIndex).Distinct());
    }

    [Fact]
    public void ExplicitColumnBreakIsNotRebalanced()
    {
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [P("Left"), new ColumnBreakBlock(), P("Right"), Break(), P("Tail")] };
        var layout = Layout(document);
        Assert.False(layout.Pages[0].Regions[0].Balanced); Assert.Single(layout.Pages);
    }

    [Fact]
    public void NestedTableAfterContinuousBoundaryHasCorrectRegionAndCaret()
    {
        var table = TableBlock.Create(2, 2); var nested = TableBlock.Create(1, 2);
        table.Rows[0].Cells[0].Blocks.Add(nested);
        var document = new DocumentModel { Blocks = [P("Intro"), Break(columns: 2), table, P("End")] };
        var layout = Layout(document); var index = new TextIndex(document);
        foreach (var paragraph in DocumentModel.Walk([table]))
        {
            var line = Line(layout, paragraph); Assert.Equal(1, line.Region!.SectionIndex);
            Assert.Equal(2, layout.FieldPageAt(index.StartOf(paragraph)).SectionNumber);
            var caret = layout.Caret(index.StartOf(paragraph));
            Assert.Equal(index.StartOf(paragraph), layout.HitTest(caret.X + layout.PageLeft(caret.PageIndex), layout.PageTop(caret.PageIndex) + caret.Y + caret.Height / 2));
        }
    }

    [Fact]
    public void ImageBeforeContinuousBoundaryReservesItsHeight()
    {
        var tail = P("After image");
        var document = new DocumentModel { Blocks = [new ImageBlock { Width = 100, Height = 200 }, Break(), tail] };
        var layout = Layout(document);
        Assert.Single(layout.Pages); Assert.True(Line(layout, tail).Y >= 280);
    }

    [Fact]
    public void SmallRemainingBandMovesOversizedFirstLineToNextPage()
    {
        var before = P("Before", new() { SpaceAfter = 600 }); var after = new Paragraph("Large", new() { FontSize = 40 });
        var document = new DocumentModel { Blocks = [before, Break(), after] };
        var layout = Layout(document);
        Assert.Equal(2, layout.Pages.Count); Assert.Equal(1, Line(layout, after).PageIndex);
        Assert.Single(layout.Pages[0].Regions); Assert.Equal(0, layout.Pages[1].Regions[0].SectionPageIndex);
        Assert.Equal(1, layout.FieldPageAt(Line(layout, after).Start).SectionPages);
    }

    [Fact]
    public void RestartOnEmptySharedRegionIsNotConsumed()
    {
        var after = new Paragraph("Large", new() { FontSize = 40 }); var boundary = Break();
        boundary.Section = boundary.Section with { Options = new() { PageNumberStart = 7 } };
        var document = new DocumentModel { Blocks = [P("Before", new() { SpaceAfter = 600 }), boundary, after] };
        var layout = Layout(document);
        Assert.Equal(7, layout.FieldPageAt(Line(layout, after).Start).PageNumber);
    }

    [Fact]
    public void PageBreakBeforeAfterContinuousBreakForcesPhysicalPage()
    {
        var after = P("After", new() { PageBreakBefore = true });
        var document = new DocumentModel { Blocks = [P("Before"), Break(), after] };
        var layout = Layout(document);
        Assert.Equal(2, layout.Pages.Count); Assert.Equal(1, Line(layout, after).PageIndex);
    }

    [Fact]
    public void PhysicalPageHeaderBelongsToFirstRegionAndLaterPageUsesNewSection()
    {
        var boundary = Break(); boundary.Section = boundary.Section with { Header = "Second section", Footer = "B" };
        var document = new DocumentModel { Header = "First section", Blocks = [P("Before"), boundary, P("After"), new PageBreakBlock(), P("Next physical page")] };
        var layout = Layout(document);
        Assert.Equal("First section", layout.Pages[0].Header); Assert.Equal("Second section", layout.Pages[1].Header);
        Assert.Equal(2, layout.Pages[0].Regions[1].SectionPageCount); Assert.Equal(1, layout.Pages[1].SectionPageIndex);
    }

    [Theory]
    [InlineData(SectionBreakKind.OddPage, 3)]
    [InlineData(SectionBreakKind.EvenPage, 2)]
    public void ParityAfterSharedSectionsUsesPhysicalPages(SectionBreakKind kind, int pages)
    {
        var document = new DocumentModel { Blocks = [P("One"), Break(), P("Two"), Break(kind), P("Three")] };
        var layout = Layout(document); Assert.Equal(pages, layout.Pages.Count);
        Assert.Equal(kind == SectionBreakKind.OddPage, layout.Pages.Count > 2 && layout.Pages[1].IsParityBlank);
    }

    [Theory]
    [InlineData(SectionBreakKind.Continuous)]
    [InlineData(SectionBreakKind.NextColumn)]
    public void NativeAndDocxRetainBreakTypeAndValidateSchema(SectionBreakKind kind)
    {
        var document = new DocumentModel { Page = new() { Columns = 2 }, Blocks = [P("Before"), Break(kind, 2), P("After")] };
        var native = DocumentJson.Clone(document); Assert.Equal(kind, native.Blocks.OfType<SectionBreakBlock>().Single().Kind);
        var bytes = new DocxWriter().Write(document);
        using var stream = new MemoryStream(bytes); using var package = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var imported = new DocxReader().Read(bytes);
        Assert.Equal(kind, imported.Document.Blocks.OfType<SectionBreakBlock>().Single().Kind);
        Assert.Equal(document.PlainText, imported.Document.PlainText);
        Assert.DoesNotContain(imported.Warnings, w => w.Contains("normalized to next-page"));
        Assert.Single(Layout(imported.Document).Pages);
    }

    [Theory]
    [InlineData(SectionBreakKind.Continuous)]
    [InlineData(SectionBreakKind.NextColumn)]
    public void InsertionAndUndoRedoRemainAtomic(SectionBreakKind kind)
    {
        var editor = new EditorSession(DocumentJson.FromText("BeforeAfter")); editor.SetSelection(6, 6);
        editor.InsertSectionBreak(kind);
        Assert.Equal(1, editor.CurrentSectionIndex); Assert.Equal(1, editor.UndoCount);
        Assert.Equal("Before\nAfter", editor.Document.PlainText);
        editor.Undo(); Assert.Empty(editor.Document.Blocks.OfType<SectionBreakBlock>());
        Assert.Equal("BeforeAfter", editor.Document.PlainText);
        editor.Redo(); Assert.Equal(kind, editor.Document.Blocks.OfType<SectionBreakBlock>().Single().Kind);
    }

    [Fact]
    public void FieldUpdateUsesLiveSamePageSectionContext()
    {
        var editor = new EditorSession(DocumentJson.FromText("First")); editor.SetSelection(5, 5);
        editor.InsertSectionBreak(SectionBreakKind.Continuous);
        editor.InsertField("SECTION", doc => new() { PageAt = Layout(doc).FieldPageAt });
        Assert.EndsWith("2", editor.Document.PlainText); Assert.Single(Layout(editor.Document).Pages);
        editor.SetSection(s => s with { Options = s.Options with { PageNumberStart = 12 } });
        editor.InsertText(" "); editor.InsertField("PAGE", doc => new() { PageAt = Layout(doc).FieldPageAt });
        Assert.EndsWith("2 12", editor.Document.PlainText);
    }

    [Fact]
    public void RenderingAndExportsSupportSharedRegions()
    {
        var document = new DocumentModel { Blocks = [P("Intro"), Break(columns: 2), P("Columns"), Break(), P("Tail")] };
        using var renderer = new DocumentRenderer(); var layout = renderer.Layout(document);
        Assert.Single(layout.Pages); Assert.Equal(3, layout.Pages[0].Regions.Count);
        var png = renderer.ExportPng(document, layout); Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(renderer.ExportPdf(document, layout)[..4]));
    }

    [Fact]
    public void RandomContinuousBandsPreserveTextAndAvoidVerticalOverlap()
    {
        var random = new Random(87031);
        for (var iteration = 0; iteration < 30; iteration++)
        {
            var document = new DocumentModel { Blocks = [] };
            for (var section = 0; section < 8; section++)
            {
                if (section > 0) document.Blocks.Add(Break(columns: random.Next(1, 4)));
                for (var p = 0; p < random.Next(1, 9); p++)
                    document.Blocks.Add(P(string.Join(' ', Enumerable.Repeat("words", random.Next(1, 90)))));
            }
            DocumentJson.Validate(document); var layout = Layout(document);
            Assert.Equal(document.Paragraphs().Sum(p => p.Length), layout.Lines.Sum(l => l.Chunks.Sum(c => c.Text.Length)));
            foreach (var page in layout.Pages)
            {
                for (var i = 1; i < page.Regions.Count; i++)
                    Assert.True(page.Regions[i].Top >= page.Regions[i - 1].Bottom - 0.001);
                Assert.All(page.Lines, l => Assert.True(l.Y + l.Height <= page.Settings.Height - l.Region!.Section.Page.MarginBottom + 0.001));
            }
        }
    }
    [Fact]
    public void ConsecutiveEmptySectionBoundariesNeverProduceNegativeSectionPageIndexes()
    {
        var document = new DocumentModel { Blocks = [P("First"), Break(), Break(SectionBreakKind.NextPage), P("Last")] };
        var layout = Layout(document);
        Assert.All(layout.Pages.SelectMany(p => p.Regions), r => Assert.True(r.SectionPageIndex >= 0));
        Assert.Equal(2, layout.Pages.Count);
    }

    [Theory]
    [InlineData(SectionBreakKind.Continuous)]
    [InlineData(SectionBreakKind.NextColumn)]
    public void ChangingSectionStartAndPropertiesIsOneUndoableOperation(SectionBreakKind kind)
    {
        var editor = new EditorSession(DocumentJson.FromText("First")); editor.SetSelection(5, 5);
        editor.InsertSectionBreak(); var before = editor.UndoCount;
        editor.SetSection(s => s with { Header = "Changed header" }, kind);
        Assert.Equal(before + 1, editor.UndoCount); Assert.Equal(kind, editor.CurrentSectionStart);
        editor.Undo(); Assert.Equal(SectionBreakKind.NextPage, editor.CurrentSectionStart); Assert.Null(editor.CurrentSection.Header);
        editor.Redo(); Assert.Equal(kind, editor.CurrentSectionStart); Assert.Equal("Changed header", editor.CurrentSection.Header);
    }

    [Fact]
    public void SectionStartChangesRejectReadOnlyAndInvalidEnums()
    {
        var editor = new EditorSession(new DocumentModel());
        Assert.Throws<InvalidOperationException>(() => editor.SetSectionBreakKind(SectionBreakKind.Continuous));
        editor.InsertSectionBreak();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetSectionBreakKind((SectionBreakKind)99));
        editor.IsReadOnly = true;
        Assert.Throws<InvalidOperationException>(() => editor.SetSectionBreakKind(SectionBreakKind.Continuous));
    }

    [Fact]
    public void HtmlContinuousSectionsReuseNamedPageWithoutForcingPageBreak()
    {
        var html = HtmlExporter.Export(new DocumentModel { Blocks = [P("Before"), Break(), P("After")] });
        var after = html[html.IndexOf("data-section-start=\"Continuous\"", StringComparison.Ordinal)..];
        Assert.Contains("page:section0;", after); Assert.Contains("break-before:auto;", after);
        Assert.DoesNotContain("break-before:page;", after);
    }

    [Fact]
    public void RepeatedLayoutDoesNotLeakOldRegionReferencesFromParagraphCache()
    {
        var document = new DocumentModel { Blocks = [P("First"), Break(), P("Second")] };
        var engine = new PageLayoutEngine(new MonospaceTextMetrics());
        var a = engine.Layout(document); var b = engine.Layout(document);
        Assert.NotSame(a.Lines.Last().Region, b.Lines.Last().Region);
        Assert.Equal(a.Lines.Last().Y, b.Lines.Last().Y);
        Assert.Equal(2L, engine.ParagraphCache.Hits);
    }

}
