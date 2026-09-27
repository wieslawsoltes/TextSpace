using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class ContentsTests
{
    private static FieldContext Context(DocumentModel d) => new() { PageAt = new PageLayoutEngine(new MonospaceTextMetrics()).Layout(d).FieldPageAt };
    private static EditorSession New() => new(new DocumentModel
    {
        Blocks = [new Paragraph(), new Paragraph("First chapter", format: new() { OutlineLevel = 1, StyleName = "Heading 1" }),
            new Paragraph("Body"), new SectionBreakBlock { Section = new() { Options = new() { PageNumberStart = 10 } } },
            new Paragraph("Second chapter", format: new() { OutlineLevel = 2, StyleName = "Heading 2" })]
    });
    [Fact] public void ContentsHaveLiveLinkedEntriesAndSectionPageNumbers()
    {
        var session = New(); Assert.Equal(2, session.InsertTableOfContents(contextFactory: Context));
        Assert.Equal(4, session.Document.Fields.Count); Assert.Equal(2, session.Document.Bookmarks.Count);
        Assert.StartsWith("Contents\nFirst chapter\t1\nSecond chapter\t10\n", session.Document.PlainText);
        Assert.Equal(2, session.Document.Paragraphs().Count(p => p.Format.OutlineLevel > 0));
        foreach (var field in session.Document.Fields)
        {
            var p = session.Index.At(field.Start); Assert.StartsWith("#_TextSpaceToc_", p.Paragraph.StyleAt(field.Start - p.Start).Hyperlink);
        }
    }
    [Fact] public void ContentsInsertionIsOneUndoEntry()
    {
        var session = New(); var original = DocumentJson.Save(session.Document); session.InsertTableOfContents(contextFactory: Context);
        Assert.Equal(1, session.UndoCount); session.Undo(); Assert.Equal(original, DocumentJson.Save(session.Document));
        session.Redo(); Assert.Equal(4, session.Document.Fields.Count);
    }
    [Fact] public void ContentsRefreshesHeadingTextWithoutRebuilding()
    {
        var session = New(); session.InsertTableOfContents(contextFactory: Context);
        var heading = session.Index.Paragraphs.First(p => p.Paragraph.Format.OutlineLevel > 0);
        session.Replace(heading.Start + 2, 1, "X"); session.UpdateFields(Context);
        Assert.StartsWith("Contents\nFiXst chapter\t1", session.Document.PlainText);
    }
    [Fact] public void UpdateTableReplacesTheExistingContentsBlock()
    {
        var session = New(); session.InsertTableOfContents(contextFactory: Context);
        session.SetSelection(session.Index.Length, session.Index.Length);
        session.InsertTableOfContents(true, Context);
        Assert.Single(session.Document.Paragraphs(), p => p.Format.StyleName == "TOCHeading");
        Assert.Equal(4, session.Document.Fields.Count); Assert.Equal(2, session.Document.Paragraphs().Count(p => p.Format.OutlineLevel > 0));
    }
    [Fact] public void ContentsCanBeInsertedImmediatelyBeforeAHeading()
    {
        var session = New(); var heading = session.Index.Paragraphs.First(p => p.Paragraph.Format.OutlineLevel > 0);
        session.SetSelection(heading.Start, heading.Start); session.InsertTableOfContents(contextFactory: Context);
        Assert.Equal(2, session.Document.Paragraphs().Count(p => p.Format.OutlineLevel > 0));
        Assert.DoesNotContain("Circular", session.Document.PlainText);
    }
    [Fact] public void ContentsRejectsAnInsertionInsideItsSourceHeading()
    {
        var session = New(); var h = session.Index.Paragraphs.First(p => p.Paragraph.Format.OutlineLevel > 0); session.SetSelection(h.Start + 2, h.Start + 2);
        var original = DocumentJson.Save(session.Document);
        Assert.Throws<InvalidOperationException>(() => session.InsertTableOfContents(contextFactory: Context)); Assert.Equal(original, DocumentJson.Save(session.Document));
    }
    [Fact] public void ContentsRoundTripsAsSchemaValidLinkedFields()
    {
        var session = New(); session.InsertTableOfContents(contextFactory: Context); var bytes = new DocxWriter().Write(session.Document);
        using var stream = new MemoryStream(bytes); using var package = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(package));
        var copy = new DocxReader().Read(bytes).Document; Assert.Equal(session.Document.PlainText, copy.PlainText); Assert.Equal(4, copy.Fields.Count);
        Assert.Equal(2, copy.Bookmarks.Count);
    }
}
