using SkiaSharp;
using TextSpace.Core;
using TextSpace.Layout;

namespace TextSpace.Skia;

public sealed partial class DocumentRenderer
{
    private VisualLayoutCache? _visualLayouts;
    public VisualLayoutCache VisualLayouts => _visualLayouts ??= new(Metrics);
    public void ClearVisualLayouts() => _visualLayouts?.Clear();
    public void InvalidateVisualLayout(string id) => _visualLayouts?.Invalidate(id);
    public EquationLayout MeasureEquation(EquationNode root, double fontSize = 18) => new EquationLayouter(Metrics).Layout(root, fontSize);
    public EquationLayout MeasureEquation(EquationBlock equation) => VisualLayouts.GetEquation(equation);

    private void DrawVisualObjects(SKCanvas canvas, LayoutPage page, RenderOptions options, bool floating)
    {
        // Older embedding hosts may provide only the legacy Images list.
        IEnumerable<LayoutObject> objects = page.Objects.Count > 0 ? page.Objects : page.Images.Select(i => new LayoutObject(i.Image, i.Bounds) { IsReplica = i.IsReplica });
        foreach (var item in objects)
            if (item.Object.Placement.Floating == floating && item.Object.Id != options.HiddenObjectId)
                DrawVisualBlock(canvas, item.Object, item.Bounds);
    }

    /// <summary>Draws a retained visual, including its non-destructive transform and source crop.</summary>
    public void DrawVisualBlock(SKCanvas canvas, VisualBlock block, RectD bounds)
    {
        canvas.Save();
        try
        {
            canvas.Translate((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y + bounds.Height / 2));
            canvas.RotateDegrees((float)block.Placement.Rotation);
            canvas.Scale(block.Placement.FlipHorizontal ? -1 : 1, block.Placement.FlipVertical ? -1 : 1);
            canvas.Translate((float)(-bounds.Width / 2), (float)(-bounds.Height / 2));
            switch (block)
            {
                case ImageBlock image: DrawPicture(canvas, image, bounds.Width, bounds.Height); break;
                case ShapeBlock shape: DrawShape(canvas, shape, bounds.Width, bounds.Height); break;
                case EquationBlock equation:
                    var layout = MeasureEquation(equation); var scale = Math.Min(bounds.Width / Math.Max(1, layout.Width), bounds.Height / Math.Max(1, layout.Height));
                    canvas.Translate((float)((bounds.Width - layout.Width * scale) / 2), (float)((bounds.Height - layout.Height * scale) / 2));
                    canvas.Scale((float)scale); DrawEquation(canvas, layout, equation.Color); break;
            }
        }
        finally { canvas.Restore(); }
    }

