namespace TextSpace.Core;

public enum TextAlignment { Left, Center, Right, Justify }
public enum ListKind { None, Bullet, Number }

public sealed record ParagraphFormat
{
    private System.Collections.Immutable.ImmutableArray<TabStop> _tabStops = [];
    public string StyleName { get; init; } = "Normal";
    public TextAlignment Alignment { get; init; }
    public double LeftIndent { get; init; }
    public double RightIndent { get; init; }
    public double FirstLineIndent { get; init; }
    public double SpaceBefore { get; init; }
    public double SpaceAfter { get; init; } = 8;
    public double LineSpacing { get; init; } = 1.15;
    public ListKind List { get; init; }
    public int ListLevel { get; init; }
    public int OutlineLevel { get; init; }
    public bool KeepWithNext { get; init; }
    public bool KeepLinesTogether { get; init; }
    public bool WidowControl { get; init; } = true;
    [System.Text.Json.Serialization.JsonConverter(typeof(TabStopArrayJsonConverter))]
    public System.Collections.Immutable.ImmutableArray<TabStop> TabStops
    {
        get => _tabStops;
        // Generated init-only deserialization can supply default(T) for an absent
        // legacy member; the field initializer alone is not a sufficient invariant.
        init => _tabStops = value.IsDefault ? [] : value;
    }
    public bool PageBreakBefore { get; init; }
    public bool BorderBottom { get; init; }
    public string? Shading { get; init; }
}
