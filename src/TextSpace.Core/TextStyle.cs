using System.Text.Json.Serialization;

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

    public TextStyle() { }

    [JsonConstructor]
    public TextStyle(string fontFamily = "Aptos", double fontSize = 11, bool bold = false,
        bool italic = false, bool underline = false, bool strikeThrough = false,
        bool superscript = false, bool subscript = false, string color = "#202020",
        string? highlight = null, string? hyperlink = null)
    {
        FontFamily = fontFamily; FontSize = fontSize; Bold = bold; Italic = italic;
        Underline = underline; StrikeThrough = strikeThrough; Superscript = superscript;
        Subscript = subscript; Color = color; Highlight = highlight; Hyperlink = hyperlink;
    }
}