    private void DrawPicture(SKCanvas canvas, ImageBlock image, double width, double height)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        if (!_images.TryGetValue(image.Id, out var bitmap))
        {
            bitmap = SKBitmap.Decode(image.Data); if (bitmap is not null) _images[image.Id] = bitmap;
        }
        var destination = SKRect.Create((float)width, (float)height);
        if (bitmap is not null)
        {
            var crop = image.Crop; var source = new SKRect((float)(bitmap.Width * crop.Left), (float)(bitmap.Height * crop.Top),
                (float)(bitmap.Width * (1 - crop.Right)), (float)(bitmap.Height * (1 - crop.Bottom)));
            canvas.DrawBitmap(bitmap, source, destination, paint);
        }
        else
        {
            paint.Color = Color("#EEEEEE"); canvas.DrawRect(destination, paint); paint.Color = Color("#666666");
            canvas.Save(); canvas.ClipRect(destination); Metrics.Draw(canvas, image.AltText, 8, 18, new(), paint); canvas.Restore();
        }
    }

    private void DrawShape(SKCanvas canvas, ShapeBlock shape, double width, double height)
    {
        canvas.Save(); canvas.Scale((float)(width / shape.Width), (float)(height / shape.Height));
        try
        {
            var w = (float)shape.Width; var h = (float)shape.Height;
            using var path = new SKPath();
            switch (shape.Kind)
            {
                case ShapeKind.Ellipse: path.AddOval(SKRect.Create(w, h)); break;
                case ShapeKind.RoundedRectangle: path.AddRoundRect(SKRect.Create(w, h), (float)Math.Min(shape.CornerRadius, Math.Min(w, h) / 2), (float)Math.Min(shape.CornerRadius, Math.Min(w, h) / 2)); break;
                case ShapeKind.Diamond: path.MoveTo(w / 2, 0); path.LineTo(w, h / 2); path.LineTo(w / 2, h); path.LineTo(0, h / 2); path.Close(); break;
                case ShapeKind.Triangle: path.MoveTo(w / 2, 0); path.LineTo(w, h); path.LineTo(0, h); path.Close(); break;
                case ShapeKind.Line: case ShapeKind.Arrow: path.MoveTo(0, h / 2); path.LineTo(w, h / 2); break;
                default: path.AddRect(SKRect.Create(w, h)); break;
            }
            using var paint = new SKPaint { IsAntialias = true, StrokeWidth = (float)shape.StrokeWidth, StrokeJoin = SKStrokeJoin.Round };
            if (shape.Fill is not null && shape.Kind is not (ShapeKind.Line or ShapeKind.Arrow))
            { paint.Color = Color(shape.Fill); paint.Style = SKPaintStyle.Fill; canvas.DrawPath(path, paint); }
            if (shape.StrokeWidth > 0)
            {
                paint.Color = Color(shape.Stroke); paint.Style = SKPaintStyle.Stroke; canvas.DrawPath(path, paint);
                if (shape.Kind == ShapeKind.Arrow)
                {
                    var head = Math.Min(Math.Min(w / 3, h / 2), Math.Max(10, (float)shape.StrokeWidth * 4));
                    using var arrow = new SKPath(); arrow.MoveTo(w - head, h / 2 - head / 2); arrow.LineTo(w, h / 2); arrow.LineTo(w - head, h / 2 + head / 2); canvas.DrawPath(arrow, paint);
                }
            }
            if (shape.Text.Length == 0) return;
            var shapeLines = VisualLayouts.GetShape(shape);
            canvas.Save(); canvas.ClipRect(SKRect.Create((float)shape.Padding, (float)shape.Padding, Math.Max(1, w - (float)shape.Padding * 2), Math.Max(1, h - (float)shape.Padding * 2)));
            paint.Style = SKPaintStyle.Fill; paint.Color = Color(shape.TextStyle.Color);
            foreach (var line in shapeLines) Metrics.Draw(canvas, line.Text, line.X, line.Baseline, shape.TextStyle, paint);
            canvas.Restore();
        }
        finally { canvas.Restore(); }
    }

    /// <summary>Paint an already measured equation. Placeholder slots appear only while editing.</summary>
    public void DrawEquation(SKCanvas canvas, EquationLayout layout, string color = "#202020", bool showSlots = false, string? activeSlot = null, bool hideActiveText = false)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = Color(color), StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        if (showSlots)
        {
            foreach (var slot in layout.Slots)
            {
                paint.Style = SKPaintStyle.Fill; paint.Color = slot.Id == activeSlot ? Color("#DCEBFF") : Color("#F4F7FB"); canvas.DrawRect(Rect(slot.Bounds), paint);
                paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 0.5f; paint.Color = slot.Id == activeSlot ? Color("#185ABD") : Color("#BBC7D6"); canvas.DrawRect(Rect(slot.Bounds), paint);
            }
        }
        var active = activeSlot is null ? null : layout.Slots.FirstOrDefault(s => s.Id == activeSlot);
        paint.Style = SKPaintStyle.Fill;
        foreach (var glyph in layout.Glyphs)
        {
            if (glyph.Placeholder && !showSlots) continue;
            if (hideActiveText && active is not null && Math.Abs(active.Bounds.X - glyph.X) < 0.001 && Math.Abs(active.Bounds.Y - glyph.Y) < 0.001) continue;
            paint.Color = glyph.Placeholder ? Color("#8C9BAD") : Color(color);
            canvas.Save(); canvas.Translate((float)glyph.X, (float)glyph.Y); canvas.Scale((float)glyph.ScaleX, (float)glyph.ScaleY);
            Metrics.Draw(canvas, glyph.Text, 0, glyph.Ascent, glyph.Style, paint); canvas.Restore();
        }
        paint.Color = Color(color); paint.Style = SKPaintStyle.Stroke;
        foreach (var rule in layout.Rules)
        {
            if (rule.Points.Count < 2) continue;
            using var path = new SKPath(); path.MoveTo((float)rule.Points[0].X, (float)rule.Points[0].Y);
            foreach (var point in rule.Points.Skip(1)) path.LineTo((float)point.X, (float)point.Y);
            paint.StrokeWidth = (float)rule.Thickness; canvas.DrawPath(path, paint);
        }
    }
}
