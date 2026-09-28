using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>
/// Single-writer bounded LRU of paragraph-relative geometry. Returned geometry is independently
/// owned; cached lines are never exposed to pagination or callers. Unversioned metrics must remain
/// stable until Clear is called. EstimatedBytes bounds estimated payload, not process working set.
/// </summary>
public sealed class ParagraphLayoutCache
{
    private readonly record struct Key(string Id, double Width, double TabInterval);
    private readonly record struct RunSnapshot(string Text, TextStyle Style);
    private sealed record Entry(Key Key, ParagraphFormat Format, TextStyle DefaultStyle,
        RunSnapshot[] Runs, List<LayoutLine> Lines, long EstimatedBytes);

    private readonly ITextMetrics _metrics;
    private readonly ParagraphLayouter _layouter;
    private readonly Dictionary<Key, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _lru = [];
    private readonly int _maximumEntries;
    private readonly long _maximumBytes;
    private long _metricsVersion;
    public long Hits { get; private set; }
    public long Misses { get; private set; }
    public int CachedParagraphs => _entries.Count;
    public long EstimatedBytes { get; private set; }

    public ParagraphLayoutCache(ITextMetrics metrics, int maximumEntries = 2048, long maximumBytes = 32L * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        _metrics = metrics; _layouter = new(metrics);
        _maximumEntries = maximumEntries; _maximumBytes = maximumBytes;
        _metricsVersion = ReadVersion();
    }
    public List<LayoutLine> Layout(Paragraph paragraph, double width, int globalStart, double defaultTabStop = 36)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        if (!double.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegative(globalStart);
        var version = ReadVersion();
        if (_metricsVersion != version) { Clear(); _metricsVersion = version; }
        var key = new Key(paragraph.Id, width, defaultTabStop);
        if (_entries.TryGetValue(key, out var node))
        {
            if (Matches(node.Value, paragraph))
            {
                Hits++; _lru.Remove(node); _lru.AddFirst(node);
                return Copy(node.Value.Lines, globalStart);
            }
            Remove(node);
        }
        Misses++;
        var lines = _layouter.Layout(paragraph, width, 0, defaultTabStop);
        var runs = paragraph.Runs.Select(run => new RunSnapshot(run.Text, run.Style)).ToArray();
        var bytes = Estimate(paragraph.Id, runs, lines);
        if (_maximumEntries > 0 && bytes <= _maximumBytes)
        {
            while (_lru.Last is { } last && (_entries.Count >= _maximumEntries || EstimatedBytes > _maximumBytes - bytes)) Remove(last);
            var added = _lru.AddFirst(new Entry(key, paragraph.Format, paragraph.DefaultStyle, runs, lines, bytes));
            _entries.Add(key, added); EstimatedBytes += bytes;
        }
        return Copy(lines, globalStart);
    }
    public void Clear() { _entries.Clear(); _lru.Clear(); EstimatedBytes = 0; }
    public void ResetStatistics() { Hits = 0; Misses = 0; }
    private long ReadVersion() => _metrics is IVersionedTextMetrics v ? v.MetricsVersion : 0;
    private static bool Matches(Entry entry, Paragraph paragraph)
    {
        if (entry.Format != paragraph.Format || entry.DefaultStyle != paragraph.DefaultStyle || entry.Runs.Length != paragraph.Runs.Count) return false;
        for (var i = 0; i < entry.Runs.Length; i++)
            if (entry.Runs[i].Style != paragraph.Runs[i].Style || !string.Equals(entry.Runs[i].Text, paragraph.Runs[i].Text, StringComparison.Ordinal)) return false;
        return true;
    }
    private void Remove(LinkedListNode<Entry> node)
    {
        _entries.Remove(node.Value.Key); _lru.Remove(node); EstimatedBytes -= node.Value.EstimatedBytes;
    }
    private static long Estimate(string id, RunSnapshot[] runs, List<LayoutLine> lines)
    {
        long bytes = 256L + id.Length * 2L + runs.Length * 32L;
        foreach (var run in runs) bytes += run.Text.Length * 2L;
        foreach (var line in lines)
        {
            bytes += 256 + line.BarTabs.Length * sizeof(double);
            foreach (var chunk in line.Chunks) bytes += 160L + chunk.Text.Length * 2L + (chunk.DisplayText?.Length ?? 0) * 2L + chunk.Carets.Length * sizeof(double);
        }
        return bytes;
    }
    private static List<LayoutLine> Copy(List<LayoutLine> source, int globalStart)
    {
        var result = new List<LayoutLine>(source.Count);
        foreach (var line in source)
        {
            var chunks = new List<LayoutChunk>(line.Chunks.Count);
            foreach (var chunk in line.Chunks)
                chunks.Add(new() { Text = chunk.Text, Style = chunk.Style, Start = checked(chunk.Start + globalStart),
                    X = chunk.X, Width = chunk.Width, Carets = (double[])chunk.Carets.Clone(), TabLeader = chunk.TabLeader, DisplayText = chunk.DisplayText });
            result.Add(new() { ParagraphId = line.ParagraphId, Start = checked(line.Start + globalStart), End = checked(line.End + globalStart),
                PageIndex = line.PageIndex, X = line.X, Y = line.Y, Width = line.Width, Height = line.Height, Ascent = line.Ascent,
                LastInParagraph = line.LastInParagraph, Marker = line.Marker, Format = line.Format, DefaultStyle = line.DefaultStyle, Chunks = chunks, BarTabs = (double[])line.BarTabs.Clone() });
        }
        return result;
    }
}
