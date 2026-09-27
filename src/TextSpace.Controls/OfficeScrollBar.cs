using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace TextSpace.Controls;

/// <summary>Custom office scrollbar with proportional thumb, paging and keyboard navigation.</summary>
public sealed class OfficeScrollBar : UserControl
{
    private sealed class Surface : SKCanvasElement
    {
        public Action<SKCanvas, Size>? Draw { get; set; }
        protected override void RenderOverride(SKCanvas canvas, Size area) => Draw?.Invoke(canvas, area);
    }
    private readonly Surface _surface = new();
    private double _extent = 1, _viewport = 1, _offset, _dragStart, _dragOffset;
    private bool _dragging, _hover;
    public bool IsVertical { get; }
    public event Action<double>? ScrollRequested;
    public double Maximum => Math.Max(0, _extent - _viewport);
    public double Offset => _offset;
    public OfficeScrollBar(bool vertical = true)
    {
        IsVertical = vertical; if (vertical) Width = 16; else Height = 16;
        Content = _surface; IsTabStop = true; AutomationProperties.SetName(this, vertical ? "Document vertical scrollbar" : "Document horizontal scrollbar");
        _surface.Draw = Draw;
        _surface.PointerEntered += (_, _) => { _hover = true; _surface.Invalidate(); }; _surface.PointerExited += (_, _) => { _hover = false; _surface.Invalidate(); };
        _surface.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(_surface).Position; var coordinate = vertical ? point.Y : point.X; var (start, size, _) = Geometry();
            if (coordinate >= start && coordinate <= start + size) { _dragging = true; _dragStart = coordinate; _dragOffset = _offset; _surface.CapturePointer(e.Pointer); }
            else Request(_offset + (coordinate < start ? -1 : 1) * Math.Max(30, _viewport * 0.9)); e.Handled = true;
        };
        _surface.PointerMoved += (_, e) => { if (!_dragging) return; var point = e.GetCurrentPoint(_surface).Position; var (_, size, track) = Geometry(); Request(_dragOffset + ((vertical ? point.Y : point.X) - _dragStart) * Maximum / Math.Max(1, track - size)); e.Handled = true; };
        _surface.PointerReleased += (_, e) => { _dragging = false; _surface.ReleasePointerCaptures(); _surface.Invalidate(); e.Handled = true; };
        _surface.PointerCaptureLost += (_, _) => { _dragging = false; _surface.Invalidate(); };
        _surface.PointerWheelChanged += (_, e) => { Request(_offset - e.GetCurrentPoint(_surface).Properties.MouseWheelDelta); e.Handled = true; };
        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case VirtualKey.Up: case VirtualKey.Left: Request(_offset - 40); break;
                case VirtualKey.Down: case VirtualKey.Right: Request(_offset + 40); break;
                case VirtualKey.PageUp: Request(_offset - _viewport); break;
                case VirtualKey.PageDown: Request(_offset + _viewport); break;
                case VirtualKey.Home: Request(0); break;
                case VirtualKey.End: Request(Maximum); break;
                default: return;
            }
            e.Handled = true;
        };
    }
    public void SetMetrics(double extent, double viewport, double offset)
    {
        _extent = Math.Max(1, extent); _viewport = Math.Max(1, viewport); _offset = Math.Clamp(offset, 0, Maximum); _surface.Invalidate();
    }
    private void Request(double offset) { _offset = Math.Clamp(offset, 0, Maximum); _surface.Invalidate(); ScrollRequested?.Invoke(_offset); }
    private (double Start, double Size, double Track) Geometry()
    {
        var track = Math.Max(1, (IsVertical ? _surface.ActualHeight : _surface.ActualWidth) - 4); var size = Math.Min(track, Math.Max(28, track * _viewport / _extent));
        var start = 2 + (Maximum > 0 ? _offset / Maximum * (track - size) : 0); return (start, size, track);
    }
    private void Draw(SKCanvas canvas, Size area)
    {
        canvas.Clear(SKColor.Parse("#F2F2F2")); if (Maximum <= 0) return;
        var (start, size, _) = Geometry(); using var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse(_dragging ? "#737373" : _hover ? "#A0A0A0" : "#BFBFBF") };
        var rect = IsVertical ? SKRect.Create(4, (float)start, (float)area.Width - 8, (float)size) : SKRect.Create((float)start, 4, (float)size, (float)area.Height - 8);
        canvas.DrawRoundRect(rect, 3, 3, paint);
    }
}
