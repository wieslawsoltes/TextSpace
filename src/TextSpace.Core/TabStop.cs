using System.Text.Json.Serialization;

namespace TextSpace.Core;

public enum TabAlignment { Left, Center, Right, Decimal, Bar }
public enum TabLeader { None, Dot, Hyphen, Underscore, Heavy, MiddleDot }

/// <summary>Positions are typographic points from the column's left text edge, or an inset from its right edge.</summary>
public sealed record TabStop
{
    public double Position { get; init; }
    public TabAlignment Alignment { get; init; }
    public TabLeader Leader { get; init; }
    public bool RelativeToRightEdge { get; init; }
    public char DecimalCharacter { get; init; } = '.';
    public double Resolve(double columnWidth, double rightIndent = 0) => RelativeToRightEdge ? columnWidth - rightIndent - Position : Position;

    public TabStop() { }

    [JsonConstructor]
    public TabStop(double position = 0, TabAlignment alignment = TabAlignment.Left,
        TabLeader leader = TabLeader.None, bool relativeToRightEdge = false, char decimalCharacter = '.')
    {
        Position = position; Alignment = alignment; Leader = leader;
        RelativeToRightEdge = relativeToRightEdge; DecimalCharacter = decimalCharacter;
    }
}
