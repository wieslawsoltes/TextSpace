using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace TextSpace.Controls;

public sealed class OfficeZoomSlider : UserControl
{
    private sealed class Surface : SKCanvasElement
    {
        public Action<SKCanvas, Size>? Draw { get; set; }
        protected override void RenderOverride(SKCanvas canvas, Size area) => Draw?.Invoke(canvas, area);
    }
    private readonly Surface _surface = new();
    private bool _dragging;
    private double _value = 1;
    public double Value { get => _value; set { _value = Math.Clamp(value, 0.25, 3); _surface.Invalidate(); } }
    public event Action<double>? ValueChanged;
    public OfficeZoomSlider()
    {
        Width = 118; Height = 25; Content = _surface; IsTabStop = true; AutomationProperties.SetName(this, "Zoom slider");
        _surface.Draw = (canvas, area) =>
        {
            using var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#858585"), StrokeWidth = 1 };
            var y = (float)area.Height / 2; canvas.DrawLine(7, y, (float)area.Width - 7, y, paint);
            var x = 7 + (Value - 0.25) / 2.75 * (area.Width - 14); paint.Color = SKColor.Parse(OfficeTheme.Accent); canvas.DrawRoundRect((float)x - 2, y - 6, 4, 12, 1, 1, paint);
            paint.Color = SKColor.Parse("#8A8A8A"); var hundred = 7 + 0.75 / 2.75 * (area.Width - 14); canvas.DrawLine((float)hundred, y - 3, (float)hundred, y + 3, paint);
        };
        void Set(double x) { Value = Math.Round((0.25 + Math.Clamp((x - 7) / Math.Max(1, ActualWidth - 14), 0, 1) * 2.75) * 20) / 20; ValueChanged?.Invoke(Value); }
        _surface.PointerPressed += (_, e) => { _dragging = true; _surface.CapturePointer(e.Pointer); Set(e.GetCurrentPoint(_surface).Position.X); e.Handled = true; };
        _surface.PointerMoved += (_, e) => { if (_dragging) { Set(e.GetCurrentPoint(_surface).Position.X); e.Handled = true; } };
        _surface.PointerReleased += (_, e) => { _dragging = false; _surface.ReleasePointerCaptures(); e.Handled = true; };
        _surface.PointerCaptureLost += (_, _) => _dragging = false;
        KeyDown += (_, e) => { if (e.Key is VirtualKey.Left or VirtualKey.Right) { Value += e.Key == VirtualKey.Left ? -0.05 : 0.05; ValueChanged?.Invoke(Value); e.Handled = true; } };
    }
}
