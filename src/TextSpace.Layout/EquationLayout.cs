using TextSpace.Core;

namespace TextSpace.Layout;

public sealed record EquationSlot(string Id, RectD Bounds, double FontSize, string Text, string Role);
public sealed record EquationGlyph(string Text, TextStyle Style, double X, double Y, double Ascent, double ScaleX = 1, double ScaleY = 1, bool Placeholder = false);
public readonly record struct EquationPoint(double X, double Y);
public sealed record EquationRule(IReadOnlyList<EquationPoint> Points, double Thickness);

/// <summary>Measured equation display list shared by painting and WYSIWYG slot navigation.</summary>
public sealed record EquationLayout(double Width, double Height, double Ascent,
    IReadOnlyList<EquationGlyph> Glyphs, IReadOnlyList<EquationRule> Rules, IReadOnlyList<EquationSlot> Slots)
{
    public EquationSlot? HitTest(double x, double y) => Slots.OrderBy(s => Distance(s.Bounds, x, y)).FirstOrDefault();
    public EquationSlot? VerticalNeighbor(string id, bool down)
    {
        var source = Slots.FirstOrDefault(s => s.Id == id); if (source is null) return null;
        var x = source.Bounds.X + source.Bounds.Width / 2; var y = source.Bounds.Y + source.Bounds.Height / 2;
        return Slots.Where(s => s.Id != id && (down ? s.Bounds.Y + s.Bounds.Height / 2 > y + 1 : s.Bounds.Y + s.Bounds.Height / 2 < y - 1))
            .OrderBy(s => Math.Abs(s.Bounds.Y + s.Bounds.Height / 2 - y) + Math.Abs(s.Bounds.X + s.Bounds.Width / 2 - x) * 2).FirstOrDefault();
    }
    private static double Distance(RectD r, double x, double y)
    {
        var dx = Math.Max(r.X - x, Math.Max(0, x - r.Right)); var dy = Math.Max(r.Y - y, Math.Max(0, y - r.Bottom));
        return dx * dx + dy * dy;
    }
}
