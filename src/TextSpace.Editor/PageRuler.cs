using SkiaSharp;
using TextSpace.Core;
using Uno.WinUI.Graphics2DSK;

namespace TextSpace.Editor;

public sealed class PageRuler : SKCanvasElement
{
    private bool _dragging;
    private string _handle = "";
    public double PageLeft { get; set; }
    public double Scale { get; set; } = 4d / 3;
    public PageSettings Page { get; set; } = new();
    public ParagraphFormat Paragraph { get; set; } = new();
    public SKTypeface? Typeface { get; set; }
    public event Action<string, double>? IndentChanged;
    public PageRuler()
    {
        Height = 25; AutomationProperties.SetName(this, "Horizontal ruler");
        PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(this).Position; var left = PageLeft + (Page.MarginLeft + Paragraph.LeftIndent) * Scale; var first = left + Paragraph.FirstLineIndent * Scale; var right = PageLeft + (Page.Width - Page.MarginRight - Paragraph.RightIndent) * Scale;
            _handle = Math.Abs(point.X - right) < 9 ? "right" : Math.Abs(point.X - first) < 9 && point.Y < 13 ? "first" : Math.Abs(point.X - left) < 9 ? "left" : "";
            if (_handle.Length == 0) return; _dragging = true; CapturePointer(e.Pointer); e.Handled = true;
        };
        PointerMoved += (_, e) => { if (!_dragging) return; var x = (e.GetCurrentPoint(this).Position.X - PageLeft) / Scale; var value = _handle == "right" ? Page.Width - Page.MarginRight - x : x - Page.MarginLeft - (_handle == "first" ? Paragraph.LeftIndent : 0); value = Math.Round(value / 3) * 3; Paragraph = _handle switch { "right" => Paragraph with { RightIndent = Math.Clamp(value, 0, Page.ColumnWidth - 24) }, "first" => Paragraph with { FirstLineIndent = Math.Clamp(value, -Paragraph.LeftIndent, Page.ColumnWidth - 24) }, _ => Paragraph with { LeftIndent = Math.Clamp(value, 0, Page.ColumnWidth - 24) } }; Invalidate(); e.Handled = true; };
        PointerReleased += (_, e) => { if (!_dragging) return; _dragging = false; ReleasePointerCaptures(); IndentChanged?.Invoke(_handle, _handle == "right" ? Paragraph.RightIndent : _handle == "first" ? Paragraph.FirstLineIndent : Paragraph.LeftIndent); e.Handled = true; };
        PointerCaptureLost += (_, _) => _dragging = false;
    }
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        canvas.Clear(SKColor.Parse("#E8E8E8")); using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        var left = PageLeft + Page.MarginLeft * Scale; var width = Page.ContentWidth * Scale;
        canvas.DrawRect((float)PageLeft, 5, (float)(Page.Width * Scale), 16, new SKPaint { Color = SKColor.Parse("#D7D7D7") });
        canvas.DrawRect((float)left, 5, (float)width, 16, paint);
        paint.Color = SKColor.Parse("#5F5F5F"); paint.StrokeWidth = 0.7f; using var font = new SKFont(Typeface ?? SKTypeface.Default, 9);
        for (var tick = -8; tick < Page.Width / 9; tick++)
        {
            var x = left + tick * 9 * Scale; if (x < PageLeft || x > PageLeft + Page.Width * Scale) continue;
            var major = tick % 8 == 0; var half = tick % 4 == 0; if (major && tick != 0) canvas.DrawText(Math.Abs(tick / 8).ToString(), (float)x - 2, 16, font, paint);
            else if (!major) canvas.DrawLine((float)x, 12, (float)x, half ? 18 : 15, paint);
        }
        void Triangle(double x, double y, bool down)
        {
            using var path = new SKPath(); path.MoveTo((float)x - 4, (float)y); path.LineTo((float)x + 4, (float)y); path.LineTo((float)x, (float)y + (down ? 5 : -5)); path.Close(); paint.Color = SKColor.Parse("#FFFFFF"); paint.Style = SKPaintStyle.Fill; canvas.DrawPath(path, paint); paint.Color = SKColor.Parse("#717171"); paint.Style = SKPaintStyle.Stroke; canvas.DrawPath(path, paint); paint.Style = SKPaintStyle.Fill;
        }
        Triangle(left + (Paragraph.LeftIndent + Paragraph.FirstLineIndent) * Scale, 4, true);
        Triangle(left + Paragraph.LeftIndent * Scale, 21, false);
        Triangle(PageLeft + (Page.Width - Page.MarginRight - Paragraph.RightIndent) * Scale, 21, false);
    }
}
