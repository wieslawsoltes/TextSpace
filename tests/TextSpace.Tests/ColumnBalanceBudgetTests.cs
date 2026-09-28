using TextSpace.Core;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class ColumnBalanceBudgetTests
{
    [Fact]
    public void OversizedBandStopsSpeculativeMeasurementBeforeItsEnd()
    {
        var paragraphs = Enumerable.Range(0, 100)
            .Select(i => new Paragraph("Line " + i, new() { FontSize = 100 },
                new() { SpaceAfter = 0, LineSpacing = 1 })).ToArray();
        var tail = new Paragraph("Tail");
        var document = new DocumentModel
        {
            Page = new() { Columns = 2 },
            Blocks = [.. paragraphs,
                new SectionBreakBlock { Kind = SectionBreakKind.Continuous,
                    Section = new() { Page = new() } }, tail]
        };
        var engine = new PageLayoutEngine(new MonospaceTextMetrics());
        var layout = engine.Layout(document);

        Assert.False(layout.Pages[0].Regions[0].Balanced);
        Assert.Equal(101L, engine.ParagraphCache.Misses);
        // Only the initial, speculatively measured paragraphs are cache hits.
        // Measuring all 100 up front would not obey early rejection.
        Assert.InRange(engine.ParagraphCache.Hits, 1L, 20L);
        Assert.Equal(document.Paragraphs().Sum(p => p.Length),
            layout.Lines.SelectMany(l => l.Chunks).Sum(c => c.Text.Length));
    }
}
