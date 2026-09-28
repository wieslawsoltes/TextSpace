using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Immutable spatial index for vertical interval queries; preserves paint order.</summary>
internal sealed class VerticalIntervalIndex<T>
{
    private readonly (T Value, double Start, double End, int Order)[] _items;
    private readonly double[] _prefixEnd;
    public VerticalIntervalIndex(IEnumerable<T> values, Func<T, double> start, Func<T, double> end)
    {
        _items = values.Select((value, order) => (Value: value, Start: start(value), End: end(value), Order: order))
            .OrderBy(item => item.Start).ThenBy(item => item.Order).ToArray();
        _prefixEnd = new double[_items.Length]; var maximum = double.NegativeInfinity;
        for (var i = 0; i < _items.Length; i++) _prefixEnd[i] = maximum = Math.Max(maximum, _items[i].End);
    }
    public IReadOnlyList<T> Intersect(double start, double end)
    {
        var low = 0; var high = _items.Length;
        while (low < high) { var middle = low + (high - low) / 2; if (_items[middle].Start < end) low = middle + 1; else high = middle; }
        var found = new List<int>();
        for (var i = low - 1; i >= 0 && _prefixEnd[i] > start; i--)
            if (_items[i].End > start) found.Add(i);
        found.Sort((a, b) => _items[a].Order.CompareTo(_items[b].Order));
        return found.Select(i => _items[i].Value).ToArray();
    }
}

internal sealed class TableFlowLayout
{
    public required double Height { get; init; }
    public required double HeaderHeight { get; init; }
    public required IReadOnlyList<(double Start, double End, bool AllowSplit)> Groups { get; init; }
    public List<LayoutLine> Lines { get; } = [];
    public List<LayoutCell> Cells { get; } = [];
    public List<LayoutImage> Images { get; } = [];
    public VerticalIntervalIndex<LayoutLine> LineIndex { get; private set; } = null!;
    public VerticalIntervalIndex<LayoutCell> CellIndex { get; private set; } = null!;
    public VerticalIntervalIndex<LayoutImage> ImageIndex { get; private set; } = null!;
    public void BuildIndexes()
    {
        LineIndex = new(Lines, l => l.Y, l => l.Y + l.Height);
        CellIndex = new(Cells, c => c.Bounds.Y, c => c.Bounds.Bottom);
        ImageIndex = new(Images, i => i.Bounds.Y, i => i.Bounds.Bottom);
    }
    public double Cut(double start, double end)
    {
        var cut = end;
        foreach (var line in LineIndex.Intersect(end - 0.000001, end))
            if (line.Y >= start && line.Y + line.Height > end + 0.0001) cut = Math.Min(cut, line.Y);
        foreach (var image in ImageIndex.Intersect(end - 0.000001, end))
            if (image.Bounds.Y >= start && image.Bounds.Bottom > end + 0.0001) cut = Math.Min(cut, image.Bounds.Y);
        return cut;
    }
    public double DrawSlice(LayoutPage page, double start, double end, double x, double y, bool replica = false)
    {
        var lines = LineIndex.Intersect(start, end).Where(l => l.Y >= start - 0.0001 && l.Y < end - 0.0001).ToArray();
        var images = ImageIndex.Intersect(start, end).Where(i => i.Bounds.Y >= start - 0.0001 && i.Bounds.Y < end - 0.0001).ToArray();
        // Lines in different columns need not have identical baselines. Finish every
        // assigned line within the available area; carry the next line only once.
        var drawEnd = Math.Max(end, Math.Max(lines.Length == 0 ? end : lines.Max(l => l.Y + l.Height), images.Length == 0 ? end : images.Max(i => i.Bounds.Bottom)));
        foreach (var cell in CellIndex.Intersect(start, drawEnd))
        {
            var top = Math.Max(start, cell.Bounds.Y); var bottom = Math.Min(drawEnd, cell.Bounds.Bottom);
            page.Cells.Add(cell with { Bounds = new(x + cell.Bounds.X, y + top - start, cell.Bounds.Width, bottom - top),
                DrawTop = cell.DrawTop && cell.Bounds.Y >= start - 0.0001,
                DrawBottom = cell.DrawBottom && cell.Bounds.Bottom <= end + 0.0001, IsReplica = replica });
        }
        foreach (var line in lines) page.Lines.Add(LayoutGeometry.Copy(line, x, y - start, page.Index, replica));
        foreach (var image in images) page.Images.Add(image with { Bounds = new(x + image.Bounds.X, y + image.Bounds.Y - start, image.Bounds.Width, image.Bounds.Height) });
        return drawEnd - start;
    }
}

internal static class LayoutGeometry
{
    public static LayoutLine Copy(LayoutLine line, double x, double y, int pageIndex, bool replica = false) => new()
    {
        ParagraphId = line.ParagraphId, Start = line.Start, End = line.End, X = line.X + x, Y = line.Y + y,
        PageIndex = pageIndex, IsReplica = replica, Width = line.Width, Height = line.Height, Ascent = line.Ascent,
        LastInParagraph = line.LastInParagraph, Marker = line.Marker, DefaultStyle = line.DefaultStyle, Format = line.Format,
        BarTabs = line.BarTabs.Select(p => p + x).ToArray(),
        Chunks = line.Chunks.Select(c => new LayoutChunk { Text = c.Text, DisplayText = c.DisplayText, Style = c.Style,
            Start = c.Start, X = c.X + x, Width = c.Width, TabLeader = c.TabLeader, Carets = (double[])c.Carets.Clone() }).ToList()
    };
}
