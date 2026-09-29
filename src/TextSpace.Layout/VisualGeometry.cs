using TextSpace.Core;

namespace TextSpace.Layout;

public enum VisualHandle { None, Move, NorthWest, North, NorthEast, East, SouthEast, South, SouthWest, West, Rotate }
public readonly record struct VisualResize(double Width, double Height, double OffsetX, double OffsetY);

/// <summary>Pure coordinate math, independent of UI event timing and document history.</summary>
public static class VisualGeometry
{
    public static RectD Place(VisualBlock block, double availableWidth, double availableHeight, double x, double y)
    {
        var ratio = Math.Min(1, Math.Min(availableWidth / block.Width, availableHeight / block.Height));
        var w = block.Width * ratio; var h = block.Height * ratio;
        var aligned = block.Alignment == TextAlignment.Center ? (availableWidth - w) / 2 : block.Alignment == TextAlignment.Right ? availableWidth - w : 0;
        return new(x + aligned + block.Placement.X, y + block.Placement.Y, w, h);
    }
    public static (double X, double Y) Rotate(double x, double y, double degrees)
    {
        var angle = degrees * Math.PI / 180; var c = Math.Cos(angle); var s = Math.Sin(angle);
        return (x * c - y * s, x * s + y * c);
    }
    public static (double X, double Y) Local(RectD bounds, double x, double y, double rotation)
    {
        var p = Rotate(x - bounds.X - bounds.Width / 2, y - bounds.Y - bounds.Height / 2, -rotation);
        return (p.X + bounds.Width / 2, p.Y + bounds.Height / 2);
    }
    public static VisualResize Resize(double width, double height, VisualHandle handle, double dx, double dy, bool aspect, bool centered = false)
    {
        if (!double.IsFinite(width + height + dx + dy) || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var west = handle is VisualHandle.NorthWest or VisualHandle.West or VisualHandle.SouthWest;
        var east = handle is VisualHandle.NorthEast or VisualHandle.East or VisualHandle.SouthEast;
        var north = handle is VisualHandle.NorthWest or VisualHandle.North or VisualHandle.NorthEast;
        var south = handle is VisualHandle.SouthWest or VisualHandle.South or VisualHandle.SouthEast;
        var w = width + (east ? dx : west ? -dx : 0) * (centered ? 2 : 1);
        var h = height + (south ? dy : north ? -dy : 0) * (centered ? 2 : 1);
        if (aspect)
        {
            var scale = north || south ? (west || east) && Math.Abs(w / width - 1) > Math.Abs(h / height - 1) ? w / width : h / height : w / width;
            scale = Math.Clamp(scale, Math.Max(6 / width, 6 / height), Math.Min(4000 / width, 4000 / height));
            w = width * scale; h = height * scale;
        }
        else { w = Math.Clamp(w, 6, 4000); h = Math.Clamp(h, 6, 4000); }
        return new(w, h, centered ? (width - w) / 2 : west ? width - w : 0, centered ? (height - h) / 2 : north ? height - h : 0);
    }
    public static IReadOnlyList<(VisualHandle Handle, double X, double Y)> Handles(double width, double height) =>
    [
        (VisualHandle.NorthWest, 0, 0), (VisualHandle.North, width / 2, 0), (VisualHandle.NorthEast, width, 0),
        (VisualHandle.East, width, height / 2), (VisualHandle.SouthEast, width, height), (VisualHandle.South, width / 2, height),
        (VisualHandle.SouthWest, 0, height), (VisualHandle.West, 0, height / 2)
    ];
}
