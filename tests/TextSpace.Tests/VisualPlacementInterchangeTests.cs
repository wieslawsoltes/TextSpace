using System.IO.Compression;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualPlacementInterchangeTests
{
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static byte[] Rewrite(byte[] bytes, Action<XDocument> edit)
    {
        using var sourceBytes = new MemoryStream(bytes); using var output = new MemoryStream();
        using (var source = new ZipArchive(sourceBytes, ZipArchiveMode.Read))
        using (var destination = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in source.Entries)
            {
                using var input = entry.Open(); using var target = destination.CreateEntry(entry.FullName).Open();
                if (entry.FullName != "word/document.xml") { input.CopyTo(target); continue; }
                var document = XDocument.Load(input); edit(document); document.Save(target);
            }
        }
        return output.ToArray();
    }
    private static DocumentModel Document(bool floating = true) => new()
    {
        Blocks = [new ShapeBlock { Width = 240, Height = 100, Alignment = TextAlignment.Center,
            Placement = new() { Floating = floating, X = 12, Y = 8 }, Text = "editable" }, new Paragraph()]
    };
    [Fact]
    public void UnchangedStandardGeometryRetainsNativeAnchorOffsets()
    {
        var result = new DocxReader().Read(new DocxWriter().Write(Document())).Document.Blocks.OfType<ShapeBlock>().Single();
        Assert.Equal(12, result.Placement.X); Assert.Equal(8, result.Placement.Y); Assert.Equal(TextAlignment.Center, result.Alignment);
    }
    [Fact]
    public void WidthChangedByExternalEditorInvalidatesNativeAlignmentHint()
    {
        var bytes = Rewrite(new DocxWriter().Write(Document()), xml => xml.Descendants(Wp + "extent").First().SetAttributeValue("cx", 280L * 12700));
        var shape = new DocxReader().Read(bytes).Document.Blocks.OfType<ShapeBlock>().Single();
        Assert.Equal(280, shape.Width); Assert.Equal(126, shape.Placement.X); Assert.Equal(TextAlignment.Left, shape.Alignment);
    }
    [Fact]
    public void ChangedStandardOffsetTakesPrecedence()
    {
        var bytes = Rewrite(new DocxWriter().Write(Document()), xml => xml.Descendants(Wp + "positionH").First().Element(Wp + "posOffset")!.Value = (180 * 12700).ToString());
        var shape = new DocxReader().Read(bytes).Document.Blocks.OfType<ShapeBlock>().Single();
        Assert.Equal(180, shape.Placement.X); Assert.Equal(TextAlignment.Left, shape.Alignment);
    }
    [Fact]
    public void ChangedCoordinateSpaceDoesNotReuseNativeOffset()
    {
        var bytes = Rewrite(new DocxWriter().Write(Document()), xml => xml.Descendants(Wp + "positionH").First().SetAttributeValue("relativeFrom", "page"));
        var result = new DocxReader().Read(bytes); var shape = result.Document.Blocks.OfType<ShapeBlock>().Single();
        Assert.Equal(126, shape.Placement.X); Assert.Equal(TextAlignment.Left, shape.Alignment);
        Assert.Contains(result.Warnings, w => w.Contains("coordinate", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void InlineAlignmentChangedByExternalEditorIsPreserved()
    {
        var bytes = Rewrite(new DocxWriter().Write(Document(false)), xml => xml.Descendants(W + "pPr").First().Element(W + "jc")!.SetAttributeValue(W + "val", "right"));
        var shape = new DocxReader().Read(bytes).Document.Blocks.OfType<ShapeBlock>().Single();
        Assert.False(shape.Placement.Floating); Assert.Equal(TextAlignment.Right, shape.Alignment); Assert.Equal(12, shape.Placement.X);
    }
}
