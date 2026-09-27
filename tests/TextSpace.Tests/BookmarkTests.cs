using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class BookmarkTests
{
    private static EditorSession Editor(string text = "Hello world") => new(DocumentJson.FromText(text));
    [Fact] public void NamedRangesAreCaseInsensitiveAndCanBeMoved()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Target"); e.SetSelection(0, 5); e.SetBookmark("target");
        var b = Assert.Single(e.Document.Bookmarks); Assert.Equal("Target", b.Name); Assert.Equal(0, b.Start); Assert.Equal(5, b.End);
        e.SetSelection(11, 11); Assert.True(e.GoToBookmark("TARGET")); Assert.Equal("Hello", e.SelectedText());
    }
    [Theory][InlineData("")][InlineData("1Start")][InlineData("two words")][InlineData("a-b")][InlineData("a#b")][InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNO")]
    public void InvalidNamesAreRejectedWithoutHistory(string name)
    {
        var e = Editor(); Assert.Throws<ArgumentException>(() => e.SetBookmark(name)); Assert.Empty(e.Document.Bookmarks); Assert.False(e.CanUndo);
    }
    [Fact] public void InsertingBeforeRangePreservesItsText()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Target"); e.SetSelection(0, 0); e.InsertText("New ");
        Assert.True(e.GoToBookmark("Target")); Assert.Equal("world", e.SelectedText());
    }
    [Fact] public void InsertingInsideRangeExpandsIt()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Target"); e.SetSelection(8, 8); e.InsertText("NEW");
        e.GoToBookmark("Target"); Assert.Equal("woNEWrld", e.SelectedText());
    }
    [Fact] public void PointBookmarkHasRightGravityAndStaysCollapsed()
    {
        var e = Editor(); e.SetSelection(5, 5); e.SetBookmark("Point"); e.InsertText("!!");
        var b = Assert.Single(e.Document.Bookmarks); Assert.Equal(7, b.Start); Assert.Equal(b.Start, b.End);
    }
    [Fact] public void CombiningInsertionDoesNotInvalidateBookmark()
    {
        var e = Editor("AB"); e.SetSelection(0, 1); e.SetBookmark("Letter"); e.SetSelection(1, 1); e.InsertText("\u0301");
        e.GoToBookmark("Letter"); Assert.Equal("A\u0301", e.SelectedText());
    }
    [Fact] public void DeleteContainingRangeCollapsesBookmarkAndUndoRestoresIt()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Target"); e.SelectAll(); e.InsertText("");
        Assert.Equal(0, e.Document.Bookmarks[0].Start); Assert.Equal(0, e.Document.Bookmarks[0].End);
        e.Undo(); e.GoToBookmark("Target"); Assert.Equal("world", e.SelectedText()); e.Redo(); Assert.Equal("", e.Document.PlainText);
    }
    [Fact] public void StructuralTableChangesPreserveBookmarkByParagraphIdentity()
    {
        var e = Editor(""); e.InsertTable(2, 2); e.InsertText("left"); e.MoveTableCell(false); e.InsertText("target");
        var target = e.Index.At(e.Selection.Active); e.SetSelection(target.Start, target.End); e.SetBookmark("Cell");
        e.AddTableColumnBefore(); e.GoToBookmark("Cell"); Assert.Equal("target", e.SelectedText());
        e.AddTableRowAbove(); e.GoToBookmark("Cell"); Assert.Equal("target", e.SelectedText()); e.DeleteTableColumn();
        var b = e.Document.Bookmarks[0]; Assert.InRange(b.Start, 0, e.Index.Length); Assert.InRange(b.End, b.Start, e.Index.Length);
        e.Undo(); e.GoToBookmark("Cell"); Assert.Equal("target", e.SelectedText());
    }
    [Fact] public void RenameUpdatesInternalHyperlinksAtomically()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Old"); e.SetSelection(0, 5); e.FormatText("Link", s => s with { Hyperlink = "#old" });
        e.RenameBookmark("OLD", "New"); Assert.Equal("#New", e.Document.Paragraphs().First().Runs[0].Style.Hyperlink); Assert.NotNull(e.FindBookmark("New"));
        e.Undo(); Assert.NotNull(e.FindBookmark("Old")); Assert.Equal("#old", e.Document.Paragraphs().First().Runs[0].Style.Hyperlink);
    }
    [Fact] public void DuplicateRenameRollsBack()
    {
        var e = Editor(); e.SetBookmark("A"); e.SetBookmark("B"); Assert.Throws<InvalidOperationException>(() => e.RenameBookmark("A", "b"));
        Assert.NotNull(e.FindBookmark("A")); Assert.NotNull(e.FindBookmark("B"));
    }
    [Fact] public void ReadingModeAllowsNavigationButNotMutation()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Target"); e.SetSelection(0, 0); e.IsReadOnly = true;
        Assert.True(e.GoToBookmark("Target")); Assert.Equal("world", e.SelectedText()); Assert.Throws<InvalidOperationException>(() => e.DeleteBookmark("Target"));
    }
    [Fact] public void NativeRoundTripRetainsBookmarksAndLegacyFilesRemainReadable()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Target"); var saved = DocumentJson.Save(e.Document);
        Assert.Equal("Target", Assert.Single(DocumentJson.Load(saved).Bookmarks).Name);
        var legacy = JsonNode.Parse(saved)!.AsObject(); legacy.Remove("bookmarks"); Assert.Empty(DocumentJson.Load(legacy.ToJsonString()).Bookmarks);
    }
    [Fact] public void InvalidNativeRangesAndDuplicateNamesAreRejected()
    {
        var d = DocumentJson.FromText("😀"); d.Bookmarks.Add(new() { Name = "Invalid", Start = 1, End = 2 });
        Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(d)); d.Bookmarks[0].Start = 0; d.Bookmarks.Add(new() { Name = "invalid", Start = 0, End = 2 });
        Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(d));
    }
    [Fact] public void DocxBookmarksAndHyperlinksRoundTripAcrossRunsAndParagraphs()
    {
        var e = Editor("Alpha\nBeta\nGamma"); e.SetSelection(2, 13); e.SetBookmark("Section");
        e.SetSelection(0, 0); e.SetBookmark("_Start"); e.SetSelection(e.Index.Length, e.Index.Length); e.SetBookmark("End");
        e.SetSelection(0, 2); e.ToggleBold(); e.SetSelection(6, 10); e.FormatText("Link", s => s with { Hyperlink = "#Section" });
        var bytes = new DocxWriter().Write(e.Document); using var word = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var errors = new OpenXmlValidator().Validate(word).Select(x => x.Description).ToArray(); Assert.True(errors.Length == 0, string.Join("\n", errors));
        var result = new DocxReader().Read(bytes); Assert.Equal(e.Document.PlainText, result.Document.PlainText); Assert.Equal(3, result.Document.Bookmarks.Count);
        foreach (var original in e.Document.Bookmarks)
        {
            var restored = result.Document.Bookmarks.Single(b => b.Name == original.Name); Assert.Equal(original.Start, restored.Start); Assert.Equal(original.End, restored.End);
        }
        Assert.Contains(result.Document.Paragraphs().SelectMany(p => p.Runs), r => r.Style.Hyperlink == "#Section");
    }
    [Fact] public void DocxPointsAtEveryRunBoundaryEmitExactlyOneMarkerPair()
    {
        var d = DocumentJson.FromText("ABCD"); d.Paragraphs().Single().Runs = [new("AB", new() { Bold = true }), new("CD")];
        for (var i = 0; i <= 4; i++) d.Bookmarks.Add(new() { Name = "Point" + i, Start = i, End = i });
        using var zip = new ZipArchive(new MemoryStream(new DocxWriter().Write(d))); using var stream = zip.GetEntry("word/document.xml")!.Open(); var xml = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        Assert.Equal(5, xml.Descendants(w + "bookmarkStart").Count()); Assert.Equal(5, xml.Descendants(w + "bookmarkEnd").Count());
    }
    [Fact] public void HtmlExportsWorkingAnchorsAndSafeExternalLinks()
    {
        var e = Editor(); e.SetSelection(6, 11); e.SetBookmark("Target"); e.SetSelection(0, 5);
        e.FormatText("Link", s => s with { Hyperlink = "#target", Underline = true, StrikeThrough = true }); var html = HtmlExporter.Export(e.Document);
        Assert.Contains("id=\"Target\"", html); Assert.Contains("href=\"#Target\"", html); Assert.Contains("underline line-through", html);
        e.FormatText("External", s => s with { Hyperlink = "https://example.org/?a=1&b=2" }); html = HtmlExporter.Export(e.Document);
        Assert.Contains("href=\"https://example.org/?a=1&amp;b=2\"", html); Assert.Contains("noopener noreferrer", html);
    }
    [Fact] public void HtmlDoesNotExecuteUntrustedLinksOrFontCss()
    {
        var d = DocumentJson.FromText("Safe text"); var r = d.Paragraphs().Single().Runs[0];
        r.Style = r.Style with { Hyperlink = "javascript:alert(1)", FontFamily = "X';background:url(https://attacker.invalid);'", Color = "red;background:url(https://attacker.invalid)" };
        var html = HtmlExporter.Export(d); Assert.DoesNotContain("href=\"javascript:", html); Assert.Contains("default-src 'none'", html); Assert.Contains("\\27 ", html); Assert.Contains("color:#202020", html);
    }
    [Fact] public void TextIndexPreservesGraphemeAndParagraphBoundaryRules()
    {
        var d = DocumentJson.FromText("A😀B\n\ne\u0301\nLast"); var index = new TextIndex(d);
        for (var position = 0; position <= index.Length; position++)
        {
            var expected = index.Paragraphs.First(p => position <= p.End); Assert.Same(expected.Paragraph, index.At(position).Paragraph); Assert.Equal(expected.Start, index.StartOf(expected.Paragraph));
        }
        Assert.Equal(1, index.Snap(2)); Assert.Equal(3, index.Snap(2, true)); Assert.Equal(index.Length, index.Next(int.MaxValue)); Assert.Equal(0, index.Previous(int.MinValue));
    }
}
