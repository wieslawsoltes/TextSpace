using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>
/// Single-writer LRU for visual typography. Transform-only edits reuse measurement;
/// in-place tree/story edits, slot identities and metric revisions invalidate it.
/// Published display lists are immutable snapshots, not mutable document objects.
/// </summary>
public sealed class VisualLayoutCache
{
    private readonly record struct Key(string Id, bool Equation);
    private readonly record struct NodeState(string Id, EquationKind Kind, string Text, int Columns, int Children);
    private sealed record ShapeState(string Text, TextStyle Style, double Width, double Height, double Padding, CellVerticalAlignment Alignment);
    private sealed class Entry
    {
        public required Key Key;
        public NodeState[]? Nodes;
        public double FontSize;
        public ShapeState? Shape;
        public EquationLayout? Equation;
        public IReadOnlyList<VisualTextLine>? Lines;
        public long Bytes;
    }
    private readonly ITextMetrics _metrics;
    private readonly Dictionary<Key, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _lru = [];
    private readonly int _maximumEntries;
    private readonly long _maximumBytes;
    private long _version;
    public long Hits { get; private set; }
    public long Misses { get; private set; }
    public long EstimatedBytes { get; private set; }
    public int Count => _entries.Count;

    public VisualLayoutCache(ITextMetrics metrics, int maximumEntries = 128, long maximumBytes = 8L * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        _metrics = metrics; _maximumEntries = maximumEntries; _maximumBytes = maximumBytes; _version = Version;
    }
    private long Version => _metrics is IVersionedTextMetrics versioned ? versioned.MetricsVersion : 0;
    private void CheckVersion() { var version = Version; if (_version != version) { Clear(); _version = version; } }
    public void Clear() { _entries.Clear(); _lru.Clear(); EstimatedBytes = 0; }
    public void Invalidate(string id)
    {
        if (_entries.TryGetValue(new(id, false), out var shape)) Remove(shape);
        if (_entries.TryGetValue(new(id, true), out var equation)) Remove(equation);
    }
    public EquationLayout GetEquation(EquationBlock block)
    {
        ArgumentNullException.ThrowIfNull(block); CheckVersion(); var key = new Key(block.Id, true);
        if (_entries.TryGetValue(key, out var node))
        {
            if (node.Value.FontSize == block.FontSize && Matches(block.Root, node.Value.Nodes!))
            { Touch(node); Hits++; return node.Value.Equation!; }
            Remove(node);
        }
        Misses++; VisualBlockRules.Validate(block);
        var measured = new EquationLayouter(_metrics).Layout(block.Root, block.FontSize);
        var frozen = new EquationLayout(measured.Width, measured.Height, measured.Ascent,
            Array.AsReadOnly(measured.Glyphs.ToArray()),
            Array.AsReadOnly(measured.Rules.Select(r => new EquationRule(Array.AsReadOnly(r.Points.ToArray()), r.Thickness)).ToArray()),
            Array.AsReadOnly(measured.Slots.ToArray()));
        var states = new List<NodeState>();
        void Capture(EquationNode n)
        {
            states.Add(new(n.Id, n.Kind, n.Text, n.Columns, n.Children.Count));
            foreach (var child in n.Children) Capture(child);
        }
        Capture(block.Root);
        var bytes = 256L + states.Sum(s => 64L + 2L * (s.Id.Length + s.Text.Length))
            + frozen.Glyphs.Sum(g => 128L + g.Text.Length * 2L) + frozen.Slots.Count * 128L
            + frozen.Rules.Sum(r => 48L + r.Points.Count * 16L);
        Add(new() { Key = key, Nodes = states.ToArray(), FontSize = block.FontSize, Equation = frozen, Bytes = bytes });
        return frozen;
    }
    public IReadOnlyList<VisualTextLine> GetShape(ShapeBlock block)
    {
        ArgumentNullException.ThrowIfNull(block); CheckVersion(); var key = new Key(block.Id, false);
        if (_entries.TryGetValue(key, out var node))
        {
            var state = node.Value.Shape!;
            if (state.Text == block.Text && state.Style == block.TextStyle && state.Width == block.Width && state.Height == block.Height
                && state.Padding == block.Padding && state.Alignment == block.VerticalAlignment)
            { Touch(node); Hits++; return node.Value.Lines!; }
            Remove(node);
        }
        Misses++; VisualBlockRules.Validate(block);
        var lines = Array.AsReadOnly(VisualTextLayout.Layout(block, _metrics).ToArray());
        var signature = new ShapeState(block.Text, block.TextStyle, block.Width, block.Height, block.Padding, block.VerticalAlignment);
        Add(new() { Key = key, Shape = signature, Lines = lines, Bytes = 256L + block.Text.Length * 2L + lines.Sum(l => 64L + l.Text.Length * 2L) });
        return lines;
    }
    private static bool Matches(EquationNode? root, NodeState[] states)
    {
        var at = 0;
        bool Match(EquationNode? node, int depth)
        {
            if (node is null || at >= states.Length || depth > EquationRules.MaximumDepth || node.Children is null) return false;
            var state = states[at++];
            if (state.Id != node.Id || state.Kind != node.Kind || state.Text != node.Text || state.Columns != node.Columns || state.Children != node.Children.Count) return false;
            for (var i = 0; i < node.Children.Count; i++) if (!Match(node.Children[i], depth + 1)) return false;
            return true;
        }
        return Match(root, 0) && at == states.Length;
    }
    private void Touch(LinkedListNode<Entry> node) { _lru.Remove(node); _lru.AddFirst(node); }
    private void Remove(LinkedListNode<Entry> node) { _entries.Remove(node.Value.Key); _lru.Remove(node); EstimatedBytes -= node.Value.Bytes; }
    private void Add(Entry entry)
    {
        if (_maximumEntries == 0 || entry.Bytes > _maximumBytes) return;
        while (_lru.Last is { } last && (_entries.Count >= _maximumEntries || EstimatedBytes > _maximumBytes - entry.Bytes)) Remove(last);
        _entries.Add(entry.Key, _lru.AddFirst(entry)); EstimatedBytes += entry.Bytes;
    }
}
