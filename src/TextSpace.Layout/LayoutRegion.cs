using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>
/// A section's flow region on one physical page. Multiple sections can occupy
/// the same page. Geometry is finalized by pagination and must not be mutated
/// after DocumentLayout indexes its lines.
/// </summary>
public sealed class LayoutRegion
{
    public required int SectionIndex { get; init; }
    public required int SectionPageIndex { get; init; }
    public required int PageNumber { get; init; }
    public required SectionDefinition Section { get; init; }
    public required double Top { get; init; }
    public required int FirstColumn { get; init; }
    public int LastColumn { get; internal set; }
    public double Bottom { get; internal set; }
    public int SectionPageCount { get; internal set; }
    public bool Balanced { get; internal set; }
    public double ColumnLeft(int column) => Section.Page.MarginLeft
        + column * (Section.Page.ColumnWidth + Section.Page.ColumnGap);
    private PageSettings?[]? _rulers;
    internal PageSettings Ruler(int column)
    {
        var settings = Section.Page;
        if (settings.Columns == 1) return settings;
        _rulers ??= new PageSettings[settings.Columns];
        var left = ColumnLeft(column);
        return _rulers[column] ??= settings with
        {
            MarginLeft = left, MarginRight = settings.Width - left - settings.ColumnWidth, Columns = 1
        };
    }
    public RectD Bounds => new(ColumnLeft(FirstColumn), Top,
        (LastColumn - FirstColumn + 1) * (Section.Page.ColumnWidth + Section.Page.ColumnGap)
        - Section.Page.ColumnGap, Math.Max(0, Bottom - Top));
}

public sealed record LayoutNotice(string Code, int SectionIndex, string Message);

/// <summary>Half-open physical-page interval returned without allocating an enumerable.</summary>
public readonly record struct VisiblePageRange(int Start, int End)
{
    public int Count => End - Start;
}
