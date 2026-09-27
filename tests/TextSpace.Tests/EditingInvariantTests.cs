using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using Xunit;

namespace TextSpace.Tests;

public sealed class EditingInvariantTests
{
    private static EditorSession Text(string text) => new(DocumentJson.FromText(text));

    [Theory]
    [InlineData("😀", 1, 0)]
    [InlineData("Ae\u0301B", 2, 1)]
    [InlineData("A👩‍💻B", 4, 1)]
    public void CaretInsideGraphemeRemainsCollapsed(string text, int position, int expected)
    {
        var editor = Text(text);
        editor.SetSelection(position, position);
        Assert.Equal(new TextSelection(expected, expected), editor.Selection);
        editor.InsertText("X");
        Assert.Equal(text.Insert(expected, "X"), editor.Document.PlainText);
    }

    [Fact]
    public void BackwardSelectionExpandsAtBothGraphemeBoundaries()
    {
        var editor = Text("A😀B");
        editor.SetSelection(2, 1);
        Assert.Equal(new TextSelection(3, 1), editor.Selection);
        Assert.Equal("😀", editor.SelectedText());
    }

    [Fact]
    public void ReplacementEndIsMeasuredBeforeSnappingItsStart()
    {
        var editor = Text("A😀BC");
        editor.Replace(2, 2, "X");
        Assert.Equal("AXC", editor.Document.PlainText);
        editor.Undo();
        Assert.Equal("A😀BC", editor.Document.PlainText);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4, 0)]
    [InlineData(2, int.MaxValue)]
    public void InvalidReplacementDoesNotMutateOrCreateHistory(int start, int length)
    {
        var editor = Text("abc");
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Replace(start, length, "X"));
        Assert.Equal("abc", editor.Document.PlainText);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void CompoundEditPublishesOnlyCommittedState()
    {
        var editor = Text("abc");
        var observed = new List<(EditorChangeKind Kind, string Text)>();
        editor.Changed += (_, change) => observed.Add((change.Kind, editor.Document.PlainText));
        editor.Execute("Compound", () =>
        {
            editor.SetSelection(0, 0);
            editor.InsertText("1");
            editor.InsertText("2");
            editor.SetSelection(0, 2);
            editor.ToggleBold();
        });
        Assert.Equal("12abc", editor.Document.PlainText);
        Assert.Equal((EditorChangeKind.Document, "12abc"), Assert.Single(observed));
        Assert.Equal(1, editor.UndoCount);
        editor.Undo();
        Assert.Equal("abc", editor.Document.PlainText);
    }

    [Fact]
    public void RollbackPublishesRestoredStateWithoutChangingHistory()
    {
        var editor = Text("abc");
        var observed = new List<string>();
        editor.Changed += (_, _) => observed.Add(editor.Document.PlainText);
        Assert.Throws<InvalidOperationException>(() => editor.Execute("Failed edit", () =>
        {
            editor.SetSelection(0, 1);
            editor.InsertText("wrong");
            throw new InvalidOperationException("Abort");
        }));
        Assert.Equal("abc", Assert.Single(observed));
        Assert.Equal(new TextSelection(0, 0), editor.Selection);
        Assert.Equal(0, editor.UndoCount);
        Assert.Equal(0, editor.Revision);
    }

    [Fact]
    public void SubscriberFailureDoesNotRollBackCommittedDocument()
    {
        var editor = Text("abc");
        editor.Changed += (_, change) => { if (change.Kind == EditorChangeKind.Document) throw new InvalidOperationException("Subscriber failed"); };
        Assert.Throws<InvalidOperationException>(() => editor.InsertText("X"));
        Assert.Equal("Xabc", editor.Document.PlainText);
        Assert.Equal(1, editor.UndoCount);
        Assert.Equal(1, editor.Revision);
    }

    [Fact]
    public void NoOpDoesNotDiscardRedoHistory()
    {
        var editor = Text("abc");
        editor.InsertText("X");
        editor.Undo();
        editor.Execute("No op", () => { });
        Assert.True(editor.CanRedo);
        editor.Redo();
        Assert.Equal("Xabc", editor.Document.PlainText);
    }

    [Fact]
    public void HistoryRespectsBothEntryAndByteBudgets()
    {
        var editor = new EditorSession(DocumentJson.FromText("abc"), maximumHistoryEntries: 3, maximumHistoryBytes: 20_000);
        for (var i = 0; i < 20; i++) editor.InsertText("X");
        Assert.Equal(3, editor.UndoCount);
        Assert.InRange(editor.HistoryBytes, 1, 20_000);
        editor.Undo(); editor.Undo();
        Assert.InRange(editor.HistoryBytes, 1, 20_000);
        editor.Redo();
        Assert.InRange(editor.HistoryBytes, 1, 20_000);
        var tiny = new EditorSession(DocumentJson.FromText("abc"), maximumHistoryBytes: 1);
        tiny.InsertText("X");
        Assert.Equal("Xabc", tiny.Document.PlainText);
        Assert.False(tiny.CanUndo);
        Assert.Equal(0, tiny.HistoryBytes);
    }

    [Fact]
    public void ReadOnlyBlocksCollapsedCaretFormatting()
    {
        var editor = Text("abc");
        editor.IsReadOnly = true;
        Assert.Throws<InvalidOperationException>(editor.ToggleBold);
        Assert.Throws<InvalidOperationException>(() => editor.SetFont("Arial"));
        Assert.Throws<InvalidOperationException>(() => editor.ChangeCase(true));
        Assert.False(editor.TypingStyle.Bold);
        Assert.False(editor.CanUndo);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteFontSizesCannotPoisonTypingState(double size)
    {
        var editor = Text("abc");
        var before = editor.TypingStyle;
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetFontSize(size));
        Assert.Equal(before, editor.TypingStyle);
        Assert.Throws<ArgumentException>(() => editor.FormatText("Invalid", s => s with { FontSize = size }));
        Assert.Equal(before, editor.TypingStyle);
    }

    [Fact]
    public void CaseConversionPreservesRunFormattingAndDirection()
    {
        var bold = new TextStyle { Bold = true };
        var link = new TextStyle { Italic = true, Hyperlink = "https://example.com" };
        var paragraph = new Paragraph { Runs = [new("one ", bold), new("two", link)] };
        var editor = new EditorSession(new() { Blocks = [paragraph] });
        editor.SetSelection(7, 0);
        editor.ChangeCase(true);
        Assert.Equal("ONE TWO", editor.Document.PlainText);
        Assert.Equal(new TextSelection(7, 0), editor.Selection);
        Assert.Equal(bold, editor.CurrentParagraph.Runs[0].Style);
        Assert.Equal(link, editor.CurrentParagraph.Runs[1].Style);
        Assert.Equal(1, editor.UndoCount);
        editor.Undo();
        Assert.Equal("one two", editor.Document.PlainText);
    }

    [Fact]
    public void ClearFormattingIsOneUndoUnit()
    {
        var paragraph = new Paragraph("abc", new() { Bold = true }, new() { Alignment = TextAlignment.Right });
        var editor = new EditorSession(new() { Blocks = [paragraph] });
        editor.SelectAll(); editor.ClearFormatting();
        Assert.False(editor.CurrentParagraph.Runs[0].Style.Bold);
        Assert.Equal(TextAlignment.Left, editor.CurrentParagraph.Format.Alignment);
        Assert.Equal(1, editor.UndoCount);
        editor.Undo();
        Assert.True(editor.CurrentParagraph.Runs[0].Style.Bold);
        Assert.Equal(TextAlignment.Right, editor.CurrentParagraph.Format.Alignment);
    }

    [Fact]
    public void TableReplacesSelectionInOneUndoUnit()
    {
        var editor = Text("abcdef");
        editor.SetSelection(2, 5);
        editor.InsertTable(2, 2);
        Assert.Single(editor.Document.Blocks.OfType<TableBlock>());
        Assert.Equal(1, editor.UndoCount);
        editor.Undo();
        Assert.Equal("abcdef", editor.Document.PlainText);
        Assert.Equal(new TextSelection(2, 5), editor.Selection);
        Assert.Empty(editor.Document.Blocks.OfType<TableBlock>());
    }

    private static (EditorSession Editor, TableBlock Table, string AfterId) TableDocument()
    {
        var table = TableBlock.Create(2, 2);
        var texts = new[] { "A", "B", "C", "D" };
        var i = 0;
        foreach (var cell in table.Rows.SelectMany(r => r.Cells)) cell.Blocks = [new Paragraph(texts[i++])];
        var after = new Paragraph("after");
        var document = new DocumentModel { Blocks = [new Paragraph("before"), table, after] };
        var editor = new EditorSession(document);
        var start = editor.Index.StartOf(after);
        editor.SetSelection(start, start + after.Length);
        editor.AddComment("Keep this attached to after");
        var first = editor.Index.StartOf((Paragraph)table.Rows[0].Cells[0].Blocks[0]);
        editor.SetSelection(first, first);
        return (editor, table, after.Id);
    }

    [Fact]
    public void InsertingRowsAndColumnsPreservesCommentsByParagraphIdentity()
    {
        var (editor, _, afterId) = TableDocument();
        editor.AddTableRowAbove();
        editor.AddTableColumn();
        var after = editor.Index.Paragraphs.Single(p => p.Paragraph.Id == afterId);
        var comment = Assert.Single(editor.Document.Comments);
        Assert.Equal(after.Start, comment.Start);
        Assert.Equal(after.End, comment.End);
        Assert.Equal("after", editor.Index.Text.Substring(comment.Start, comment.End - comment.Start));
    }

    [Fact]
    public void InsertColumnUsesCurrentCellInsteadOfAlwaysAppending()
    {
        var (editor, table, _) = TableDocument();
        editor.AddTableColumn();
        Assert.Equal(3, table.Rows[0].Cells.Count);
        Assert.Equal("A", DocumentModel.Walk(table.Rows[0].Cells[0].Blocks).First().Text);
        Assert.Equal("", DocumentModel.Walk(table.Rows[0].Cells[1].Blocks).First().Text);
        Assert.Equal("B", DocumentModel.Walk(table.Rows[0].Cells[2].Blocks).First().Text);
        Assert.Equal("A", editor.CurrentParagraph.Text);
        editor.AddTableColumnBefore();
        Assert.Equal("", DocumentModel.Walk(table.Rows[0].Cells[0].Blocks).First().Text);
        Assert.Equal("A", editor.CurrentParagraph.Text);
    }

    [Fact]
    public void DeletingColumnPreservesSurvivingReviewAnchor()
    {
        var (editor, table, afterId) = TableDocument();
        editor.DeleteTableColumn();
        Assert.Single(table.Rows[0].Cells);
        Assert.Equal("B", editor.CurrentParagraph.Text);
        var after = editor.Index.Paragraphs.Single(p => p.Paragraph.Id == afterId);
        Assert.Equal(after.Start, editor.Document.Comments[0].Start);
        Assert.Equal(after.End, editor.Document.Comments[0].End);
    }

    [Fact]
    public void DeletingFinalColumnRemovesTableAndRetainsEditableDocument()
    {
        var editor = Text("");
        editor.InsertTable(2, 1);
        editor.DeleteTableColumn();
        Assert.Empty(editor.Document.Blocks.OfType<TableBlock>());
        Assert.NotEmpty(editor.Document.Paragraphs());
        editor.InsertText("Still editable");
        Assert.Contains("Still editable", editor.Document.PlainText);
    }

    [Fact]
    public void DeletingAChangedCellMakesIndividualRevisionRejectionUnsafe()
    {
        var (editor, _, _) = TableDocument();
        editor.TrackChanges = true;
        editor.InsertText("inserted");
        var change = Assert.Single(editor.Document.Changes);
        editor.DeleteTableRow();
        Assert.False(change.CanReject);
        Assert.Throws<InvalidOperationException>(() => editor.RejectChange(change.Id));
    }
}
