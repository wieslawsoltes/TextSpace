namespace TextSpace.Core;

public enum ShapeKind { Rectangle, RoundedRectangle, Ellipse, Diamond, Triangle, Line, Arrow, TextBox }

/// <summary>Retained vector geometry with editable text; never a baked screenshot.</summary>
public sealed class ShapeBlock : VisualBlock
{
    public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;
    public string? Fill { get; set; } = "#D9E5F5";
    public string Stroke { get; set; } = "#185ABD";
    public double StrokeWidth { get; set; } = 1.5;
    public double CornerRadius { get; set; } = 12;
    public string Text { get; set; } = "";
    public TextStyle TextStyle { get; set; } = new();
    public CellVerticalAlignment VerticalAlignment { get; set; } = CellVerticalAlignment.Center;
    public double Padding { get; set; } = 8;
}
