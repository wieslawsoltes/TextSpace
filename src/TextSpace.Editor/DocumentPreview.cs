using SkiaSharp;
using TextSpace.Core;
using TextSpace.Layout;
using TextSpace.Skia;
using Uno.WinUI.Graphics2DSK;

namespace TextSpace.Editor;

/// <summary>A reusable, non-editable page preview sharing the editor's font and rendering resources.</summary>
public sealed class DocumentPreview : SKCanvasElement
{
    public DocumentModel? Document { get; set; }
    public DocumentRenderer? Renderer { get; set; }
    public DocumentLayout? Layout { get; set; }
    public int PageIndex { get; set; }
    public DocumentPreview() { Width = 140; Height = 181; IsHitTestVisible = false; }
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (Document is null || Renderer is null) return;
        var layout = Layout ?? Renderer.Layout(Document);
        var settings = layout.Pages[Math.Clamp(PageIndex, 0, layout.Pages.Count - 1)].Settings;
        var scale = Math.Min(area.Width / settings.Width, area.Height / settings.Height);
        canvas.Clear(SKColors.Transparent); canvas.Save();
        canvas.Translate((float)(area.Width - settings.Width * scale) / 2, (float)(area.Height - settings.Height * scale) / 2);
        canvas.Scale((float)scale);
        Renderer.DrawPage(canvas, Document, layout, Math.Clamp(PageIndex, 0, layout.Pages.Count - 1), new() { ShowChanges = false, ShowComments = false });
        using var border = new SKPaint { Color = SKColor.Parse("#C8C8C8"), Style = SKPaintStyle.Stroke, StrokeWidth = (float)(1 / scale) };
        canvas.DrawRect(0, 0, (float)settings.Width, (float)settings.Height, border); canvas.Restore();
    }
}
