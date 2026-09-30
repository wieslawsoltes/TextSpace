namespace TextSpace.Core;

/// <summary>An independently selectable visual object, anchored at its position in block flow.</summary>
public abstract class VisualBlock : Block
{
    /// <summary>Optional user label, independent of content and alternative text.</summary>
    public string Name { get; set; } = "";
    public double Width { get; set; } = 240;
    public double Height { get; set; } = 120;
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;
    public VisualPlacement Placement { get; set; } = new();
}

/// <summary>
/// Point offsets from the aligned block-flow anchor. Floating objects do not consume
/// flow height and paint above text. This is not square/tight text wrapping.
/// </summary>
public sealed record VisualPlacement
{
    public bool Floating { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Rotation { get; init; }
    public bool FlipHorizontal { get; init; }
    public bool FlipVertical { get; init; }
}
