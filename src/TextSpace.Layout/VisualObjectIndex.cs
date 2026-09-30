using TextSpace.Core;

namespace TextSpace.Layout;

public readonly record struct VisualObjectLocation(int PageIndex, LayoutObject Item);

/// <summary>
/// Single-layout object navigation and painter-order hit testing. Rebuild after
/// layout changes. Repeated-header replicas are neither selectable nor navigable.
/// Queries allocate no result collections and do not walk other pages.
/// </summary>
public sealed class VisualObjectIndex
{
    private readonly LayoutObject[][] _pages;
    private readonly Dictionary<string, int> _positions = new(StringComparer.Ordinal);
    private readonly VisualObjectLocation[] _locations;
    public IReadOnlyList<VisualObjectLocation> Locations { get; }

    public VisualObjectIndex(DocumentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _pages = new LayoutObject[layout.Pages.Count][];
        var locations = new List<VisualObjectLocation>();
        for (var p = 0; p < _pages.Length; p++)
        {
            var page = layout.Pages[p];
            var objects = page.Objects.Count > 0 ? page.Objects.Where(o => !o.IsReplica).ToArray()
                : page.Images.Where(o => !o.IsReplica).Select(o => new LayoutObject(o.Image, o.Bounds)).ToArray();
            _pages[p] = objects;
            foreach (var item in objects)
                if (_positions.TryAdd(item.Object.Id, locations.Count)) locations.Add(new(p, item));
        }
        _locations = locations.ToArray(); Locations = Array.AsReadOnly(_locations);
    }
    public bool TryLocate(string? id, out VisualObjectLocation location)
    {
        if (id is not null && _positions.TryGetValue(id, out var index)) { location = _locations[index]; return true; }
        location = default; return false;
    }
    public VisualObjectLocation? Next(string? id, bool backwards = false)
    {
        if (_locations.Length == 0) return null;
        if (id is null || !_positions.TryGetValue(id, out var index)) return _locations[backwards ? ^1 : 0];
        var next = backwards ? index == 0 ? _locations.Length - 1 : index - 1 : index + 1 == _locations.Length ? 0 : index + 1;
        return _locations[next];
    }
    public bool TryHitTest(int pageIndex, double x, double y, out VisualObjectLocation location)
    {
        location = default;
        if ((uint)pageIndex >= (uint)_pages.Length || !double.IsFinite(x) || !double.IsFinite(y)) return false;
        var objects = _pages[pageIndex];
        // Renderer paints inline objects first and floating objects above body
        // text, irrespective of their mixed order in the page's source list.
        for (var layer = 1; layer >= 0; layer--)
            for (var i = objects.Length - 1; i >= 0; i--)
            {
                var item = objects[i];
                if (item.Object.Placement.Floating != (layer == 1)) continue;
                var local = VisualGeometry.Local(item.Bounds, x, y, item.Object.Placement.Rotation);
                if (local.X >= 0 && local.Y >= 0 && local.X <= item.Bounds.Width && local.Y <= item.Bounds.Height)
                { location = new(pageIndex, item); return true; }
            }
        return false;
    }
}
