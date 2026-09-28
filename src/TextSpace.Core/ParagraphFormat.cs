using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace TextSpace.Core;

public enum TextAlignment { Left, Center, Right, Justify }
public enum ListKind { None, Bullet, Number }

public sealed record ParagraphFormat
{
    private ImmutableArray<TabStop> _tabStops = [];
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
    [JsonConverter(typeof(TabStopArrayJsonConverter))]
    public ImmutableArray<TabStop> TabStops
    {
        get => _tabStops;
        init => _tabStops = value.IsDefault ? [] : value;
    }
    public bool PageBreakBefore { get; init; }
    public bool BorderBottom { get; init; }
    public string? Shading { get; init; }

    public ParagraphFormat() { }

    // .NET 10 source-generated init-only property construction can overwrite
    // initializers for omitted members. Constructor argument defaults distinguish
    // omission from explicit zero/false/null without weakening validation.
    [JsonConstructor]
    public ParagraphFormat(string styleName = "Normal", TextAlignment alignment = TextAlignment.Left,
        double leftIndent = 0, double rightIndent = 0, double firstLineIndent = 0,
        double spaceBefore = 0, double spaceAfter = 8, double lineSpacing = 1.15,
        ListKind list = ListKind.None, int listLevel = 0, int outlineLevel = 0,
        bool keepWithNext = false, bool keepLinesTogether = false, bool widowControl = true,
        ImmutableArray<TabStop> tabStops = default, bool pageBreakBefore = false,
        bool borderBottom = false, string? shading = null)
    {
        StyleName = styleName; Alignment = alignment; LeftIndent = leftIndent; RightIndent = rightIndent;
        FirstLineIndent = firstLineIndent; SpaceBefore = spaceBefore; SpaceAfter = spaceAfter;
        LineSpacing = lineSpacing; List = list; ListLevel = listLevel; OutlineLevel = outlineLevel;
        KeepWithNext = keepWithNext; KeepLinesTogether = keepLinesTogether; WidowControl = widowControl;
        TabStops = tabStops; PageBreakBefore = pageBreakBefore; BorderBottom = borderBottom; Shading = shading;
    }
}
