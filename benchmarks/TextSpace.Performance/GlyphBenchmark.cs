using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using TextSpace.Core;
using TextSpace.Skia;

internal static class GlyphBenchmark
{
    public static string Run()
    {
        using var metrics = new SkiaTextMetrics(); using var surface = SKSurface.Create(new SKImageInfo(600, 200));
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.Black };
        var style = new TextStyle { FontSize = 11 };
        var words = Enumerable.Range(0, 100).Select(i => $"Reusable typography {i}").ToArray();
        var font = metrics.Font(style); var shaper = metrics.Shaper(style);
        object Measure(Action operation)
        {
            for (var i = 0; i < 3; i++) operation();
            var times = new List<double>(); var allocations = new List<long>();
            for (var i = 0; i < 7; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
                operation(); watch.Stop(); times.Add(watch.Elapsed.TotalMilliseconds); allocations.Add(GC.GetAllocatedBytesForCurrentThread() - before);
            }
            times.Sort(); allocations.Sort(); return new { medianMs = times[3], medianAllocatedBytes = allocations[3], iterations = 7 };
        }
        var original = Measure(() => { surface.Canvas.Clear(SKColors.White); for (var i = 0; i < 1000; i++) surface.Canvas.DrawShapedText(shaper, words[i % words.Length], 0, 40 + i % 5 * 20, SKTextAlign.Left, font, paint); });
        var cached = Measure(() => { surface.Canvas.Clear(SKColors.White); for (var i = 0; i < 1000; i++) metrics.Draw(surface.Canvas, words[i % words.Length], 0, 40 + i % 5 * 20, style, paint); });
        return JsonSerializer.Serialize(new { framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            drawCallsPerIteration = 1000, uniqueTextRuns = words.Length, original, cached, cacheEntries = metrics.CachedShapedRuns, estimatedCacheBytes = metrics.EstimatedShapedBytes }, new JsonSerializerOptions { WriteIndented = true });
    }
}
