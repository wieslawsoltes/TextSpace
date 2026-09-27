using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Layout;
using TextSpace.Skia;
using Xunit;

namespace TextSpace.Tests;

public sealed class LayoutPerformanceTests
{
    private sealed class CountingMetrics : IVersionedTextMetrics
    {
        private readonly MonospaceTextMetrics _inner = new();
        public long MetricsVersion { get; set; }
        public int Measurements { get; private set; }
        public TextMeasurement Measure(string text, TextStyle style) { Measurements++; return _inner.Measure(text, style); }
        public double[] CaretPositions(string text, TextStyle style) => _inner.CaretPositions(text, style);
    }
    [Fact] public void UnchangedPaginationDoesNotRemeasureParagraphs()
    {
        var metrics = new CountingMetrics(); var engine = new PageLayoutEngine(metrics);
        var document = DocumentJson.FromText(string.Join("\n", Enumerable.Repeat("A paragraph of text.", 30)));
        engine.Layout(document); var measured = metrics.Measurements;
        engine.Layout(document);
        Assert.Equal(measured, metrics.Measurements); Assert.Equal(30, engine.ParagraphCache.Misses); Assert.Equal(30, engine.ParagraphCache.Hits);
    }
    [Fact] public void EditingOneParagraphInvalidatesOnlyThatParagraph()
    {
        var engine = new PageLayoutEngine(new CountingMetrics()); var document = DocumentJson.FromText("First\nSecond\nThird");
        engine.Layout(document); document.Paragraphs().ElementAt(1).Runs[0].Text = "Changed"; engine.Layout(document);
        Assert.Equal(4, engine.ParagraphCache.Misses); Assert.Equal(2, engine.ParagraphCache.Hits);
    }
    [Fact] public void CachedGeometryRebasesWithoutRemeasurement()
    {
        var metrics = new CountingMetrics(); var cache = new ParagraphLayoutCache(metrics); var paragraph = new Paragraph("ABCDE");
        var first = cache.Layout(paragraph, 200, 10); var measurements = metrics.Measurements; var second = cache.Layout(paragraph, 200, 110);
        Assert.Equal(measurements, metrics.Measurements); Assert.Equal(first[0].Start + 100, second[0].Start); Assert.Equal(first[0].End + 100, second[0].End);
        Assert.Equal(first[0].Chunks[0].Start + 100, second[0].Chunks[0].Start); Assert.Equal(first[0].Width, second[0].Width);
    }
    [Fact] public void ReturnedGeometryCannotPoisonCache()
    {
        var cache = new ParagraphLayoutCache(new CountingMetrics()); var paragraph = new Paragraph("ABCDE"); var first = cache.Layout(paragraph, 200, 0);
        var original = first[0].Chunks[0].X; first[0].X = 999; first[0].Chunks[0].X = 999; first[0].Chunks[0].Carets[0] = 999; first[0].Chunks.Clear();
        var second = cache.Layout(paragraph, 200, 0);
        Assert.NotEmpty(second[0].Chunks); Assert.Equal(original, second[0].Chunks[0].X); Assert.Equal(0, second[0].Chunks[0].Carets[0]); Assert.NotEqual(999, second[0].X);
    }
    [Fact] public void VersionedMetricsInvalidateCachedParagraphs()
    {
        var metrics = new CountingMetrics(); var cache = new ParagraphLayoutCache(metrics); var paragraph = new Paragraph("Text");
        cache.Layout(paragraph, 200, 0); var measured = metrics.Measurements; metrics.MetricsVersion++; cache.Layout(paragraph, 200, 0);
        Assert.True(metrics.Measurements > measured); Assert.Equal(2, cache.Misses); Assert.Equal(1, cache.CachedParagraphs);
    }
    [Fact] public void WidthAndCharacterFormattingInvalidateGeometry()
    {
        var cache = new ParagraphLayoutCache(new CountingMetrics()); var paragraph = new Paragraph("Text"); cache.Layout(paragraph, 200, 0); cache.Layout(paragraph, 100, 0);
        paragraph.Runs[0].Style = paragraph.Runs[0].Style with { FontSize = 24 }; cache.Layout(paragraph, 100, 0); Assert.Equal(3, cache.Misses);
    }
    [Fact] public void ParagraphAndEmptyDefaultFormattingInvalidateGeometry()
    {
        var cache = new ParagraphLayoutCache(new CountingMetrics()); var p = new Paragraph(); cache.Layout(p, 200, 0);
        p.Format = p.Format with { LeftIndent = 18 }; cache.Layout(p, 200, 0); p.DefaultStyle = p.DefaultStyle with { FontSize = 30 }; cache.Layout(p, 200, 0); Assert.Equal(3, cache.Misses);
    }
    [Fact] public void IdenticalIdDoesNotReuseDifferentContent()
    {
        var cache = new ParagraphLayoutCache(new CountingMetrics()); var p = new Paragraph("Original"); cache.Layout(p, 200, 0);
        var other = new Paragraph("Changed") { Id = p.Id }; Assert.Equal("Changed", cache.Layout(other, 200, 0)[0].Chunks[0].Text); Assert.Equal(2, cache.Misses);
    }
    [Fact] public void CacheHonorsEntryAndPayloadBudgets()
    {
        var cache = new ParagraphLayoutCache(new CountingMetrics(), 1, 64 * 1024); var first = new Paragraph("First"); var second = new Paragraph("Second");
        cache.Layout(first, 200, 0); cache.Layout(second, 200, 0); cache.Layout(first, 200, 0); Assert.Equal(1, cache.CachedParagraphs); Assert.Equal(3, cache.Misses); Assert.InRange(cache.EstimatedBytes, 0L, 64L * 1024);
        var disabled = new ParagraphLayoutCache(new CountingMetrics(), maximumBytes: 0); disabled.Layout(first, 200, 0); Assert.Equal(0, disabled.CachedParagraphs); Assert.Equal(0, disabled.EstimatedBytes);
    }
    [Fact] public void IndexedCaretPreservesOriginalSharedEndpointPolicy()
    {
        var document = SampleDocument.Create(); var layout = new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document); var lines = layout.Lines.ToArray();
        for (var position = -1; position <= document.PlainText.Length + 1; position++)
        {
            var expected = lines.LastOrDefault(l => position >= l.Start && position <= l.End) ?? lines[^1]; var actual = layout.Caret(position);
            Assert.Equal(expected.PageIndex, actual.PageIndex); Assert.Equal(expected.CaretX(position), actual.X, 10); Assert.Equal(expected.Y, actual.Y, 10); Assert.Equal(expected.Height, actual.Height, 10);
        }
    }
    [Fact] public void IndexedCaretHandlesOverlappingAndUnsortedTableIntervals()
    {
        var page = new LayoutPage(0, new());
        page.Lines.AddRange([new() { Start = 10, End = 40, X = 10 }, new() { Start = 0, End = 50, X = 20 }, new() { Start = 20, End = 25, X = 30 }]);
        var layout = new DocumentLayout(new(), [page]);
        Assert.Equal(20, layout.Caret(15).X); Assert.Equal(30, layout.Caret(20).X); Assert.Equal(20, layout.Caret(30).X); Assert.Equal(30, layout.Caret(100).X);
    }
    [Fact] public void NearestLinePreservesOriginalScoring()
    {
        var document = SampleDocument.Create(); var layout = new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document); var random = new Random(417);
        foreach (var page in layout.Pages)
            for (var i = 0; i < 100; i++)
            {
                var x = random.NextDouble() * page.Settings.Width; var y = random.NextDouble() * page.Settings.Height;
                var expected = page.Lines.OrderBy(l => (y < l.Y ? l.Y - y : y > l.Y + l.Height ? y - l.Y - l.Height : 0) * 10000 + (x < l.X ? l.X - x : x > l.X + l.Width ? x - l.X - l.Width : 0)).First();
                Assert.Equal(expected.HitTest(x), layout.HitTest(x + layout.PageLeft(page.Index), y + layout.PageTop(page.Index)));
            }
    }
    [Fact] public void PrecomputedLayoutPngMatchesConvenienceExport()
    {
        using var renderer = new DocumentRenderer(); var document = DocumentJson.FromText("Identical export"); var layout = renderer.Layout(document);
        Assert.Equal(renderer.ExportPng(document), renderer.ExportPng(document, layout));
    }
    [Fact] public void ClearMeasurementsAdvancesVersion()
    {
        using var metrics = new SkiaTextMetrics(); var initial = metrics.MetricsVersion; metrics.ClearMeasurements(); Assert.True(metrics.MetricsVersion > initial);
    }
}
