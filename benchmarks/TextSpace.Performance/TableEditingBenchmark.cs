using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TextSpace.Core;
using TextSpace.Editing;

internal static class TableEditingBenchmark
{
    public static string Run(string label)
    {
        const int rowCount = 100, columnCount = 16;
        var table = TableBlock.Create(rowCount, columnCount);
        for (var r = 0; r < rowCount; r++)
            for (var c = 0; c < columnCount; c++)
                table.Rows[r].Cells[c].Blocks = [new Paragraph($"Cell {r:D3}:{c:D2} - a stable paragraph identity")];
        var document = new DocumentModel { Blocks = [table, new Paragraph("End")] };
        var index = new TextIndex(document);
        foreach (var p in index.Paragraphs)
            document.Bookmarks.Add(new() { Name = "B" + p.Paragraph.Id, Start = p.Start, End = p.End });
        var session = new EditorSession(document);
        var paragraphs = Enumerable.Range(0, 4000).Select(i => new Paragraph { Runs =
            [new("First " + i), new("bold", new() { Bold = true }), new(" normal "), new("italic", new() { Italic = true })] }).ToArray();
        object Measure(Action operation)
        {
            for (var i = 0; i < 3; i++) operation();
            var times = new double[7]; var allocations = new long[7];
            for (var i = 0; i < times.Length; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
                operation(); watch.Stop(); times[i] = watch.Elapsed.TotalMilliseconds;
                allocations[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            return new { medianMs = times.Order().ElementAt(3), medianAllocatedBytes = allocations.Order().ElementAt(3),
                elapsedMs = times, allocatedBytes = allocations, warmups = 3, measurements = 7 };
        }
        var normalize = Measure(() => { foreach (var p in paragraphs) p.Normalize(); });
        var edit = Measure(() => { session.AddTableColumn(); session.Undo(); });
        return JsonSerializer.Serialize(new { label, framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            normalizationParagraphs = paragraphs.Length, normalizationRunsPerParagraph = 4,
            tableRows = rowCount, tableColumns = columnCount, bookmarks = document.Bookmarks.Count,
            normalize, insertColumnAndUndo = edit }, new JsonSerializerOptions { WriteIndented = true });
    }
}
