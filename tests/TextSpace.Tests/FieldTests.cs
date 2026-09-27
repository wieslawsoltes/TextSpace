using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class FieldTests
{
    private static FieldContext Context(DocumentModel document) => new() { PageAt = new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document).FieldPageAt };
    private static EditorSession New() => new(new DocumentModel { Title = "A real title", Author = "Test Author", Subject = "Subject", Created = new(2020, 1, 2, 0, 0, 0, TimeSpan.Zero) });

    [Theory] [InlineData("TITLE", "A real title")] [InlineData("AUTHOR", "Test Author")] [InlineData("SUBJECT", "Subject")]
    [InlineData("FILENAME", "A real title.docx")] [InlineData("CREATEDATE \\@ \"yyyy-MM-dd\"", "2020-01-02")]
    public void MetadataAndDateFieldsHaveLiveResults(string instruction, string expected)
    {
        var session = New(); session.InsertField(instruction, Context); Assert.Equal(expected, session.Document.PlainText); Assert.Single(session.Document.Fields);
    }
    [Fact] public void UpdatingTitlePreservesFieldAndUsesOneHistoryEntry()
    {
        var session = New(); session.InsertField("TITLE"); session.Execute("Rename", () => session.Document.Title = "Changed"); var count = session.UndoCount;
        session.UpdateFields(); Assert.Equal("Changed", session.Document.PlainText); Assert.Single(session.Document.Fields); Assert.Equal(count + 1, session.UndoCount);
        session.Undo(); Assert.Equal("A real title", session.Document.PlainText);
    }
    [Fact] public void InsertFieldIsAtomicAndRedoRestoresInstructions()
    {
        var session = New(); session.InsertField("TITLE"); Assert.Equal(1, session.UndoCount); session.Undo(); Assert.Empty(session.Document.Fields); Assert.Empty(session.Document.PlainText);
        session.Redo(); Assert.Equal("TITLE", Assert.Single(session.Document.Fields).Instruction);
    }
    [Fact] public void FieldLockAndUnlinkHaveDistinctSemantics()
    {
        var session = New(); var id = session.InsertField("TITLE"); session.LockField(id, true); session.Execute("Rename", () => session.Document.Title = "Other"); session.UpdateFields();
        Assert.Equal("A real title", session.Document.PlainText); session.LockField(id, false); session.UpdateFields(); Assert.Equal("Other", session.Document.PlainText);
        session.UnlinkField(id); Assert.Empty(session.Document.Fields); Assert.Equal("Other", session.Document.PlainText);
    }
    [Theory] [InlineData(0, 0, "prefix ", true)] [InlineData(12, 0, " suffix", true)] [InlineData(2, 0, "!", false)] [InlineData(0, 1, "!", false)]
    public void ManualEditingTransformsOrUnlinksFieldRanges(int start, int length, string text, bool retained)
    {
        var session = New(); session.InsertField("TITLE"); session.Replace(start, length, text);
        Assert.Equal(retained ? 1 : 0, session.Document.Fields.Count);
        if (retained) { var field = session.Document.Fields[0]; Assert.Equal("A real title", session.Index.Text.Substring(field.Start, field.End - field.Start)); }
    }
    [Fact] public void ReferencesFollowBookmarkTextAndRename()
    {
        var session = new EditorSession(DocumentJson.FromText("Source\n")); session.SetSelection(0, 6); session.SetBookmark("Target"); session.SetSelection(7, 7); session.InsertField("REF Target \\h");
        Assert.Equal("Source\nSource", session.Document.PlainText); session.RenameBookmark("Target", "Renamed"); Assert.Contains("Renamed", session.Document.Fields[0].Instruction);
        session.Replace(0, 6, "Updated"); session.UpdateFields(); Assert.Equal("Updated\nUpdated", session.Document.PlainText); Assert.Equal("#Renamed", session.Document.Paragraphs().Last().Runs[0].Style.Hyperlink);
    }
    [Fact] public void MissingReferenceIsAVisibleErrorNotAnException()
    {
        var session = New(); session.InsertField("REF Missing"); Assert.Contains("Reference source not found", session.Document.PlainText);
    }
    [Fact] public void CircularReferenceIsDetectedAndBounded()
    {
        var session = new EditorSession(DocumentJson.FromText("X")); session.SetSelection(0, 1); session.SetBookmark("Self"); session.SetSelection(0, 1); session.InsertField("REF Self");
        Assert.Contains("Circular", session.Document.PlainText); Assert.True(session.Document.PlainText.Length < 100);
    }
    [Fact] public void NumberingSequencesRestartRepeatAndSeparateIdentifiers()
    {
        var session = New();
        foreach (var code in new[] { "SEQ Figure", "SEQ Figure", "SEQ Table", "SEQ Figure \\r 10", "SEQ Figure \\c", "SEQ Figure \\* ROMAN" }) { session.InsertField(code); session.InsertText(" "); }
        Assert.Equal("1 2 1 10 10 XI ", session.Document.PlainText);
    }
    [Fact] public void PageFieldsUseSectionRestartAndFormatting()
    {
        var session = New(); session.InsertText("First"); session.InsertSectionBreak(); session.SetSection(s => s with { Options = s.Options with { PageNumberStart = 4, NumberStyle = PageNumberStyle.LowerRoman } });
        session.InsertField("PAGE", Context); session.InsertText("/"); session.InsertField("NUMPAGES", Context); session.InsertText("/"); session.InsertField("SECTION", Context); session.InsertText("/"); session.InsertField("SECTIONPAGES", Context);
        Assert.EndsWith("iv/2/2/1", session.Document.PlainText);
    }
    [Fact] public void PageReferenceUsesTheReferencedBookmarkPage()
    {
        var session = New(); session.InsertText("Here"); session.SetSelection(0, 4); session.SetBookmark("Target"); session.SetSelection(4, 4); session.InsertSectionBreak(); session.InsertField("PAGEREF Target \\h", Context);
        Assert.EndsWith("\n1", session.Document.PlainText);
    }
    [Fact] public void PageFieldsWithoutLayoutKeepTheirCachedResult()
    {
        var session = New(); session.InsertField("PAGE", Context); var result = Assert.Single(session.UpdateFields()); Assert.False(result.Evaluated); Assert.Equal("1", session.Document.PlainText);
    }
    [Theory] [InlineData("DDE cmd /c calc")] [InlineData("INCLUDETEXT \"https://example.invalid/private\"")] [InlineData("DATABASE")]
    public void ActiveExternalFieldInstructionsCannotBeCreated(string code)
    {
        var session = New(); Assert.Throws<ArgumentException>(() => session.InsertField(code)); Assert.Empty(session.Document.Fields); Assert.Empty(session.Document.PlainText);
    }
    [Fact] public void UnsupportedImportedFieldsArePreservedWithoutEvaluation()
    {
        var document = DocumentJson.FromText("cached"); document.Fields.Add(new() { Start = 0, End = 6, Instruction = "DDE cmd /c calc" });
        var session = new EditorSession(document); var result = Assert.Single(session.UpdateFields()); Assert.False(result.Evaluated); Assert.Equal("cached", session.Document.PlainText);
    }
    [Theory] [InlineData("DATE \\@ \"unclosed")] [InlineData("SEQ Item \\r -1")] [InlineData("REF")] [InlineData("PAGE\nDDE cmd")]
    public void MalformedInstructionsAreRejectedBeforeMutation(string code) => Assert.Throws<FormatException>(() => New().InsertField(code));
    [Fact] public void LiveFieldsCannotCrossParagraphsOrOverlap()
    {
        var document = DocumentJson.FromText("one\ntwo"); document.Fields.Add(new() { Start = 0, End = 7, Instruction = "TITLE" }); Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(document));
        document.Fields = [new() { Start = 0, End = 2, Instruction = "TITLE" }, new() { Start = 1, End = 3, Instruction = "AUTHOR" }]; Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(document));
    }
    [Fact] public void ReadOnlyModeRejectsFieldMutations()
    {
        var session = New(); var id = session.InsertField("TITLE"); session.IsReadOnly = true;
        Assert.Throws<InvalidOperationException>(() => session.UpdateFields()); Assert.Throws<InvalidOperationException>(() => session.UnlinkField(id)); Assert.Throws<InvalidOperationException>(() => session.LockField(id, true));
    }
    [Fact] public void StructuralInsertionKeepsFieldCoordinates()
    {
        var session = New(); session.InsertField("TITLE"); session.SetSelection(0, 0); session.InsertTable(1, 2);
        var field = Assert.Single(session.Document.Fields); Assert.Equal("A real title", session.Index.Text.Substring(field.Start, field.End - field.Start));
    }
    [Fact] public void FieldUpdatesPreserveSelectionDirection()
    {
        var session = New(); session.InsertField("TITLE"); session.InsertText(" tail"); session.SetSelection(session.Index.Length, session.Index.Length - 4);
        session.Execute("Rename", () => session.Document.Title = "Short"); session.UpdateFields(); Assert.Equal("tail", session.SelectedText()); Assert.True(session.Selection.Anchor > session.Selection.Active);
    }
    [Fact] public void FieldInstructionsAndLocksRoundTripThroughSchemaValidDocx()
    {
        var session = New(); var id = session.InsertField("TITLE"); session.LockField(id, true); session.InsertText(" "); session.InsertField("PAGE", Context);
        var bytes = new DocxWriter().Write(session.Document); using var stream = new MemoryStream(bytes); using var package = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(package).Select(e => e.Path?.XPath + ": " + e.Description).ToArray(); Assert.True(errors.Length == 0, string.Join("\n", errors));
        var copy = new DocxReader().Read(bytes).Document; Assert.Equal(session.Document.PlainText, copy.PlainText); Assert.Equal(2, copy.Fields.Count); Assert.True(copy.Fields[0].Locked); Assert.Equal("PAGE", copy.Fields[1].Instruction);
    }
    [Fact] public void FieldAndBookmarkBoundariesCanCoincideInDocx()
    {
        var session = New(); session.InsertField("TITLE"); session.SelectAll(); session.SetBookmark("TitleRange");
        var copy = new DocxReader().Read(new DocxWriter().Write(session.Document)).Document;
        Assert.Equal(copy.Fields[0].Start, copy.Bookmarks[0].Start); Assert.Equal(copy.Fields[0].End, copy.Bookmarks[0].End);
    }
    [Fact] public void ReaderAcceptsSimpleFieldsWithRichCachedResults()
    {
        var bytes = Package("<w:p><w:fldSimple w:instr=\"TITLE\" w:fldLock=\"1\"><w:r><w:rPr><w:b/></w:rPr><w:t>Cached</w:t></w:r></w:fldSimple></w:p>");
        var copy = new DocxReader().Read(bytes).Document; Assert.Equal("TITLE", Assert.Single(copy.Fields).Instruction); Assert.True(copy.Fields[0].Locked); Assert.True(copy.Paragraphs().First().Runs[0].Style.Bold);
    }
    [Fact] public void ReaderDoesNotReuseFieldsFromPreviousImport()
    {
        var reader = new DocxReader(); var first = reader.Read(Package("<w:p><w:fldSimple w:instr=\"TITLE\"><w:r><w:t>Cached</w:t></w:r></w:fldSimple></w:p>")).Document;
        var second = reader.Read(Package("<w:p><w:r><w:t>Ordinary</w:t></w:r></w:p>")).Document; Assert.Empty(second.Fields); Assert.Single(first.Fields);
    }
    [Fact] public void OldNativeDocumentsDefaultToEmptyFieldAndSectionExtensions()
    {
        var json = DocumentJson.Save(New().Document); var root = System.Text.Json.Nodes.JsonNode.Parse(json)!; root.AsObject().Remove("fields"); root.AsObject().Remove("sectionOptions");
        var copy = DocumentJson.Load(root.ToJsonString()); Assert.Empty(copy.Fields); Assert.NotNull(copy.SectionOptions);
    }
    private static byte[] Package(string body)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + body + "</w:body></w:document>");
        }
        return memory.ToArray();
    }
}
