using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Layout;
using TextSpace.Skia;
using Xunit;

namespace TextSpace.Tests;

public sealed class TableLayoutTests
{
    private static DocumentLayout Layout(DocumentModel d) => new PageLayoutEngine(new MonospaceTextMetrics()).Layout(d);
    [Fact] public void MergedCellHasOneBorderBoxWithCombinedWidthAndHeight()
    {
        var (editor, table) = TableMergingTests.Create(); editor.MergeTableCells(0, 0, 2, 2);
        var layout = Layout(editor.Document); var frames = layout.Pages.SelectMany(p => p.Cells).Where(c => c.TableId == table.Id).ToArray();
        Assert.Equal(6, frames.Length); Assert.Equal(312, frames[0].Bounds.Width, 5); Assert.True(frames[0].Bounds.Height >= 48);
        Assert.Equal(editor.Document.Paragraphs().Sum(p => p.Length), layout.Lines.Sum(l => l.Chunks.Sum(c => c.Text.Length)));
    }
    [Fact] public void NestedTableGeometryStaysInsideItsParentCell()
    {
        var (editor, outer) = TableMergingTests.Create(1, 2); var inner = TableBlock.Create(2, 2);
        outer.Rows[0].Cells[0].Blocks = [inner, new Paragraph("Nested caption")];
        var layout = Layout(editor.Document); var cells = layout.Pages.SelectMany(p => p.Cells).ToArray();
        var parent = cells.First(c => c.TableId == outer.Id); var nested = cells.Where(c => c.TableId == inner.Id).ToArray();
        Assert.Equal(4, nested.Length);
        Assert.All(nested, c => { Assert.True(c.Bounds.X > parent.Bounds.X); Assert.True(c.Bounds.Right < parent.Bounds.Right + 0.01); Assert.True(c.Bounds.Bottom < parent.Bounds.Bottom + 0.01); });
        Assert.Equal(6, cells.Length);
    }
    [Fact] public void CellPictureIsRenderedRatherThanDropped()
    {
        var (editor, table) = TableMergingTests.Create(1, 1);
        table.Rows[0].Cells[0].Blocks.Insert(0, new ImageBlock { Width = 100, Height = 50, Data = [] });
        var layout = Layout(editor.Document); var picture = Assert.Single(layout.Pages.SelectMany(p => p.Images));
        Assert.Equal(100, picture.Bounds.Width); Assert.Equal(50, picture.Bounds.Height);
    }
    [Theory][InlineData(CellVerticalAlignment.Top)][InlineData(CellVerticalAlignment.Center)][InlineData(CellVerticalAlignment.Bottom)]
    public void CellVerticalAlignmentUsesAvailableRowHeight(CellVerticalAlignment alignment)
    {
        var (editor, table) = TableMergingTests.Create(1, 1); table.Rows[0].MinimumHeight = 100;
        table.Rows[0].Cells[0].VerticalAlignment = alignment;
        var layout = Layout(editor.Document); var frame = layout.Pages[0].Cells[0]; var line = layout.Lines.First();
        var extra = 100 - 12 - line.Height - 8;
        var shift = alignment == CellVerticalAlignment.Center ? extra / 2 : alignment == CellVerticalAlignment.Bottom ? extra : 0;
        Assert.Equal(frame.Bounds.Y + 6 + shift, line.Y, 5);
    }
    [Fact] public void RepeatedHeadersAreVisualReplicasNotDuplicateTextRanges()
    {
        var (editor, table) = TableMergingTests.Create(50, 2); var layout = Layout(editor.Document);
        Assert.True(layout.Pages.Count > 1); Assert.Contains(layout.Pages[1].Lines, l => l.IsReplica);
        Assert.DoesNotContain(layout.Lines, l => l.IsReplica);
        var header = (Paragraph)table.Rows[0].Cells[0].Blocks[0]; var start = editor.Index.StartOf(header);
        Assert.Equal(0, layout.Caret(start).PageIndex);
        Assert.Equal(editor.Document.Paragraphs().Sum(p => p.Length), layout.Lines.Sum(l => l.Chunks.Sum(c => c.Text.Length)));
    }
    [Fact] public void RepeatedHeaderCanBeDisabled()
    {
        var (editor, table) = TableMergingTests.Create(50, 2); table.RepeatHeaderRow = false;
        Assert.DoesNotContain(Layout(editor.Document).Pages.SelectMany(p => p.Lines), l => l.IsReplica);
    }
    [Fact] public void MergeAcrossHeaderBoundaryDoesNotRepeatPartialCell()
    {
        var (editor, table) = TableMergingTests.Create(50, 2); editor.MergeTableCells(0, 0, 2, 1);
        Assert.DoesNotContain(Layout(editor.Document).Pages.SelectMany(p => p.Lines), l => l.IsReplica);
    }
    [Fact] public void TallMergedCellPaginatesWithoutLosingOrDuplicatingText()
    {
        var (editor, table) = TableMergingTests.Create(2, 2); editor.MergeTableCells(0, 0, 2, 1);
        table.Rows[0].Cells[0].Blocks = Enumerable.Range(0, 120).Select(i => (Block)new Paragraph($"Merged item {i}")).ToList();
        var layout = Layout(editor.Document); Assert.True(layout.Pages.Count >= 3);
        Assert.Equal(editor.Document.Paragraphs().Sum(p => p.Length), layout.Lines.Sum(l => l.Chunks.Sum(c => c.Text.Length)));
        Assert.Contains(layout.Pages[1].Cells, c => !c.DrawTop);
        Assert.All(layout.Lines, l => Assert.True(l.Y + l.Height <= layout.Pages[l.PageIndex].Settings.Height - 72 + 0.01));
    }
    [Fact] public void MixedFontTallRowsFinishAssignedLinesWithinTheBody()
    {
        var (editor, table) = TableMergingTests.Create(1, 2); table.HeaderRow = false;
        for (var c = 0; c < 2; c++) table.Rows[0].Cells[c].Blocks = Enumerable.Range(0, 100)
            .Select(i => (Block)new Paragraph($"Cell {c}, paragraph {i}", new() { FontSize = c == 0 ? 11 : 17 })).ToList();
        var layout = Layout(editor.Document);
        Assert.Equal(editor.Document.Paragraphs().Sum(p => p.Length), layout.Lines.Sum(l => l.Chunks.Sum(c => c.Text.Length)));
        Assert.All(layout.Lines, l => Assert.True(l.Y + l.Height <= layout.Pages[l.PageIndex].Settings.Height - 72 + 0.01));
    }
    [Theory][InlineData(true, 0)][InlineData(false, 1)]
    public void RowSplitOptionControlsUseOfTheRemainingPage(bool allowSplit, int firstPage)
    {
        var table = TableBlock.Create(1, 1); table.HeaderRow = false;
        table.Rows[0].AllowSplit = allowSplit; table.Rows[0].MinimumHeight = 220;
        table.Rows[0].Cells[0].Blocks = Enumerable.Range(0, 10).Select(i => (Block)new Paragraph("Row line " + i)).ToList();
        var document = new DocumentModel { Blocks = [new Paragraph("Prefix", format: new() { SpaceAfter = 500 }), table, new Paragraph()] };
        var paragraph = (Paragraph)table.Rows[0].Cells[0].Blocks[0]; var layout = Layout(document);
        Assert.Equal(firstPage, layout.Caret(new TextIndex(document).StartOf(paragraph)).PageIndex);
        Assert.Equal(document.Paragraphs().Sum(p => p.Length), layout.Lines.Sum(l => l.Chunks.Sum(c => c.Text.Length)));
    }
    [Fact] public void CellGridsAndTextAreNotAliasedBetweenLayouts()
    {
        var (editor, _) = TableMergingTests.Create(); var engine = new PageLayoutEngine(new MonospaceTextMetrics());
        var first = engine.Layout(editor.Document); var second = engine.Layout(editor.Document);
        first.Pages[0].Lines[0].Chunks[0].X = 999;
        Assert.NotEqual(999, second.Pages[0].Lines[0].Chunks[0].X);
        Assert.Equal(0, first.Pages[0].Lines[0].Chunks[0].Carets[0]);
    }
    [Fact] public void MergedNestedPdfAndPngExportsAreReal()
    {
        var (editor, table) = TableMergingTests.Create(); editor.MergeTableCells(0, 0, 2, 2);
        var inner = TableBlock.Create(2, 2); table.Rows[0].Cells[0].Blocks.Insert(1, inner);
        using var renderer = new DocumentRenderer(); var pdf = renderer.ExportPdf(editor.Document); var png = renderer.ExportPng(editor.Document);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4)); Assert.Equal(137, png[0]);
        var directory = Environment.GetEnvironmentVariable("TEXTSPACE_ARTIFACTS");
        if (directory is not null) { Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "merged-nested.png"), png); File.WriteAllBytes(Path.Combine(directory, "merged-nested.pdf"), pdf); }
    }
}
