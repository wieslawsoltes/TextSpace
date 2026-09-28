using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using Xunit;

namespace TextSpace.Tests;

public sealed class TableMergingTests
{
    internal static (EditorSession Editor, TableBlock Table) Create(int rows = 3, int columns = 3)
    {
        var table = TableBlock.Create(rows, columns);
        for (var r = 0; r < rows; r++) for (var c = 0; c < columns; c++)
            table.Rows[r].Cells[c].Blocks = [new Paragraph($"R{r}C{c}")];
        var editor = new EditorSession(new() { Blocks = [table, new Paragraph("After")] });
        return (editor, table);
    }
    internal static void Select(EditorSession editor, int row, int column)
    {
        var table = editor.Document.Blocks.OfType<TableBlock>().First();
        var paragraph = DocumentModel.Walk(new TableGrid(table).At(row, column).Cell.Blocks).First();
        var position = editor.Index.StartOf(paragraph); editor.SetSelection(position, position);
    }
    [Theory][InlineData(1, 2)][InlineData(2, 1)][InlineData(2, 2)][InlineData(3, 3)]
    public void RectangleMergePreservesEachParagraphExactlyOnce(int rows, int columns)
    {
        var (editor, table) = Create(); var ids = editor.Document.Paragraphs().Select(p => p.Id).Order().ToArray();
        editor.MergeTableCells(0, 0, rows, columns); var grid = new TableGrid(table);
        Assert.Equal(9 - rows * columns + 1, grid.Regions.Count);
        Assert.Equal(ids, editor.Document.Paragraphs().Select(p => p.Id).Order());
        Assert.Equal(rows, grid.At(0, 0).RowSpan); Assert.Equal(columns, grid.At(0, 0).ColumnSpan);
        for (var r = 0; r < rows; r++) for (var c = 0; c < columns; c++) Assert.Same(grid.At(0, 0), grid.At(r, c));
        DocumentJson.Validate(editor.Document);
    }
    [Fact] public void SplitKeepsContentInAnchorAndRestoresLogicalSlots()
    {
        var (editor, table) = Create(); editor.MergeTableCells(0, 0, 2, 2); editor.SplitTableCell();
        Assert.Equal(9, new TableGrid(table).Regions.Count);
        Assert.Equal(4, table.Rows[0].Cells[0].Blocks.Count);
        Assert.Equal("", ((Paragraph)table.Rows[1].Cells[1].Blocks[0]).Text);
    }
    [Fact] public void MergeAndSplitAreSingleUndoUnits()
    {
        var (editor, _) = Create(); var original = editor.Document.PlainText;
        editor.MergeTableCells(0, 0, 2, 2); var merged = editor.Document.PlainText;
        Assert.Equal(1, editor.UndoCount); editor.SplitTableCell(); Assert.Equal(2, editor.UndoCount);
        editor.Undo(); Assert.Equal(merged, editor.Document.PlainText); Assert.Equal(2, editor.CurrentCell.RowSpan);
        editor.Undo(); Assert.Equal(original, editor.Document.PlainText); editor.Redo(); Assert.Equal(2, editor.CurrentCell.RowSpan);
    }
    [Fact] public void CrossingExistingSpanIsRejectedAtomically()
    {
        var (editor, _) = Create(); editor.MergeTableCells(0, 0, 2, 2); var json = DocumentJson.Save(editor.Document);
        Assert.Throws<InvalidOperationException>(() => editor.MergeTableCells(1, 0, 2, 2));
        Assert.Equal(json, DocumentJson.Save(editor.Document));
    }
    [Fact] public void TabSkipsCoveredSlotsAndEditsAnAnchorOnlyOnce()
    {
        var (editor, _) = Create(); editor.MergeTableCells(0, 0, 2, 2);
        Assert.True(editor.MoveTableCell(false)); Assert.Equal("R0C2", editor.CurrentParagraph.Text);
        Assert.True(editor.MoveTableCell(false)); Assert.Equal("R1C2", editor.CurrentParagraph.Text);
        Assert.True(editor.MoveTableCell(true)); Assert.Equal("R0C2", editor.CurrentParagraph.Text);
    }
    [Fact] public void MergeRemapsBookmarksCommentsAndFieldsByParagraphIdentity()
    {
        var (editor, _) = Create(); Select(editor, 1, 1);
        var p = editor.Index.At(editor.Selection.Active); editor.SetSelection(p.Start, p.End);
        editor.SetBookmark("Target"); editor.AddComment("Inspect");
        var field = new DocumentField { Start = p.Start, End = p.End, Instruction = "TITLE", Locked = true }; editor.Document.Fields.Add(field);
        Select(editor, 0, 0); editor.MergeTableCells(0, 0, 2, 2);
        var text = editor.Document.PlainText; var bookmark = Assert.Single(editor.Document.Bookmarks); var comment = Assert.Single(editor.Document.Comments);
        Assert.Equal("R1C1", text[bookmark.Start..bookmark.End]); Assert.Equal("R1C1", text[comment.Start..comment.End]);
        Assert.Equal("R1C1", text[field.Start..field.End]);
    }
    [Fact] public void InsertRowInsideOtherSpanExtendsIt()
    {
        var (editor, table) = Create(); editor.MergeTableCells(0, 0, 2, 1); Select(editor, 0, 1); editor.AddTableRow();
        Assert.Equal(4, table.Rows.Count); Assert.Equal(3, new TableGrid(table).At(0, 0).RowSpan);
        Assert.Equal("", editor.CurrentParagraph.Text); DocumentJson.Validate(editor.Document);
    }
    [Fact] public void InsertColumnInsideOtherSpanExtendsIt()
    {
        var (editor, table) = Create(); editor.MergeTableCells(0, 0, 1, 2); Select(editor, 1, 0); editor.AddTableColumn();
        Assert.Equal(4, table.Rows[0].Cells.Count); Assert.Equal(3, new TableGrid(table).At(0, 0).ColumnSpan);
        DocumentJson.Validate(editor.Document);
    }
    [Fact] public void DeleteAnchorRowRetainsSpanningContent()
    {
        var (editor, table) = Create(); editor.MergeTableCells(0, 0, 2, 2); editor.DeleteTableRow();
        Assert.Equal(2, table.Rows.Count); Assert.Equal(1, new TableGrid(table).At(0, 0).RowSpan);
        Assert.Contains("R0C0", editor.Document.PlainText); Assert.Contains("R1C1", editor.Document.PlainText);
    }
    [Fact] public void DeleteAnchorColumnRetainsSpanningContent()
    {
        var (editor, table) = Create(); editor.MergeTableCells(0, 0, 2, 2); editor.DeleteTableColumn();
        Assert.Equal(2, table.Rows[0].Cells.Count); Assert.Equal(1, new TableGrid(table).At(0, 0).ColumnSpan);
        Assert.Contains("R0C0", editor.Document.PlainText); Assert.Contains("R1C1", editor.Document.PlainText);
    }
    [Fact] public void RowPropertiesFollowInsertionAndDeletion()
    {
        var (editor, table) = Create(); table.Rows[1].MinimumHeight = 60; table.Rows[1].AllowSplit = false;
        editor.AddTableRow(); Assert.Equal(60, table.Rows[2].MinimumHeight); Assert.False(table.Rows[2].AllowSplit);
        editor.DeleteTableRow(); Assert.Equal(60, table.Rows[1].MinimumHeight);
    }
    [Fact] public void NativeRoundtripPreservesSpansAndCoveredSlots()
    {
        var (editor, _) = Create(); editor.MergeTableCells(0, 0, 2, 2); editor.SetCellVerticalAlignment(CellVerticalAlignment.Bottom);
        var document = DocumentJson.Load(DocumentJson.Save(editor.Document)); var table = document.Blocks.OfType<TableBlock>().First();
        Assert.Equal(2, new TableGrid(table).At(1, 1).RowSpan); Assert.Empty(table.Rows[1].Cells[1].Blocks);
        Assert.Equal(CellVerticalAlignment.Bottom, table.Rows[0].Cells[0].VerticalAlignment);
    }
    [Fact] public void CoveredContentAndOverlappingSpansAreRejected()
    {
        var table = TableBlock.Create(2, 2); table.Rows[0].Cells[0].ColumnSpan = 2;
        Assert.Throws<InvalidDataException>(() => new TableGrid(table));
        table.Rows[0].Cells[1].Blocks.Clear(); table.Rows[0].Cells[1].RowSpan = 2;
        Assert.Throws<InvalidDataException>(() => new TableGrid(table));
    }
    [Theory][InlineData(0,1)][InlineData(-1,1)][InlineData(1,4)][InlineData(int.MaxValue,1)]
    public void InvalidSpansCannotReachLayout(int rows, int columns)
    {
        var table = TableBlock.Create(3, 3); table.Rows[0].Cells[0].RowSpan = rows; table.Rows[0].Cells[0].ColumnSpan = columns;
        Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(new() { Blocks = [table] }));
    }
    [Fact] public void ReadOnlyBlocksMergeAndSplit()
    {
        var (editor, _) = Create(); editor.IsReadOnly = true;
        Assert.Throws<InvalidOperationException>(() => editor.MergeTableCells(0, 0, 2, 2));
        Assert.Throws<InvalidOperationException>(editor.SplitTableCell);
    }
    [Fact] public void SelectionRectangleMergeUsesLogicalCoordinates()
    {
        var (editor, table) = Create(); var a = (Paragraph)table.Rows[0].Cells[0].Blocks[0]; var b = (Paragraph)table.Rows[1].Cells[1].Blocks[0];
        editor.SetSelection(editor.Index.StartOf(a), editor.Index.StartOf(b) + b.Length);
        editor.MergeSelectedTableCells(); Assert.Equal(2, new TableGrid(table).At(0, 0).ColumnSpan);
    }
    [Fact] public void HtmlExportsRealRowspanAndColspan()
    {
        var (editor, _) = Create(); editor.MergeTableCells(0, 0, 2, 2); editor.SetCellVerticalAlignment(CellVerticalAlignment.Center);
        var html = HtmlExporter.Export(editor.Document);
        Assert.Contains("rowspan=\"2\" colspan=\"2\"", html); Assert.Contains("vertical-align:middle", html);
        Assert.Equal(1, html.Split("R1C1").Length - 1);
    }
}
