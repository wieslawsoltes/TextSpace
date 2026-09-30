using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Maps visible crop-handle movement into unflipped source-image edges.</summary>
public static class VisualCropGeometry
{
    public static ImageCrop Drag(ImageCrop source, VisualHandle handle, double pageDx, double pageDy,
        double frameWidth, double frameHeight, VisualPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(placement);
        if (!Enum.IsDefined(handle)) throw new ArgumentOutOfRangeException(nameof(handle));
        if (!double.IsFinite(pageDx) || !double.IsFinite(pageDy) || !double.IsFinite(frameWidth) || !double.IsFinite(frameHeight)
            || frameWidth <= 0 || frameHeight <= 0 || !double.IsFinite(placement.Rotation)) throw new ArgumentOutOfRangeException(nameof(frameWidth));
        if (!double.IsFinite(source.Left + source.Top + source.Right + source.Bottom) || source.Left < 0 || source.Right < 0
            || source.Top < 0 || source.Bottom < 0 || source.Left + source.Right >= 0.99 || source.Top + source.Bottom >= 0.99)
            throw new ArgumentException("Invalid source-image crop.", nameof(source));
        if (handle is VisualHandle.None or VisualHandle.Move or VisualHandle.Rotate || pageDx == 0 && pageDy == 0) return source;
        var delta = VisualGeometry.Rotate(pageDx, pageDy, -placement.Rotation);
        var dx = delta.X / frameWidth * (1 - source.Left - source.Right);
        var dy = delta.Y / frameHeight * (1 - source.Top - source.Bottom);
        var left = source.Left; var right = source.Right; var top = source.Top; var bottom = source.Bottom;
        static double Clamp(double value, double opposite) => Math.Clamp(value, 0, Math.Max(0, 0.98 - opposite));
        if (handle is VisualHandle.NorthWest or VisualHandle.West or VisualHandle.SouthWest)
        {
            if (placement.FlipHorizontal) right = Clamp(right + dx, left); else left = Clamp(left + dx, right);
        }
        if (handle is VisualHandle.NorthEast or VisualHandle.East or VisualHandle.SouthEast)
        {
            if (placement.FlipHorizontal) left = Clamp(left - dx, right); else right = Clamp(right - dx, left);
        }
        if (handle is VisualHandle.NorthWest or VisualHandle.North or VisualHandle.NorthEast)
        {
            if (placement.FlipVertical) bottom = Clamp(bottom + dy, top); else top = Clamp(top + dy, bottom);
        }
        if (handle is VisualHandle.SouthWest or VisualHandle.South or VisualHandle.SouthEast)
        {
            if (placement.FlipVertical) top = Clamp(top - dy, bottom); else bottom = Clamp(bottom - dy, top);
        }
        return new() { Left = left, Top = top, Right = right, Bottom = bottom };
    }
}
