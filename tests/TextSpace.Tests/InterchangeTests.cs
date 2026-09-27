using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.OpenXml;
using TextSpace.Skia;
using Xunit;

namespace TextSpace.Tests;

public sealed class InterchangeTests
{
    [Fact] public void DocxRoundTripPreservesTextAndFormatting()
    {
        var d = SampleDocument.Create(); var data = new DocxWriter().Write(d); var result = new DocxReader().Read(data);
        Assert.Equal(d.PlainText, result.Document.PlainText); Assert.Equal(d.Page.Width, result.Document.Page.Width); Assert.Equal(d.Title, result.Document.Title); Assert.Equal(d.Comments[0].Text, result.Document.Comments[0].Text);
        Assert.Equal(32, result.Document.Paragraphs().First().Runs.First().Style.FontSize);
    }
    [Fact] public void ExportIsValidOpenXml()
    {
        using var stream = new MemoryStream(new DocxWriter().Write(SampleDocument.Create())); using var word = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(word).Select(e => e.Path?.XPath + ": " + e.Description).ToArray(); Assert.True(errors.Length == 0, string.Join("\n", errors));
    }
    [Fact] public void RejectsNonZipInput() => Assert.Throws<InvalidDataException>(() => new DocxReader().Read(Encoding.UTF8.GetBytes("not a document")));
    [Fact] public void RejectsExternalEntityXml()
    {
        using var output = new MemoryStream(); using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true)) { using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()); writer.Write("<!DOCTYPE foo [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><document>&xxe;</document>"); }
        Assert.ThrowsAny<Exception>(() => new DocxReader().Read(output.ToArray()));
    }
    [Fact] public void HeaderFooterFieldsRoundTrip() { var d = new DocumentModel { Header = "A header", Footer = "Page {PAGE} of {NUMPAGES}" }; var result = new DocxReader().Read(new DocxWriter().Write(d)); Assert.Equal(d.Footer, result.Document.Footer); Assert.Equal(d.Header, result.Document.Header); }
    [Fact] public void CommentsRetainAnchors() { var d = SampleDocument.Create(); var result = new DocxReader().Read(new DocxWriter().Write(d)).Document; Assert.Equal(d.Comments[0].Start, result.Comments[0].Start); Assert.Equal(d.Comments[0].End, result.Comments[0].End); }
    [Fact] public void ImagesExportAndReimport()
    {
        using var renderer = new DocumentRenderer(); var png = renderer.ExportPng(new DocumentModel()); var d = new DocumentModel { Blocks = [new ImageBlock { Data = png, Width = 100, Height = 140 }, new Paragraph("caption")] };
        var result = new DocxReader().Read(new DocxWriter().Write(d)); Assert.Equal(png, Assert.Single(result.Document.Blocks.OfType<ImageBlock>()).Data);
    }
    [Fact] public void SkiaProducesPngAndPdfArtifacts()
    {
        var d = SampleDocument.Create(); using var renderer = new DocumentRenderer(); var l = renderer.Layout(d); var png = renderer.ExportPng(d); var pdf = renderer.ExportPdf(d, l);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, png.Take(4)); Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf.Take(8).ToArray())); Assert.True(l.Pages.Count > 0);
        var root = Environment.GetEnvironmentVariable("TEXTSPACE_ARTIFACTS"); if (root is not null) { Directory.CreateDirectory(root); File.WriteAllBytes(Path.Combine(root, "sample.png"), png); File.WriteAllBytes(Path.Combine(root, "sample.pdf"), pdf); File.WriteAllBytes(Path.Combine(root, "sample.docx"), new DocxWriter().Write(d)); File.WriteAllText(Path.Combine(root, "sample.textspace"), DocumentJson.Save(d)); }
    }
}
