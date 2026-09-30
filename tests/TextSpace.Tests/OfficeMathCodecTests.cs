using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class OfficeMathCodecTests
{
    [Theory]
    [InlineData("blank")][InlineData("fraction")][InlineData("radical")][InlineData("superscript")]
    [InlineData("subsuperscript")][InlineData("matrix")][InlineData("sum")][InlineData("integral")]
    [InlineData("parentheses")][InlineData("vector")][InlineData("quadratic")][InlineData("pythagoras")][InlineData("identity")]
    public void SupportedStructuresRoundTripAsEditableOfficeMath(string template)
    {
        var root = EquationTemplates.Create(template);
        var xml = OfficeMathCodec.Write(root);
        var warnings = new List<string>(); var restored = OfficeMathCodec.Read(xml, warnings.Add);
        Assert.Equal(root.ToLinearText(), restored.ToLinearText()); Assert.Empty(warnings);
        EquationRules.Validate(restored);
    }
    [Fact]
    public void UnknownStructureWarnsAndRetainsVisibleText()
    {
        var m = OfficeMathCodec.Namespace;
        var xml = new XElement(m + "oMath", new XElement(m + "unknown", new XElement(m + "r", new XElement(m + "t", "not lost"))));
        var warnings = new List<string>();
        Assert.Equal("not lost", OfficeMathCodec.Read(xml, warnings.Add).ToLinearText()); Assert.Single(warnings);
    }
    [Fact]
    public void OverdeepXmlIsRejectedBeforeRecursion()
    {
        var m = OfficeMathCodec.Namespace; var root = new XElement(m + "oMath"); var current = root;
        for (var i = 0; i < 200; i++) { var next = new XElement(m + "e"); current.Add(next); current = next; }
        Assert.Throws<InvalidDataException>(() => OfficeMathCodec.Read(root));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void VisualDocxContainsSchemaValidEditableMathAndDrawing(bool floating)
    {
        var document = new DocumentModel { Blocks = [new Paragraph("Before"),
            new ShapeBlock { Kind = ShapeKind.TextBox, Text = "Editable shape", Placement = new() { Floating = floating, X = 12, Y = 8, Rotation = 25, FlipHorizontal = true } },
            new EquationBlock { Root = EquationTemplates.Create("quadratic"), Placement = new() { Floating = floating, X = 10, Y = 6 } }, new Paragraph("After")] };
        var bytes = new DocxWriter().Write(document);
        using var stream = new MemoryStream(bytes); using var package = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2010).Validate(package).Select(e => e.Path?.XPath + ": " + e.Description).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors));
        var restored = new DocxReader().Read(bytes).Document;
        Assert.Equal("Before\nAfter", restored.PlainText);
        var shape = Assert.Single(restored.Blocks.OfType<ShapeBlock>());
        Assert.Equal(ShapeKind.TextBox, shape.Kind); Assert.Equal("Editable shape", shape.Text);
        Assert.Equal(document.Blocks.OfType<ShapeBlock>().Single().Placement, shape.Placement);
        var equation = Assert.Single(restored.Blocks.OfType<EquationBlock>());
        Assert.Equal(document.Blocks.OfType<EquationBlock>().Single().Root.ToLinearText(), equation.Root.ToLinearText());
        Assert.Equal(floating, equation.Placement.Floating);
    }
    [Theory]
    [InlineData(ShapeKind.Rectangle)][InlineData(ShapeKind.RoundedRectangle)][InlineData(ShapeKind.Ellipse)]
    [InlineData(ShapeKind.Diamond)][InlineData(ShapeKind.Triangle)][InlineData(ShapeKind.Line)][InlineData(ShapeKind.Arrow)]
    public void ShapeGeometryAndFormattingRoundTrip(ShapeKind kind)
    {
        var shape = new ShapeBlock { Kind = kind, Fill = "#80AABBCC", Stroke = "#445566", StrokeWidth = 3, Width = 200, Height = 100, Text = "a\nb", Padding = 9 };
        var document = new DocumentModel { Blocks = [shape, new Paragraph()] };
        var imported = Assert.Single(new DocxReader().Read(new DocxWriter().Write(document)).Document.Blocks.OfType<ShapeBlock>());
        Assert.Equal(kind, imported.Kind); Assert.Equal(shape.Text, imported.Text); Assert.Equal(shape.Fill, imported.Fill);
        Assert.Equal(shape.Stroke, imported.Stroke); Assert.Equal(shape.StrokeWidth, imported.StrokeWidth); Assert.Equal(9, imported.Padding);
    }
    [Fact]
    public void EquationsInMergedTableCellsAreNotDropped()
    {
        var table = TableBlock.Create(1, 2); table.Rows[0].Cells[0].Blocks = [new EquationBlock { Root = EquationTemplates.Create("fraction") }, new Paragraph("caption")];
        var document = new DocumentModel { Blocks = [table, new Paragraph()] };
        var imported = new DocxReader().Read(new DocxWriter().Write(document)).Document;
        Assert.Single(imported.Blocks.OfType<TableBlock>().Single().Rows[0].Cells[0].Blocks.OfType<EquationBlock>());
        Assert.Contains("caption", imported.PlainText);
    }
    [Fact]
    public void InlineMathBetweenRunsRetainsSourceOrder()
    {
        var document = DocumentJson.FromText("left"); var bytes = new DocxWriter().Write(document);
        using var input = new MemoryStream(bytes); using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries)
            {
                using var to = target.CreateEntry(entry.FullName).Open(); using var from = entry.Open();
                if (entry.FullName != "word/document.xml") { from.CopyTo(to); continue; }
                var xml = XDocument.Load(from); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                xml.Descendants(w + "body").First().Elements(w + "p").First().Add(OfficeMathCodec.Write(EquationTemplates.Create("fraction")), new XElement(w + "r", new XElement(w + "t", "right")));
                xml.Save(to);
            }
        }
        var result = new DocxReader().Read(output.ToArray());
        Assert.Collection(result.Document.Blocks, b => Assert.Equal("left", Assert.IsType<Paragraph>(b).Text),
            b => Assert.IsType<EquationBlock>(b), b => Assert.Equal("right", Assert.IsType<Paragraph>(b).Text));
    }
}
