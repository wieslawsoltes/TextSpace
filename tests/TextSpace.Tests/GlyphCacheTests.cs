using SkiaSharp;
using SkiaSharp.HarfBuzz;
using TextSpace.Core;
using TextSpace.Skia;
using Xunit;

namespace TextSpace.Tests;

public sealed class GlyphCacheTests
{
    private static SKSurface Surface() => SKSurface.Create(new SKImageInfo(600, 180));
    private static byte[] Pixels(SKSurface surface) { using var image = surface.Snapshot(); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray(); }

    [Fact] public void WarmDrawingReusesNativeGlyphBlobs()
    {
        using var metrics = new SkiaTextMetrics(); using var surface = Surface(); using var paint = new SKPaint(); var style = new TextStyle();
        metrics.Draw(surface.Canvas, "Reuse", 12, 40, style, paint); metrics.Draw(surface.Canvas, "Reuse", 24, 60, style, paint);
        Assert.Equal(1, metrics.CachedShapedRuns); Assert.Equal(1, metrics.ShapedCacheMisses); Assert.Equal(1, metrics.ShapedCacheHits);
    }
    [Fact] public void PaintOnlyFormattingDoesNotDuplicateGlyphResources()
    {
        using var metrics = new SkiaTextMetrics(); using var surface = Surface(); using var paint = new SKPaint(); var style = new TextStyle();
        metrics.Draw(surface.Canvas, "Reuse", 12, 40, style, paint);
        metrics.Draw(surface.Canvas, "Reuse", 12, 40, style with { Color = "#FF0000", Underline = true, Hyperlink = "#Target", Highlight = "#FFFF00" }, paint);
        Assert.Equal(1, metrics.CachedShapedRuns); Assert.Equal(1, metrics.ShapedCacheHits);
    }
    [Fact] public void SizeAndFaceStyleInvalidateGlyphIdentity()
    {
        using var metrics = new SkiaTextMetrics(); using var surface = Surface(); using var paint = new SKPaint(); var style = new TextStyle();
        foreach (var variant in new[] { style, style with { FontSize = 20 }, style with { Bold = true }, style with { Italic = true } }) metrics.Draw(surface.Canvas, "Reuse", 12, 40, variant, paint);
        Assert.Equal(4, metrics.CachedShapedRuns); Assert.Equal(4, metrics.ShapedCacheMisses);
    }
    [Fact] public void CacheEvictsAndReleasesByCountAndPayloadBudget()
    {
        using var metrics = new SkiaTextMetrics(1, 4096); using var surface = Surface(); using var paint = new SKPaint(); var style = new TextStyle();
        metrics.Draw(surface.Canvas, "A", 12, 40, style, paint); metrics.Draw(surface.Canvas, "B", 12, 40, style, paint); metrics.Draw(surface.Canvas, "A", 12, 40, style, paint);
        Assert.Equal(1, metrics.CachedShapedRuns); Assert.Equal(3, metrics.ShapedCacheMisses); Assert.InRange(metrics.EstimatedShapedBytes, 0L, 4096L);
        using var disabled = new SkiaTextMetrics(maximumShapedBytes: 0); disabled.Draw(surface.Canvas, "A", 12, 40, style, paint); Assert.Equal(0, disabled.CachedShapedRuns);
    }
    [Fact] public void FontMetricInvalidationReleasesNativeGlyphCache()
    {
        using var metrics = new SkiaTextMetrics(); using var surface = Surface(); using var paint = new SKPaint();
        metrics.Draw(surface.Canvas, "A", 12, 40, new(), paint); metrics.ClearMeasurements(); Assert.Equal(0, metrics.CachedShapedRuns); Assert.Equal(0, metrics.EstimatedShapedBytes);
        metrics.Draw(surface.Canvas, "A", 12, 40, new(), paint); Assert.Equal(2, metrics.ShapedCacheMisses);
    }
    [Theory]
    [InlineData("Office ffi typography")]
    [InlineData("A e\u0301 combining Ω")]
    [InlineData("مرحبا")]
    public void CachedAndUncachedShapingProduceIdenticalPixelsAtOrigin(string text)
    {
        using var metrics = new SkiaTextMetrics(); using var a = Surface(); using var b = Surface(); using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        var style = new TextStyle { FontSize = 24 }; a.Canvas.Clear(SKColors.White); b.Canvas.Clear(SKColors.White);
        metrics.Draw(a.Canvas, text, 0, 60, style, paint);
        b.Canvas.DrawShapedText(metrics.Shaper(style), text, 0, 60, SKTextAlign.Left, metrics.Font(style), paint);
        Assert.Equal(Pixels(a), Pixels(b));
        a.Canvas.Clear(SKColors.White); metrics.Draw(a.Canvas, text, 0, 60, style, paint); Assert.Equal(Pixels(a), Pixels(b));
    }
}
