namespace TextSpace.Core;

/// <summary>Immutable character formatting. Font sizes and geometry use typographic points.</summary>
public sealed record TextStyle
{
    public string FontFamily { get; init; } = "Aptos";
    public double FontSize { get; init; } = 11;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool StrikeThrough { get; init; }
    public bool Superscript { get; init; }
    public bool Subscript { get; init; }
    public string Color { get; init; } = "#202020";
    public string? Highlight { get; init; }
    public string? Hyperlink { get; init; }
    public double EffectiveSize => FontSize * (Superscript || Subscript ? 0.7 : 1);
}
