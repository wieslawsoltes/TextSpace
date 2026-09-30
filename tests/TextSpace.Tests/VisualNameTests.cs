using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualNameTests
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static VisualBlock Visual(int kind) => kind switch
    {
        0 => new ShapeBlock { Text = "Visible shape text" },
        1 => new EquationBlock { Root = EquationTemplates.Create("fraction") },
        _ => new ImageBlock { AltText = "Alternative image description", Data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aL1sAAAAASUVORK5CYII=") }
    };
    private static DocumentModel Document(VisualBlock visual) => new() { Blocks = [new Paragraph { Runs = [new("Main story", new())] }, visual] };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RenamePreservesIdentityContentGeometryAndIsOneUndoableTransaction(int kind)
    {
        var visual = Visual(kind); var session = new EditorSession(Document(visual)); session.SetSelection(2, 7);
        var before = DocumentJson.Save(session.Document); var selection = session.Selection;
        var changes = 0; session.Changed += (_, e) => { if (e.Kind == EditorChangeKind.Document) changes++; };
        session.RenameVisual(visual.Id, "Pump P-101 🧪");
        Assert.Same(visual, session.FindVisual(visual.Id)); Assert.Equal("Pump P-101 🧪", visual.Name);
        Assert.Equal(selection, session.Selection); Assert.Equal(1, session.Revision); Assert.Equal(1, session.UndoCount); Assert.Equal(1, changes);
        if (visual is ImageBlock image) Assert.Same(image.Data, Assert.IsType<ImageBlock>(session.FindVisual(visual.Id)).Data);
        session.Undo(); Assert.Equal(before, DocumentJson.Save(session.Document)); Assert.Equal(selection, session.Selection);
        session.Redo(); Assert.Equal("Pump P-101 🧪", session.FindVisual(visual.Id)!.Name);
    }

    [Fact]
    public void NoOpRenamePreservesRedoAndSavedState()
    {
        var visual = Visual(0); var session = new EditorSession(Document(visual)); session.RenameVisual(visual.Id, "Changed"); session.Undo(); session.MarkSaved();
        var revision = session.Revision; var count = session.RedoCount; var changed = 0; session.Changed += (_, _) => changed++;
        session.RenameVisual(visual.Id, "");
        Assert.Equal(revision, session.Revision); Assert.Equal(count, session.RedoCount); Assert.False(session.IsDirty); Assert.Equal(0, changed);
    }

    [Theory]
    [InlineData("bad\nname")] [InlineData("bad\tname")] [InlineData("bad\rname")] [InlineData("bad\0name")]
    [InlineData("bad\u2028name")] [InlineData("bad\u2029name")] [InlineData("bad\uFFFFname")]
    public void InvalidNamesNeverMutateDocumentOrHistory(string name)
    {
        var visual = Visual(0); var session = new EditorSession(Document(visual)); var before = DocumentJson.Save(session.Document);
        Assert.Throws<ArgumentException>(() => session.RenameVisual(visual.Id, name));
        Assert.Equal(before, DocumentJson.Save(session.Document)); Assert.Equal(0, session.UndoCount); Assert.Equal(0, session.Revision);
    }

    [Fact]
    public void NameLimitsAndMalformedSurrogatesAreRejected()
    {
        var visual = Visual(0); var session = new EditorSession(Document(visual));
        foreach (var invalid in new[] { new string('x', 257), new string('x', 255) + "🧪", "\uD800", "\uDC00" })
            Assert.Throws<ArgumentException>(() => session.RenameVisual(visual.Id, invalid));
        var valid = new string('x', 254) + "🧪"; session.RenameVisual(visual.Id, valid); Assert.Equal(valid, visual.Name);
        Assert.Throws<ArgumentNullException>(() => session.RenameVisual(visual.Id, null!));
    }

    [Fact]
    public void NamesAreNonUniqueAndCopiesRemainIndependent()
    {
        var visual = Visual(1); var session = new EditorSession(Document(visual)); session.RenameVisual(visual.Id, "Equation α");
        var id = session.DuplicateVisual(visual.Id); Assert.Equal("Equation α", session.FindVisual(id)!.Name);
        session.RenameVisual(id, "Equation β"); Assert.Equal("Equation α", session.FindVisual(visual.Id)!.Name);
        Assert.NotEqual(visual.Id, id); Assert.Equal("Equation α", BlockTree.CloneVisual(visual).Name);
    }

    [Fact]
    public void ReadOnlyMissingAndStaleDraftsCannotOverwriteNames()
    {
        var visual = Visual(0); var session = new EditorSession(Document(visual));
        using var draft = new VisualEditDraft(session, visual.Id);
        session.RenameVisual(visual.Id, "New name"); Assert.False(draft.IsCurrent);
        Assert.Throws<InvalidOperationException>(() => draft.Commit("Apply obsolete draft"));
        Assert.Throws<InvalidOperationException>(() => session.RenameVisual("missing", "Name"));
        session.IsReadOnly = true; Assert.Throws<InvalidOperationException>(() => session.RenameVisual(visual.Id, "Blocked"));
        Assert.Equal("New name", visual.Name);
    }

    [Fact]
    public void NamesInNestedTablesSurviveNativeSerialization()
    {
        var visual = Visual(0); visual.Name = "Nested object"; var table = TableBlock.Create(1, 1); table.Rows[0].Cells[0].Blocks.Add(visual);
        var document = new DocumentModel { Blocks = [table, new Paragraph()] }; var copy = DocumentJson.Clone(document);
        Assert.Equal(visual.Name, Assert.IsType<ShapeBlock>(BlockTree.Find(copy.Blocks, visual.Id)!.Value.Block).Name);
    }

    [Fact]
    public void LegacyNativeVisualWithoutNameRemainsLoadable()
    {
        var visual = Visual(0); var json = JsonNode.Parse(DocumentJson.Save(Document(visual)))!;
        foreach (var block in json["blocks"]!.AsArray()) block!.AsObject().Remove("name");
        Assert.Equal("", DocumentJson.Load(json.ToJsonString()).Blocks.OfType<VisualBlock>().Single().Name);
    }

    [Fact]
    public void RenamingDoesNotInvalidateShapeOrEquationTypography()
    {
        var shape = Assert.IsType<ShapeBlock>(Visual(0)); var equation = Assert.IsType<EquationBlock>(Visual(1));
        var cache = new VisualLayoutCache(new MonospaceTextMetrics()); var text = cache.GetShape(shape); var math = cache.GetEquation(equation);
        shape.Name = "Figure"; equation.Name = "Formula";
        Assert.Same(text, cache.GetShape(shape)); Assert.Same(math, cache.GetEquation(equation)); Assert.Equal(2, cache.Misses);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)] [InlineData(1, false)] [InlineData(1, true)] [InlineData(2, false)] [InlineData(2, true)]
    public void DocxUsesStandardObjectNameWithoutReplacingDescriptionOrContent(int kind, bool floating)
    {
        var visual = Visual(kind); visual.Name = "P-101 <inlet> & 🧪"; visual.Placement = new() { Floating = floating, X = 12, Y = 8 };
        var bytes = new DocxWriter().Write(Document(visual));
        using var stream = new MemoryStream(bytes); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var xmlStream = zip.GetEntry("word/document.xml")!.Open(); var xml = XDocument.Load(xmlStream);
        Assert.Equal(visual.Name, (string?)xml.Descendants(Wp + "docPr").Single().Attribute("name"));
        var imported = new DocxReader().Read(bytes).Document.Blocks.OfType<VisualBlock>().Single(); Assert.Equal(visual.Name, imported.Name);
        Assert.Equal(visual.Width, imported.Width); Assert.Equal(visual.Height, imported.Height); Assert.Equal(visual.Placement, imported.Placement);
        if (visual is ImageBlock image)
        {
            Assert.Equal(image.AltText, (string?)xml.Descendants(Wp + "docPr").Single().Attribute("descr"));
            Assert.Equal(image.AltText, Assert.IsType<ImageBlock>(imported).AltText); Assert.Equal(image.Data, ((ImageBlock)imported).Data);
        }
        if (visual is ShapeBlock shape) Assert.Equal(shape.Text, Assert.IsType<ShapeBlock>(imported).Text);
        if (visual is EquationBlock equation) Assert.Equal(equation.Root.ToLinearText(), Assert.IsType<EquationBlock>(imported).Root.ToLinearText());
    }

    [Fact]
    public void EditedStandardNameTakesPrecedenceAndLongImportsAreReported()
    {
        var visual = Visual(0); visual.Name = "Original";
        var bytes = new DocxWriter().Write(Document(visual));
        var renamed = new DocxReader().Read(RewriteName(bytes, "Externally renamed"));
        Assert.Equal("Externally renamed", renamed.Document.Blocks.OfType<VisualBlock>().Single().Name);
        var longName = new string('x', 255) + "🧪";
        var normalized = new DocxReader().Read(RewriteName(bytes, longName));
        Assert.Equal(new string('x', 255), normalized.Document.Blocks.OfType<VisualBlock>().Single().Name);
        Assert.Contains(normalized.Warnings, w => w.Contains("names were normalized", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, "")] [InlineData("A\nB", "A B")] [InlineData("A\u2028B", "A B")] [InlineData("A\u0085B", "A B")]
    [InlineData("Normal 🧪", "Normal 🧪")]
    public void ExternalNameNormalizationIsBoundedAndUnicodeSafe(string? original, string expected)
    {
        var result = VisualNameRules.NormalizeImported(original); Assert.Equal(expected, result); Assert.True(VisualNameRules.IsValid(result));
    }

    [Fact]
    public void HtmlRetainsEscapedNameSeparatelyFromAccessibleContent()
    {
        var visual = Assert.IsType<ShapeBlock>(Visual(0)); visual.Name = "<script> & label";
        var html = HtmlExporter.Export(Document(visual));
        Assert.Contains("data-textspace-name=\"&lt;script&gt; &amp; label\"", html);
        Assert.Contains("aria-label=\"Visible shape text\"", html); Assert.DoesNotContain("<script>", html);
    }

    private static byte[] RewriteName(byte[] bytes, string name)
    {
        using var input = new MemoryStream(bytes); using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var destination = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open(); using var to = destination.CreateEntry(entry.FullName).Open();
                if (entry.FullName != "word/document.xml") { from.CopyTo(to); continue; }
                var xml = XDocument.Load(from); xml.Descendants(Wp + "docPr").Single().SetAttributeValue("name", name); xml.Save(to);
            }
        return output.ToArray();
    }
}
