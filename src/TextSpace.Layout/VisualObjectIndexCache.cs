namespace TextSpace.Layout;

/// <summary>
/// Single-owner, layout-scoped lazy navigation index. Null-selection lookups
/// invalidate obsolete snapshots without scanning or indexing the new document.
/// </summary>
public sealed class VisualObjectIndexCache
{
    private DocumentLayout? _layout;
    private VisualObjectIndex? _index;
    public bool IsPopulated => _index is not null;

    private void UseLayout(DocumentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (ReferenceEquals(_layout, layout)) return;
        _index = null; _layout = layout;
    }
    public VisualObjectIndex Get(DocumentLayout layout)
    {
        UseLayout(layout); return _index ??= new(layout);
    }
    public bool TryLocate(DocumentLayout layout, string? id, out VisualObjectLocation location)
    {
        UseLayout(layout);
        if (id is null) { location = default; return false; }
        return (_index ??= new(layout)).TryLocate(id, out location);
    }
    public void Clear() { _index = null; _layout = null; }
}
