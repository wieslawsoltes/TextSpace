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
    public EquationSlot? HitTest(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return null;
        EquationSlot? best = null; var distance = double.PositiveInfinity;
        for (var i = 0; i < Slots.Count; i++)
        {
            var candidate = Slots[i]; var next = Distance(candidate.Bounds, x, y);
            if (best is null || next < distance) { best = candidate; distance = next; }
        }
        return best;
    }
    public EquationSlot? VerticalNeighbor(string id, bool down)
    {
        EquationSlot? source = null;
        for (var i = 0; i < Slots.Count; i++) if (Slots[i].Id == id) { source = Slots[i]; break; }
        if (source is null) return null;
        var x = source.Bounds.X + source.Bounds.Width / 2; var y = source.Bounds.Y + source.Bounds.Height / 2;
        EquationSlot? best = null; var distance = double.PositiveInfinity;
        for (var i = 0; i < Slots.Count; i++)
        {
            var candidate = Slots[i]; var centerY = candidate.Bounds.Y + candidate.Bounds.Height / 2;
            if (candidate.Id == id || (down ? centerY <= y + 1 : centerY >= y - 1)) continue;
            var next = Math.Abs(centerY - y) + Math.Abs(candidate.Bounds.X + candidate.Bounds.Width / 2 - x) * 2;
            if (next < distance) { best = candidate; distance = next; }
        }
        return best;
    }
    private static double Distance(RectD r, double x, double y)
    {
        var dx = Math.Max(r.X - x, Math.Max(0, x - r.Right)); var dy = Math.Max(r.Y - y, Math.Max(0, y - r.Bottom));
        return dx * dx + dy * dy;
    }
}
