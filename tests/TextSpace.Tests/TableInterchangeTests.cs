using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class TableInterchangeTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    [Fact] public void MergedCellsProduceSchemaValidDocxAndRoundtrip()
    {
        var (editor, table) = TableMergingTests.Create(); editor.MergeTableCells(0, 0, 2, 2);
        editor.SetCellVerticalAlignment(CellVerticalAlignment.Center); table.Rows[0].MinimumHeight = 55; table.Rows[0].AllowSplit = false;
        var bytes = new DocxWriter().Write(editor.Document);
        using var stream = new MemoryStream(bytes); using var docx = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(docx).Select(e => e.Description).ToArray(); Assert.True(errors.Length == 0, string.Join("\n", errors));
        var imported = new DocxReader().Read(bytes); var actual = imported.Document.Blocks.OfType<TableBlock>().First();
        Assert.Equal(editor.Document.PlainText, imported.Document.PlainText); Assert.Empty(imported.Warnings);
        Assert.Equal(2, actual.Rows[0].Cells[0].RowSpan); Assert.Equal(2, actual.Rows[0].Cells[0].ColumnSpan);
        Assert.Equal(CellVerticalAlignment.Center, actual.Rows[0].Cells[0].VerticalAlignment); Assert.Equal(55, actual.Rows[0].MinimumHeight); Assert.False(actual.Rows[0].AllowSplit);
    }
    [Fact] public void NestedMergedTablesAndBookmarksSurviveRoundtrip()
    {
        var (editor, table) = TableMergingTests.Create(2, 2); editor.MergeTableCells(0, 0, 1, 2);
        var nested = TableBlock.Create(2, 2); table.Rows[0].Cells[0].Blocks.Insert(1, nested);
        var paragraph = (Paragraph)nested.Rows[0].Cells[0].Blocks[0]; paragraph.Runs.Add(new("Inner"));
        var at = editor.Index.StartOf(paragraph); editor.SetSelection(at, at + 5); editor.SetBookmark("InnerTarget");
        var bytes = new DocxWriter().Write(editor.Document); var imported = new DocxReader().Read(bytes);
        Assert.Empty(imported.Warnings); Assert.Equal(editor.Document.PlainText, imported.Document.PlainText);
        var marker = Assert.Single(imported.Document.Bookmarks); Assert.Equal("Inner", imported.Document.PlainText[marker.Start..marker.End]);
        Assert.IsType<TableBlock>(imported.Document.Blocks.OfType<TableBlock>().First().Rows[0].Cells[0].Blocks[1]);
    }
    private static byte[] Mutate(byte[] bytes, Action<XDocument> mutate)
    {
        using var output = new MemoryStream(); output.Write(bytes); output.Position = 0;
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("word/document.xml")!; XDocument xml; using (var input = entry.Open()) xml = XDocument.Load(input);
            mutate(xml); entry.Delete(); using var stream = zip.CreateEntry("word/document.xml").Open(); xml.Save(stream);
        }
        return output.ToArray();
    }
    [Fact] public void MissingVmergeValueMeansContinuation()
    {
        var (editor, _) = TableMergingTests.Create(); editor.MergeTableCells(0, 0, 2, 1);
        var bytes = Mutate(new DocxWriter().Write(editor.Document), xml => xml.Descendants(W + "vMerge").Last().Attribute(W + "val")!.Remove());
        var imported = new DocxReader().Read(bytes); Assert.Empty(imported.Warnings);
        Assert.Equal(2, imported.Document.Blocks.OfType<TableBlock>().First().Rows[0].Cells[0].RowSpan);
    }
    [Fact] public void NonemptyContinuationIsPreservedWithWarning()
    {
        var (editor, _) = TableMergingTests.Create(); editor.MergeTableCells(0, 0, 2, 1);
        var bytes = Mutate(new DocxWriter().Write(editor.Document), xml =>
        {
            var cell = xml.Descendants(W + "vMerge").Last().Parent!.Parent!;
            cell.Element(W + "p")!.Add(new XElement(W + "r", new XElement(W + "t", "Keep me")));
        });
        var imported = new DocxReader().Read(bytes); Assert.Contains("Keep me", imported.Document.PlainText);
        Assert.Contains(imported.Warnings, w => w.Contains("continuation"));
    }
    [Theory][InlineData("0")][InlineData("-1")][InlineData("999999999999")][InlineData("21")]
    public void MaliciousGridSpansAreRejectedBeforeAllocation(string span)
    {
        var (editor, _) = TableMergingTests.Create();
        var bytes = Mutate(new DocxWriter().Write(editor.Document), xml => xml.Descendants(W + "tcPr").First().Add(new XElement(W + "gridSpan", new XAttribute(W + "val", span))));
        Assert.Throws<InvalidDataException>(() => new DocxReader().Read(bytes));
    }
    [Fact] public void RepeatingHeaderFlagRespectsItsExplicitOffValue()
    {
        var (editor, table) = TableMergingTests.Create(); table.RepeatHeaderRow = false;
        var imported = new DocxReader().Read(new DocxWriter().Write(editor.Document));
        Assert.False(imported.Document.Blocks.OfType<TableBlock>().First().RepeatHeaderRow);
    }
}
