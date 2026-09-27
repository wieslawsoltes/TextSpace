using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class LayoutTests
{
    private static DocumentLayout Layout(DocumentModel document) => new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document);
    [Fact] public void EmptyDocumentHasOnePageAndCaret() { var layout = Layout(new()); Assert.Single(layout.Pages); Assert.Single(layout.Lines); Assert.Equal(72, layout.Caret(0).X); }
    [Fact] public void WrapsLongParagraph() { var d = DocumentJson.FromText(string.Join(" ", Enumerable.Repeat("word", 100))); var l = Layout(d); Assert.True(l.Lines.Count() > 3); Assert.All(l.Lines, line => Assert.True(line.X + line.Width <= 541)); }
    [Fact] public void LongUnbrokenTextAlwaysProgresses() { var d = DocumentJson.FromText(new string('x', 10_000)); var l = Layout(d); Assert.True(l.Pages.Count > 1); Assert.Equal(10_000, l.Lines.Last().End); }
    [Fact] public void PageBreakCreatesNewPage() { var d = new DocumentModel { Blocks = [new Paragraph("first"), new PageBreakBlock(), new Paragraph("second")] }; var l = Layout(d); Assert.Equal(2, l.Pages.Count); Assert.Equal("second", l.Pages[1].Lines[0].Chunks[0].Text); }
    [Fact] public void PageBreakBeforeMovesHeading() { var d = new DocumentModel { Blocks = [new Paragraph("first"), new Paragraph("heading", format: new() { PageBreakBefore = true })] }; Assert.Equal(2, Layout(d).Pages.Count); }
    [Fact] public void AllTextIsAccountedFor() { var d = SampleDocument.Create(); var l = Layout(d); Assert.Equal(d.Paragraphs().Sum(p => p.Length), l.Lines.SelectMany(line => line.Chunks).Sum(c => c.Text.Length)); }
    [Fact] public void TableProducesCellGeometry() { var d = new DocumentModel { Blocks = [TableBlock.Create(3, 4), new Paragraph()] }; var l = Layout(d); Assert.Equal(12, l.Pages.Sum(p => p.Cells.Count)); }
    [Fact] public void CenteredTextHasHorizontalOffset() { var d = new DocumentModel { Blocks = [new Paragraph("center", format: new() { Alignment = TextAlignment.Center })] }; Assert.True(Layout(d).Lines.First().X > 200); }
    [Fact] public void HitTestReturnsNearestCaret() { var d = DocumentJson.FromText("ABCDE"); var l = Layout(d); var caret = l.Caret(3); Assert.Equal(3, l.HitTest(caret.X, caret.Y + caret.Height / 2)); }
    [Fact] public void MultipleColumnsFlowBeforeNextPage() { var d = DocumentJson.FromText(string.Join("\n", Enumerable.Repeat("A line", 60))); d.Page = d.Page with { Columns = 2 }; var l = Layout(d); Assert.Contains(l.Pages[0].Lines, line => line.X > 300); }
    [Fact] public void AllPageLinesStayWithinPageBounds() { var d = SampleDocument.Create(); var l = Layout(d); Assert.All(l.Lines, line => { Assert.True(line.Y >= 0); Assert.True(line.Y + line.Height <= d.Page.Height); }); }
    [Fact] public void LandscapeHasSwappedPageDimensions() { var p = new PageSettings().Landscape(); Assert.Equal(792, p.Width); Assert.Equal(612, p.Height); }
}
