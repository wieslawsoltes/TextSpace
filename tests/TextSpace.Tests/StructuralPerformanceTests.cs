using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using Xunit;

namespace TextSpace.Tests;

public sealed class StructuralPerformanceTests
{
    [Fact]
    public void CanonicalNormalizationPreservesRunAndCollectionIdentity()
    {
        var paragraph = new Paragraph { Runs = [new("A"), new("B", new() { Bold = true })] };
        var runs = paragraph.Runs; var first = runs[0]; var second = runs[1];
        for (var i = 0; i < 100; i++) paragraph.Normalize();
        Assert.Same(runs, paragraph.Runs); Assert.Same(first, paragraph.Runs[0]); Assert.Same(second, paragraph.Runs[1]);
    }
    [Fact]
    public void NoncanonicalNormalizationStillRemovesEmptyAndJoinsEqualStyles()
    {
        var p = new Paragraph { Runs = [new("A"), new(""), new("B"), new("C", new() { Bold = true }), new("D", new() { Bold = true }), new("")] };
        p.Normalize(); Assert.Equal("ABCD", p.Text); Assert.Equal(2, p.Runs.Count);
        Assert.Equal("AB", p.Runs[0].Text); Assert.Equal("CD", p.Runs[1].Text);
        var runs = p.Runs; p.Normalize(); Assert.Same(runs, p.Runs);
    }
    [Fact]
    public void LargeIdentityRemapKeepsAllSurvivingAnchors()
    {
        var table = TableBlock.Create(100, 10);
        for (var row = 0; row < 100; row++)
            for (var column = 0; column < 10; column++)
                table.Rows[row].Cells[column].Blocks = [new Paragraph($"R{row}C{column}😀")];
        var document = new DocumentModel { Blocks = [table, new Paragraph("Tail")] };
        var before = new TextIndex(document);
        foreach (var p in before.Paragraphs) document.Bookmarks.Add(new() { Name = "B" + p.Paragraph.Id, Start = p.Start, End = p.End });
        var expected = document.Bookmarks.ToDictionary(b => b.Name, b => before.Text.Substring(b.Start, b.End - b.Start));
        var editor = new EditorSession(document); var target = editor.Index.StartOf((Paragraph)table.Rows[50].Cells[5].Blocks[0]); editor.SetSelection(target, target);
        editor.AddTableColumn(); var next = editor.Index;
        foreach (var b in document.Bookmarks) Assert.Equal(expected[b.Name], next.Text.Substring(b.Start, b.End - b.Start));
        editor.Undo(); Assert.Equal(before.Text, editor.Index.Text);
    }
    [Fact]
    public void RemovedAnchorFallsToNextSurvivorThenPreviousEnd()
    {
        var editor = TableMergingTests.Create(3, 2).Editor; var table = editor.Document.Blocks.OfType<TableBlock>().Single();
        var removed = (Paragraph)table.Rows[1].Cells[1].Blocks[0]; var at = editor.Index.StartOf(removed);
        editor.Document.Bookmarks.Add(new() { Name = "Removed", Start = at, End = at + removed.Length });
        TableMergingTests.Select(editor, 1, 0); editor.DeleteTableRow();
        var next = (Paragraph)table.Rows[1].Cells[0].Blocks[0]; var bookmark = editor.Document.Bookmarks.Single();
        Assert.Equal(editor.Index.StartOf(next), bookmark.Start); Assert.Equal(bookmark.Start, bookmark.End);
        editor.Undo(); Assert.Equal("R1C1", editor.Index.Text.Substring(editor.Document.Bookmarks[0].Start, editor.Document.Bookmarks[0].End - editor.Document.Bookmarks[0].Start));
    }
    [Fact]
    public void DeterministicStructuralFuzzMaintainsTopologyAndUndo()
    {
        var editor = TableMergingTests.Create(6, 6).Editor; var random = new Random(731);
        for (var i = 0; i < 120; i++)
        {
            var table = editor.Document.Blocks.OfType<TableBlock>().Single(); var grid = new TableGrid(table);
            var active = grid.Regions[random.Next(grid.Regions.Count)]; TableMergingTests.Select(editor, active.Row, active.Column);
            var before = DocumentJson.Save(editor.Document); var undoCount = editor.UndoCount;
            switch (random.Next(7))
            {
                case 0: editor.SplitTableCell(); break;
                case 1 when grid.RowCount < 12: editor.AddTableRow(); break;
                case 2 when grid.ColumnCount < 12: editor.AddTableColumn(); break;
                case 3 when grid.RowCount > 1: editor.DeleteTableRow(); break;
                case 4 when grid.ColumnCount > 1: editor.DeleteTableColumn(); break;
                default:
                    // Whole-table merges always select existing spans in full.
                    editor.MergeTableCells(0, 0, grid.RowCount, grid.ColumnCount); break;
            }
            DocumentJson.Validate(editor.Document); _ = new TableGrid(editor.Document.Blocks.OfType<TableBlock>().Single());
            if (i % 3 == 0 && editor.UndoCount > undoCount)
            {
                editor.Undo(); Assert.Equal(before, DocumentJson.Save(editor.Document));
                editor.Redo(); DocumentJson.Validate(editor.Document);
            }
        }
    }
}
