namespace TextSpace.Core;

/// <summary>A structural, editable display equation, including equations in table cells.</summary>
public sealed class EquationBlock : VisualBlock
{
    public EquationBlock() { Width = 240; Height = 64; }
    public EquationNode Root { get; set; } = EquationNode.Row(EquationNode.Leaf("x"));
    public double FontSize { get; set; } = 18;
    public string Color { get; set; } = "#202020";
}
