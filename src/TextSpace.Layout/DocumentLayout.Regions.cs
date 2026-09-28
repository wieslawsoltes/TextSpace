using TextSpace.Core;

namespace TextSpace.Layout;

public sealed partial class DocumentLayout
{
    public ParagraphFormat ParagraphFormatAt(int position) => FindCaretLine(position)?.Format ?? new();

    public LayoutRegion? RegionAt(int position) => FindCaretLine(position)?.Region;

    /// <summary>Page-shaped ruler geometry with margins adjusted to the active text column.</summary>
    public PageSettings RulerAt(int position)
    {
        var line = FindCaretLine(position);
        if (line?.Region is not { } region) return Pages[line?.PageIndex ?? 0].Settings;
        return region.Ruler(line.ColumnIndex);
    }

    /// <summary>O(log pages) query; exact intersections, no allocations, inclusive viewport edges.</summary>
    public VisiblePageRange VisiblePages(double top, double bottom)
    {
        if (!double.IsFinite(top) || !double.IsFinite(bottom) || bottom < top)
            throw new ArgumentOutOfRangeException(nameof(top), "Viewport bounds must be finite and ordered.");
        if (bottom < 0 || top > Height) return new(0, 0);
        var first = PageAtY(top);
        if (PageTop(first) + Pages[first].Settings.Height < top) first++;
        var last = PageAtY(bottom);
        while (last >= first && PageTop(last) > bottom) last--;
        return last < first ? new(first, first) : new(first, last + 1);
    }
}
