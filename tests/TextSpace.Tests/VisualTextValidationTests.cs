using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualTextValidationTests
{
    [Theory]
    [InlineData(0)][InlineData(11)][InlineData(0xD800)][InlineData(0xDC00)][InlineData(0xFFFE)][InlineData(0xFFFF)]
    public void ShapeTextRejectsUnexportableCharacters(int codePoint)
    {
        var shape = new ShapeBlock { Text = "before" + (char)codePoint + "after" };
        Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(new() { Blocks = [shape, new Paragraph()] }));
    }
    [Theory]
    [InlineData(0xD800)][InlineData(0xFFFE)][InlineData(0xFFFF)]
    public void EquationTextRejectsUnexportableCharacters(int codePoint)
    {
        var equation = new EquationBlock { Root = EquationNode.Leaf("x" + (char)codePoint) };
        Assert.Throws<InvalidDataException>(() => VisualBlockRules.Validate(equation));
    }
    [Fact]
    public void UnicodeAndMultilineShapeTextRoundTripUnchanged()
    {
        const string text = "A👩‍💻e\u0301\nB\tC\r\nD\u2028E";
        var document = new DocumentModel { Blocks = [new ShapeBlock { Text = text }, new Paragraph()] };
        var restored = DocumentJson.Load(DocumentJson.Save(document));
        Assert.Equal(text, restored.Blocks.OfType<ShapeBlock>().Single().Text);
    }
    [Fact]
    public void InvalidShapeStyleRollsBackTheDocumentTransaction()
    {
        var shape = new ShapeBlock { Text = "Preserved" };
        var editor = new EditorSession(new() { Blocks = [shape, new Paragraph()] });
        Assert.Throws<InvalidDataException>(() => editor.Execute("Invalid style", () => shape.TextStyle = shape.TextStyle with { FontFamily = "unsafe\0font" }));
        Assert.Equal("Preserved", editor.Document.Blocks.OfType<ShapeBlock>().Single().Text);
        Assert.DoesNotContain('\0', editor.Document.Blocks.OfType<ShapeBlock>().Single().TextStyle.FontFamily);
        Assert.False(editor.CanUndo);
    }
    [Fact]
    public void NullOrOversizedVisualTextIsNotSilentlyDropped()
    {
        Assert.False(VisualTextRules.IsValid(null, 10));
        Assert.False(VisualTextRules.IsValid("eleven", 5));
        Assert.False(VisualTextRules.IsValid("a\nb", 10, false));
        Assert.True(VisualTextRules.IsValid("a\nb", 10));
    }
}
