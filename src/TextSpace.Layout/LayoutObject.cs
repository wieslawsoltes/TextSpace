using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Visual-object placement in page coordinates. Repeated headers are not independent objects.</summary>
public sealed record LayoutObject(VisualBlock Object, RectD Bounds)
{
    public bool IsReplica { get; init; }
}
