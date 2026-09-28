using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using TextSpace.Core;
using TextSpace.Layout;

internal static class InteractionBenchmark
{
    public static string Run()
    {
        var document = new DocumentModel { Blocks = Enumerable.Range(0, 500).Select(i => (Block)new Paragraph($"Paragraph {i}: steady scrolling should not reconstruct the entire document's text or materialize word matches on every frame.")).ToList() };
        var pages = Enumerable.Range(0, 10000).Select(i => new LayoutPage(i, new() { Height = 612 + i % 3 * 90 })).ToArray();
        var layout = new DocumentLayout(new(), pages);
        var snapshot = new DocumentTextSnapshot(document);
        var random = new Random(89061);
        var queries = Enumerable.Range(0, 2000).Select(_ => { var top = random.NextDouble() * layout.Height; return (Top: top, Bottom: top + 900); }).ToArray();
        long sink = 0;
        object Measure(Action action)
        {
            for (var i = 0; i < 3; i++) action();
            var times = new double[7]; var allocations = new long[7];
            for (var i = 0; i < 7; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
                action(); times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                allocations[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            return new { medianMs = times.Order().ElementAt(3), medianAllocatedBytes = allocations.Order().ElementAt(3), rawMs = times, rawAllocatedBytes = allocations };
        }
        int Brute((double Top, double Bottom) query)
        {
            var count = 0;
            for (var i = 0; i < layout.Pages.Count; i++)
                if (layout.PageTop(i) <= query.Bottom && layout.PageTop(i) + layout.Pages[i].Settings.Height >= query.Top) count++;
            return count;
        }
        foreach (var query in queries)
            if (Brute(query) != layout.VisiblePages(query.Top, query.Bottom).Count) throw new InvalidOperationException("Viewport query mismatch");
        var beforeViewport = Measure(() => { foreach (var query in queries) sink += Brute(query); });
        var indexedViewport = Measure(() => { foreach (var query in queries) sink += layout.VisiblePages(query.Top, query.Bottom).Count; });
        var beforeWords = Measure(() => { for (var i = 0; i < 25; i++) sink += Regex.Matches(document.PlainText, @"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*").Count; });
        var capturedWords = Measure(() => { for (var i = 0; i < 25; i++) sink += snapshot.WordCount; });
        var captureCost = Measure(() => sink += new DocumentTextSnapshot(document).WordCount);
        if (Regex.Matches(document.PlainText, @"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*").Count != snapshot.WordCount)
            throw new InvalidOperationException("Word-count mismatch");
        return JsonSerializer.Serialize(new
        {
            framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), warmups = 3, measurements = 7,
            paragraphs = 500, characters = snapshot.Text.Length, pages = pages.Length, viewportQueries = queries.Length,
            wordCountQueries = 25, beforeViewport, indexedViewport, beforeWords, capturedWords, captureCost, sink,
            scope = "CPU query microbenchmarks; excludes rendering, browser input, full edit/repagination and startup. Cached statistics are reused only until the next document change."
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
