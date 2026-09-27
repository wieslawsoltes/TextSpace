using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using Xunit;

namespace TextSpace.Tests;

public sealed class EditingTests
{
    private static EditorSession Create(string text = "Hello world") => new(DocumentJson.FromText(text));
    [Fact] public void InsertsAtCaret() { var e = Create(); e.SetSelection(5, 5); e.InsertText(", beautiful"); Assert.Equal("Hello, beautiful world", e.Document.PlainText); }
    [Fact] public void ReplacesRange() { var e = Create(); e.SetSelection(6, 11); e.InsertText("Uno"); Assert.Equal("Hello Uno", e.Document.PlainText); Assert.Equal(9, e.Selection.Active); }
    [Fact] public void SplitsParagraphAndRetainsSuffix() { var e = Create(); e.SetSelection(5, 5); e.InsertText("\nNew"); Assert.Equal("Hello\nNew world", e.Document.PlainText); Assert.Equal(2, e.Document.Paragraphs().Count()); }
    [Fact] public void JoinsParagraphsOnBackspace() { var e = Create("One\nTwo"); e.SetSelection(4, 4); e.DeleteBackward(); Assert.Equal("OneTwo", e.Document.PlainText); }
    [Fact] public void ReplacesAcrossParagraphs() { var e = Create("One\nTwo\nThree"); e.SetSelection(2, 9); e.InsertText("X"); Assert.Equal("OnXhree", e.Document.PlainText); }
    [Fact] public void UndoRedoRestoresContentAndSelection() { var e = Create(); e.SetSelection(0, 5); e.InsertText("Goodbye"); e.Undo(); Assert.Equal("Hello world", e.Document.PlainText); Assert.Equal(new TextSelection(0, 5), e.Selection); e.Redo(); Assert.Equal("Goodbye world", e.Document.PlainText); }
    [Fact] public void FailedTransactionRollsBack() { var e = Create(); Assert.Throws<InvalidOperationException>(() => e.Execute("broken", () => { e.Document.Title = "Wrong"; throw new InvalidOperationException(); })); Assert.Equal("Document1", e.Document.Title); Assert.False(e.CanUndo); }
    [Fact] public void FormattingSplitsRunsWithoutChangingText() { var e = Create(); e.SetSelection(0, 5); e.ToggleBold(); Assert.Equal("Hello world", e.Document.PlainText); var p = e.CurrentParagraph; Assert.True(p.Runs[0].Style.Bold); Assert.False(p.Runs[1].Style.Bold); Assert.Equal(2, p.Runs.Count); }
    [Fact] public void TypingStyleAppliesToNewText() { var e = Create(""); e.ToggleBold(); e.InsertText("Bold"); Assert.True(e.CurrentParagraph.Runs[0].Style.Bold); }
    [Fact] public void GraphemeBackspaceDoesNotSplitEmoji() { var e = Create("A👩‍💻B"); e.SetSelection(6, 6); e.DeleteBackward(); Assert.Equal("AB", e.Document.PlainText); }
    [Fact] public void CombiningCharactersDeleteTogether() { var e = Create("Ae\u0301B"); e.SetSelection(3, 3); e.DeleteBackward(); Assert.Equal("AB", e.Document.PlainText); }
    [Theory][InlineData(1,0)][InlineData(2,2)] public void SurrogateSelectionSnaps(int position, int expected) { var e = Create("😀"); e.SetSelection(position, position); Assert.Equal(expected, e.Selection.Anchor); }
    [Fact] public void ParagraphStyleAppliesToWholeParagraph() { var e = Create(); e.ApplyStyle("Heading 1"); Assert.Equal(1, e.CurrentParagraph.Format.OutlineLevel); Assert.Equal(20, e.CurrentParagraph.Runs[0].Style.FontSize); }
    [Fact] public void FindIsLiteralAndSupportsWholeWords() { var e = Create("cat caterpillar Cat cat."); Assert.Equal(4, e.Find("cat").Count); Assert.Equal(3, e.Find("cat", wholeWord: true).Count); Assert.Single(e.Find("cat.")); }
    [Fact] public void ReplaceAllIsOneUndoOperation() { var e = Create("one one one"); Assert.Equal(3, e.ReplaceAll("one", "two")); Assert.Equal("two two two", e.Document.PlainText); e.Undo(); Assert.Equal("one one one", e.Document.PlainText); Assert.False(e.CanUndo); }
    [Fact] public void CommentsMoveWithInsertion() { var e = Create(); e.SetSelection(6, 11); e.AddComment("Review"); e.SetSelection(0, 0); e.InsertText("New "); Assert.Equal(10, e.Document.Comments[0].Start); Assert.Equal(15, e.Document.Comments[0].End); }
    [Fact] public void TracksAndRejectsInsertion() { var e = Create(); e.TrackChanges = true; e.SetSelection(5, 5); e.InsertText("!"); Assert.Single(e.Document.Changes); e.RejectChange(e.Document.Changes[0].Id); Assert.Equal("Hello world", e.Document.PlainText); Assert.Empty(e.Document.Changes); }
    [Fact] public void RejectsUnsafeOverlappingRevision() { var e = Create(); e.TrackChanges = true; e.SetSelection(0, 0); e.InsertText("1234"); var id = e.Document.Changes[0].Id; e.SetSelection(2, 2); e.InsertText("X"); Assert.False(e.Document.Changes[0].CanReject); Assert.Throws<InvalidOperationException>(() => e.RejectChange(id)); }
    [Fact] public void TableInsertionAndCellNavigation() { var e = Create(""); e.InsertTable(2, 2); Assert.NotNull(e.CurrentTable); e.InsertText("A"); Assert.True(e.MoveTableCell(false)); e.InsertText("B"); Assert.Equal("B", e.CurrentParagraph.Text); Assert.Equal(2, e.CurrentTable!.Rows.Count); }
    [Fact] public void CrossCellReplaceIsExplicitlyRejected() { var e = Create(""); e.InsertTable(2, 2); var cells = e.Index.Paragraphs.Where(p => p.Table is not null).ToArray(); e.SetSelection(cells[0].Start, cells[1].Start); Assert.Throws<InvalidOperationException>(() => e.InsertText("x")); }
    [Fact] public void DeletesCurrentTable() { var e = Create("before"); e.SetSelection(6, 6); e.InsertTable(2, 3); e.DeleteTable(); Assert.Empty(e.Document.Blocks.OfType<TableBlock>()); Assert.Contains("before", e.Document.PlainText); }
    [Fact] public void PageBreakIsARealBlock() { var e = Create("hello"); e.SetSelection(2, 2); e.InsertPageBreak(); Assert.IsType<PageBreakBlock>(e.Document.Blocks[1]); Assert.Equal("he\nllo", e.Document.PlainText); }
    [Fact] public void NativeRoundTripPreservesAllModelData() { var d = SampleDocument.Create(); var clone = DocumentJson.Load(DocumentJson.Save(d)); Assert.Equal(d.PlainText, clone.PlainText); Assert.Equal(d.Comments[0].Text, clone.Comments[0].Text); Assert.Equal(d.Page, clone.Page); }
    [Fact] public void InvalidPageGeometryRejected() { var d = new DocumentModel { Page = new() { Width = double.NaN } }; Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(d)); }
    [Fact] public void ReadOnlyDoesNotMutate() { var e = Create(); e.IsReadOnly = true; Assert.Throws<InvalidOperationException>(() => e.InsertText("x")); Assert.Equal("Hello world", e.Document.PlainText); }
    [Fact] public void DirtyStateReturnsToSavedAfterUndo() { var e = Create(); Assert.False(e.IsDirty); e.InsertText("x"); Assert.True(e.IsDirty); e.Undo(); Assert.False(e.IsDirty); }
    [Fact] public void HtmlEscapesUntrustedText() { var d = DocumentJson.FromText("<script>alert(1)</script>"); var html = HtmlExporter.Export(d); Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html); }
}
