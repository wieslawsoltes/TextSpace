using SkiaSharp;
using TextSpace.Core;
using TextSpace.Layout;

namespace TextSpace.Skia;

public sealed partial class DocumentRenderer
{
    private void DrawTabLeader(SKCanvas canvas, LayoutChunk chunk, LayoutLine line, SKColor color)
    {
        if (chunk.TabLeader == TabLeader.None || chunk.Width <= 2) return;
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        var start = chunk.X + 1; var width = chunk.Width - 2;
        if (chunk.TabLeader is TabLeader.Underscore or TabLeader.Heavy)
        {
            paint.StrokeWidth = chunk.TabLeader == TabLeader.Heavy ? 1.5f : 0.6f;
            canvas.DrawLine((float)start, (float)(line.Baseline + 1), (float)(start + width), (float)(line.Baseline + 1), paint); return;
        }
        var glyph = chunk.TabLeader == TabLeader.Hyphen ? '-' : chunk.TabLeader == TabLeader.MiddleDot ? '·' : '.';
        var advance = Metrics.Measure(glyph.ToString(), chunk.Style).Width;
        if (advance <= 0) return;
        var count = Math.Clamp((int)(width / advance), 0, 4096); if (count == 0) return;
        canvas.Save(); canvas.ClipRect(SKRect.Create((float)start, (float)line.Y, (float)width, (float)line.Height));
        Metrics.Draw(canvas, new string(glyph, count), start + (width - count * advance) / 2, line.Baseline, chunk.Style, paint);
        canvas.Restore();
    }
    private static void DrawBarTabs(SKCanvas canvas, LayoutLine line)
    {
        if (line.BarTabs.Length == 0) return;
        using var paint = new SKPaint { IsAntialias = true, Color = Color(line.DefaultStyle.Color), StrokeWidth = 0.6f };
        foreach (var x in line.BarTabs) canvas.DrawLine((float)x, (float)line.Y, (float)x, (float)(line.Y + line.Height), paint);
    }
}
