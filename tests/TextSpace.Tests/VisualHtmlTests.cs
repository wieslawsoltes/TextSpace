using TextSpace.Core;
using TextSpace.Documents;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualHtmlTests
{
    [Fact]
    public void ShapesAndEquationStructureArePresentAndEscaped()
    {
        var document = new DocumentModel { Blocks = [new ShapeBlock { Text = "<script>alert('x')</script> & text", Placement = new() { Rotation = 45, FlipHorizontal = true } },
            new EquationBlock { Root = EquationTemplates.Create("fraction", EquationNode.Leaf("a < b")) }, new Paragraph()] };
        var html = HtmlExporter.Export(document);
        Assert.Contains("<svg", html); Assert.Contains("<math", html); Assert.Contains("<mfrac", html);
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("a &lt; b", html); Assert.DoesNotContain("<script>", html);
        Assert.Contains("rotate(45deg)", html); Assert.Contains("scale(-1,1)", html); Assert.Contains("default-src 'none'", html);
    }
    [Theory]
    [InlineData("matrix", "mtable")][InlineData("radical", "msqrt")][InlineData("superscript", "msup")]
    [InlineData("subscript", "msub")][InlineData("subsuperscript", "msubsup")][InlineData("integral", "munderover")]
    public void MathematicalStructureIsNotFlattened(string template, string element)
    {
        var html = HtmlExporter.Export(new() { Blocks = [new EquationBlock { Root = EquationTemplates.Create(template) }, new Paragraph()] });
        Assert.Contains("<" + element, html);
    }
}
