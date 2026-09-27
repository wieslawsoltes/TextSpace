using SkiaSharp;
using TextSpace.Core;

namespace TextSpace.Skia;

public sealed partial class SkiaTextMetrics
{
    private readonly record struct GlyphKey(string Text, string Family, double Size, bool Bold, bool Italic);
    private sealed record GlyphEntry(GlyphKey Key, SKTextBlob Blob, long EstimatedBytes);
    private readonly Dictionary<GlyphKey, LinkedListNode<GlyphEntry>> _glyphs = [];
    private readonly LinkedList<GlyphEntry> _glyphLru = [];
    private readonly int _maximumShapedRuns;
    private readonly long _maximumShapedBytes;
    public int CachedShapedRuns => _glyphs.Count;
    public long EstimatedShapedBytes { get; private set; }
    public long ShapedCacheHits { get; private set; }
    public long ShapedCacheMisses { get; private set; }

    /// <summary>Bounds reusable native glyph blobs independently of paragraph geometry and measurement caches.</summary>
    public SkiaTextMetrics(int maximumShapedRuns = 2048, long maximumShapedBytes = 16L * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumShapedRuns);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumShapedBytes);
        _maximumShapedRuns = maximumShapedRuns; _maximumShapedBytes = maximumShapedBytes;
    }
    /// <summary>Releases owned native glyph blobs. Run on the renderer's owning thread.</summary>
    public void ClearShapedText()
    {
        foreach (var entry in _glyphLru) entry.Blob.Dispose();
        _glyphLru.Clear(); _glyphs.Clear(); EstimatedShapedBytes = 0;
    }
    private void RemoveGlyph(LinkedListNode<GlyphEntry> node)
    {
        _glyphs.Remove(node.Value.Key); _glyphLru.Remove(node);
        EstimatedShapedBytes -= node.Value.EstimatedBytes; node.Value.Blob.Dispose();
    }
    private void DrawCachedText(SKCanvas canvas, string text, double x, double baseline, TextStyle style, SKPaint paint)
    {
        // Color, decoration, hyperlink and baseline shifts do not change glyph identity.
        var key = new GlyphKey(text, style.FontFamily, style.EffectiveSize, style.Bold, style.Italic);
        if (_glyphs.TryGetValue(key, out var node))
        {
            ShapedCacheHits++; _glyphLru.Remove(node); _glyphLru.AddFirst(node);
            canvas.DrawText(node.Value.Blob, (float)x, (float)baseline, paint); return;
        }
        ShapedCacheMisses++;
        var font = Font(style); var shaped = Shaper(style).Shape(text, font);
        if (shaped.Codepoints.Length == 0) return;
        using var builder = new SKTextBlobBuilder();
        var run = builder.AllocateRawPositionedRun(font, shaped.Codepoints.Length, null);
        for (var i = 0; i < shaped.Codepoints.Length; i++) { run.Glyphs[i] = checked((ushort)shaped.Codepoints[i]); run.Positions[i] = shaped.Points[i]; }
        SKTextBlob? blob = builder.Build();
        if (blob is null) return;
        try
        {
            canvas.DrawText(blob, (float)x, (float)baseline, paint);
            var bytes = 256L + text.Length * sizeof(char) + shaped.Codepoints.Length * 16L;
            if (_maximumShapedRuns == 0 || bytes > _maximumShapedBytes) return;
            while (_glyphLru.Last is { } last && (_glyphs.Count >= _maximumShapedRuns || EstimatedShapedBytes > _maximumShapedBytes - bytes)) RemoveGlyph(last);
            var entry = new GlyphEntry(key, blob, bytes); var added = _glyphLru.AddFirst(entry);
            _glyphs.Add(key, added); EstimatedShapedBytes += bytes; blob = null; // cache owns the native resource
        }
        finally { blob?.Dispose(); }
    }
}
