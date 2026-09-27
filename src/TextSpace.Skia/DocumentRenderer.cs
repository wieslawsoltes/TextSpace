using SkiaSharp;
using TextSpace.Core;
using TextSpace.Layout;

namespace TextSpace.Skia;

public sealed record RenderOptions
{
    public TextSelection Selection { get; init; }
    public bool DrawCaret { get; init; }
    public bool ShowFormatting { get; init; }
    public bool ShowComments { get; init; } = true;
    public bool ShowChanges { get; init; } = true;
    public bool ShowBoundaries { get; init; }
    public string? SelectedImageId { get; init; }
}

public sealed class DocumentRenderer : IDisposable
{
    private readonly Dictionary<string, SKBitmap> _images = [];
    public SkiaTextMetrics Metrics { get; } = new();
    public DocumentLayout Layout(DocumentModel document) => new PageLayoutEngine(Metrics).Layout(document);
    public static SKColor Color(string? text, string fallback = "#202020") => SKColor.TryParse(text ?? fallback, out var color) ? color : SKColor.Parse(fallback);
    private static SKRect Rect(RectD r) => SKRect.Create((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height);
    public void ClearImages() { foreach (var bitmap in _images.Values) bitmap.Dispose(); _images.Clear(); }
    public void DrawPage(SKCanvas canvas, DocumentModel document, DocumentLayout layout, int pageIndex, RenderOptions? options = null)
    {
        options ??= new(); var page = layout.Pages[pageIndex]; var settings = page.Settings;
        using var paint = new SKPaint { IsAntialias = true, Color = Color(settings.Color, "#FFFFFF") };
        canvas.DrawRect(0, 0, (float)settings.Width, (float)settings.Height, paint);
        canvas.Save(); canvas.ClipRect(SKRect.Create(0, 0, (float)settings.Width, (float)settings.Height));
        if (!string.IsNullOrEmpty(settings.Watermark))
        {
            canvas.Save(); canvas.Translate((float)settings.Width / 2, (float)settings.Height / 2); canvas.RotateDegrees(-40);
            var style = new TextStyle { FontSize = 64, Color = "#EEEEEE" }; paint.Color = Color(style.Color);
            Metrics.Draw(canvas, settings.Watermark, -Metrics.Measure(settings.Watermark, style).Width / 2, 0, style, paint); canvas.Restore();
        }
        if (options.ShowBoundaries)
        {
            paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 0.4f; paint.Color = Color("#CDD6E0"); canvas.DrawRect((float)settings.MarginLeft, (float)settings.MarginTop, (float)settings.ContentWidth, (float)settings.ContentHeight, paint); paint.Style = SKPaintStyle.Fill;
        }
        foreach (var cell in page.Cells)
        {
            if (cell.Fill is not null) { paint.Color = Color(cell.Fill); canvas.DrawRect(Rect(cell.Bounds), paint); }
            paint.Color = Color("#A8B7C8"); paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 0.55f; canvas.DrawRect(Rect(cell.Bounds), paint); paint.Style = SKPaintStyle.Fill;
        }
        foreach (var image in page.Images)
        {
            if (!_images.TryGetValue(image.Image.Id, out var bitmap))
            {
                bitmap = SKBitmap.Decode(image.Image.Data); if (bitmap is not null) _images[image.Image.Id] = bitmap;
            }
            if (bitmap is not null) { paint.Color = SKColors.White; canvas.DrawBitmap(bitmap, Rect(image.Bounds), paint); }
            else { paint.Color = Color("#EEEEEE"); canvas.DrawRect(Rect(image.Bounds), paint); paint.Color = Color("#666666"); Metrics.Draw(canvas, image.Image.AltText, image.Bounds.X + 8, image.Bounds.Y + 18, new(), paint); }
            if (options.SelectedImageId == image.Image.Id)
            {
                paint.Color = Color("#185ABD"); paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 1; canvas.DrawRect(Rect(image.Bounds), paint); paint.Style = SKPaintStyle.Fill;
                foreach (var point in new[] { new SKPoint((float)image.Bounds.X, (float)image.Bounds.Y), new SKPoint((float)image.Bounds.Right, (float)image.Bounds.Y), new SKPoint((float)image.Bounds.X, (float)image.Bounds.Bottom), new SKPoint((float)image.Bounds.Right, (float)image.Bounds.Bottom) }) canvas.DrawRect(point.X - 3, point.Y - 3, 6, 6, paint);
            }
        }
        foreach (var line in page.Lines)
        {
            if (line.Format.Shading is not null) { paint.Color = Color(line.Format.Shading); canvas.DrawRect((float)line.X, (float)line.Y, (float)Math.Max(1, line.Width), (float)line.Height, paint); }
            foreach (var chunk in line.Chunks)
            {
                if (chunk.Style.Highlight is not null) { paint.Color = Color(chunk.Style.Highlight); canvas.DrawRect((float)chunk.X, (float)line.Y, (float)chunk.Width, (float)line.Height, paint); }
                var start = Math.Max(chunk.Start, options.Selection.Start); var end = Math.Min(chunk.End, options.Selection.End);
                if (end > start)
                {
                    paint.Color = Color("#B5D7FA"); var x = chunk.Position(start - chunk.Start); var right = chunk.Position(end - chunk.Start);
                    canvas.DrawRect((float)x, (float)line.Y, (float)Math.Max(0.5, right - x), (float)line.Height, paint);
                }
                paint.Color = Color(chunk.Style.Color); Metrics.Draw(canvas, chunk.Text, chunk.X, line.Baseline, chunk.Style, paint);
                if (chunk.Style.Underline || chunk.Style.Hyperlink is not null) { paint.StrokeWidth = 0.6f; canvas.DrawLine((float)chunk.X, (float)(line.Baseline + 1.5), (float)(chunk.X + chunk.Width), (float)(line.Baseline + 1.5), paint); }
                if (chunk.Style.StrikeThrough) { paint.StrokeWidth = 0.6f; canvas.DrawLine((float)chunk.X, (float)(line.Baseline - chunk.Style.EffectiveSize * 0.3), (float)(chunk.X + chunk.Width), (float)(line.Baseline - chunk.Style.EffectiveSize * 0.3), paint); }
                if (options.ShowFormatting && (chunk.Text.All(c => c == ' ') || chunk.Text == "\t"))
                {
                    paint.Color = Color("#8F9BAB"); Metrics.Draw(canvas, chunk.Text == "\t" ? "→" : "·", chunk.X, line.Baseline, chunk.Style, paint);
                }
            }
            if (line.Marker is not null) { paint.Color = Color(line.DefaultStyle.Color); Metrics.Draw(canvas, line.Marker, line.X - 14, line.Baseline, line.DefaultStyle, paint); }
            if (options.ShowFormatting && line.LastInParagraph) { paint.Color = Color("#8F9BAB"); Metrics.Draw(canvas, "¶", line.X + line.Width + 2, line.Baseline, line.DefaultStyle, paint); }
            if (line.Format.BorderBottom && line.LastInParagraph) { paint.Color = Color("#8E9EAD"); paint.StrokeWidth = 0.6f; canvas.DrawLine((float)line.X, (float)(line.Y + line.Height + 3), (float)(settings.Width - settings.MarginRight), (float)(line.Y + line.Height + 3), paint); }
            if (options.ShowComments && document.Comments.Any(c => !c.Resolved && c.Start >= line.Start && c.Start <= line.End))
            {
                paint.Color = Color("#185ABD"); canvas.DrawRoundRect((float)(settings.Width - settings.MarginRight + 12), (float)(line.Y + 2), 12, 9, 2, 2, paint);
            }
            if (options.ShowChanges && document.Changes.Any(c => c.Start >= line.Start && c.Start <= line.End || c.Start < line.Start && c.Start + c.Inserted.Length > line.Start))
            {
                paint.Color = Color("#C43E1C"); paint.StrokeWidth = 1.5f; canvas.DrawLine((float)(settings.MarginLeft - 14), (float)line.Y, (float)(settings.MarginLeft - 14), (float)(line.Y + line.Height), paint);
            }
        }
        if (options.ShowFormatting)
        {
            var markerStyle = new TextStyle { FontSize = 7, Color = "#8F9BAB" };
            foreach (var marker in page.Breaks)
            {
                paint.Color = Color(markerStyle.Color); paint.StrokeWidth = 0.4f;
                var width = Metrics.Measure(marker.Label, markerStyle).Width; var center = marker.X + marker.Width / 2;
                canvas.DrawLine((float)marker.X, (float)(marker.Y + 5), (float)Math.Max(marker.X, center - width / 2 - 4), (float)(marker.Y + 5), paint);
                canvas.DrawLine((float)Math.Min(marker.X + marker.Width, center + width / 2 + 4), (float)(marker.Y + 5), (float)(marker.X + marker.Width), (float)(marker.Y + 5), paint);
                Metrics.Draw(canvas, marker.Label, center - width / 2, marker.Y + 7, markerStyle, paint);
            }
        }
        var headerStyle = new TextStyle { FontSize = 8, Color = "#777777" }; paint.Color = Color(headerStyle.Color);
        string Fields(string text) => text.Replace("{PAGE}", DocumentSections.FormatNumber(page.PageNumber, page.Section.Options.NumberStyle))
            .Replace("{NUMPAGES}", layout.Pages.Count.ToString()).Replace("{SECTION}", (page.SectionIndex + 1).ToString())
            .Replace("{SECTIONPAGES}", page.SectionPageCount.ToString()).Replace("{TITLE}", document.Title).Replace("{AUTHOR}", document.Author);
        void Story(string text, bool footer)
        {
            if (string.IsNullOrEmpty(text)) return;
            var paragraph = new Paragraph
            {
                DefaultStyle = headerStyle, Runs = [new(Fields(text).Replace("\r\n", "\u2028").Replace('\n', '\u2028').Replace('\r', '\u2028'), headerStyle)],
                Format = new() { Alignment = footer ? TextAlignment.Center : TextAlignment.Left, SpaceAfter = 0, LineSpacing = 1 }
            };
            var lines = new ParagraphLayouter(Metrics).Layout(paragraph, settings.ContentWidth, 0);
            var y = footer ? settings.Height - settings.FooterDistance - lines.Sum(l => l.Height) + lines[^1].Height - lines[^1].Ascent : settings.HeaderDistance - lines[0].Ascent;
            canvas.Save();
            canvas.ClipRect(footer ? SKRect.Create((float)settings.MarginLeft, (float)(settings.Height - settings.MarginBottom), (float)settings.ContentWidth, (float)settings.MarginBottom)
                : SKRect.Create((float)settings.MarginLeft, 0, (float)settings.ContentWidth, (float)settings.MarginTop));
            foreach (var line in lines)
            {
                foreach (var chunk in line.Chunks) Metrics.Draw(canvas, chunk.Text, settings.MarginLeft + chunk.X, y + line.Ascent, chunk.Style, paint);
                y += line.Height;
            }
            canvas.Restore();
        }
        Story(page.Header, false); Story(page.Footer, true);
        if (options.DrawCaret && options.Selection.IsEmpty)
        {
            var caret = layout.Caret(options.Selection.Active); if (caret.PageIndex == pageIndex) { paint.Color = Color("#111111"); paint.StrokeWidth = 0.8f; canvas.DrawLine((float)caret.X, (float)caret.Y, (float)caret.X, (float)(caret.Y + caret.Height), paint); }
        }
        canvas.Restore();
    }
    public byte[] ExportPdf(DocumentModel document, DocumentLayout? layout = null)
    {
        layout ??= Layout(document); using var output = new MemoryStream();
        using (var pdf = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata { Title = document.Title, Author = document.Author, Creator = "TextSpace" }))
        {
            if (pdf is null) throw new NotSupportedException("PDF creation is not available in this Skia runtime.");
            for (var i = 0; i < layout.Pages.Count; i++) { var canvas = pdf.BeginPage((float)layout.Pages[i].Settings.Width, (float)layout.Pages[i].Settings.Height); DrawPage(canvas, document, layout, i, new() { ShowChanges = false, ShowComments = false }); pdf.EndPage(); }
            pdf.Close();
        }
        return output.ToArray();
    }
    public byte[] ExportPng(DocumentModel document, int pageIndex = 0, double scale = 2)
    {
        var layout = Layout(document); pageIndex = Math.Clamp(pageIndex, 0, layout.Pages.Count - 1);
        if (!double.IsFinite(scale) || scale is <= 0 or > 8) throw new ArgumentOutOfRangeException(nameof(scale));
        var settings = layout.Pages[pageIndex].Settings;
        var pixelWidth = (int)Math.Ceiling(settings.Width * scale); var pixelHeight = (int)Math.Ceiling(settings.Height * scale);
        if ((long)pixelWidth * pixelHeight > 64_000_000) throw new InvalidOperationException("The page image exceeds 64 megapixels.");
        using var surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight)) ?? throw new InvalidOperationException("The page image could not be allocated.");
        surface.Canvas.Scale((float)scale); DrawPage(surface.Canvas, document, layout, pageIndex, new() { ShowComments = false, ShowChanges = false });
        using var image = surface.Snapshot(); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }
    public void Dispose() { ClearImages(); Metrics.Dispose(); }
}
