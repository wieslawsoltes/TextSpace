namespace TextSpace.Layout;

public sealed partial class DocumentLayout
{
    private readonly record struct IndexedLine(LayoutLine Line, int Order);
    private IndexedLine[] _textIndex = [];
    private int[] _prefixMaximumEnd = [];
    private void BuildTextLookup()
    {
        _textIndex = new IndexedLine[_lines.Length];
        for (var i = 0; i < _lines.Length; i++) _textIndex[i] = new(_lines[i], i);
        Array.Sort(_textIndex, static (a, b) => { var c = a.Line.Start.CompareTo(b.Line.Start); return c != 0 ? c : a.Order.CompareTo(b.Order); });
        _prefixMaximumEnd = new int[_textIndex.Length]; var maximum = int.MinValue;
        for (var i = 0; i < _textIndex.Length; i++) { maximum = Math.Max(maximum, _textIndex[i].Line.End); _prefixMaximumEnd[i] = maximum; }
    }
    private LayoutLine? FindCaretLine(int position)
    {
        var low = 0; var high = _textIndex.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_textIndex[middle].Line.Start <= position) low = middle + 1; else high = middle;
        }
        LayoutLine? best = null; var order = -1;
        // Original order resolves shared endpoints and overlapping table-row text intervals.
        for (var i = low - 1; i >= 0 && _prefixMaximumEnd[i] >= position; i--)
        {
            var candidate = _textIndex[i];
            if (candidate.Line.End >= position && candidate.Order > order) { best = candidate.Line; order = candidate.Order; }
        }
        return best ?? (_lines.Length == 0 ? null : _lines[^1]);
    }
    private static LayoutLine? NearestLine(IReadOnlyList<LayoutLine> lines, double x, double y)
    {
        if (lines.Count == 0) return null;
        if (!double.IsFinite(x) || !double.IsFinite(y)) return lines[0];
        LayoutLine? nearest = null; var distance = double.PositiveInfinity;
        foreach (var line in lines)
        {
            var vertical = y < line.Y ? line.Y - y : y > line.Y + line.Height ? y - line.Y - line.Height : 0;
            var horizontal = x < line.X ? line.X - x : x > line.X + line.Width ? x - line.X - line.Width : 0;
            var score = vertical * 10000 + horizontal;
            if (score < distance) { distance = score; nearest = line; }
        }
        return nearest;
    }
}
