using System.Globalization;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using TextSpace.Core;
using TextSpace.Layout;

namespace TextSpace.Skia;

/// <summary>Single-threaded font/shaper cache. Hosts can register openly licensed or user-provided typefaces.</summary>
public sealed partial class SkiaTextMetrics : IVersionedTextMetrics, IDisposable
{
    private readonly Dictionary<(string Family, bool Bold, bool Italic), SKTypeface> _faces = [];
    private readonly Dictionary<(string Family, bool Bold, bool Italic), SKShaper> _shapers = [];
    private readonly Dictionary<TextStyle, SKFont> _fonts = [];
    private readonly Dictionary<(string Text, TextStyle Style), TextMeasurement> _measurements = [];
    private readonly HashSet<SKTypeface> _owned = [];
    public void Register(string family, bool bold, bool italic, byte[] data)
    {
        using var bytes = SKData.CreateCopy(data); var face = SKTypeface.FromData(bytes) ?? throw new InvalidDataException("Invalid font data.");
        var key = (family, bold, italic); if (_shapers.Remove(key, out var shaper)) shaper.Dispose();
        _faces[key] = face; _owned.Add(face); ClearMeasurements();
    }
    public long MetricsVersion { get; private set; }
    public void ClearMeasurements()
    {
        MetricsVersion++;
        ClearShapedText();
        foreach (var cachedShaper in _shapers.Values) cachedShaper.Dispose();
        _shapers.Clear();
        _measurements.Clear(); foreach (var font in _fonts.Values) font.Dispose(); _fonts.Clear();
    }
    private (string, bool, bool) Key(TextStyle style) => (style.FontFamily, style.Bold, style.Italic);
    public SKTypeface Typeface(TextStyle style)
    {
        var key = Key(style); if (_faces.TryGetValue(key, out var face)) return face;
        if (_faces.TryGetValue((style.FontFamily, false, false), out face)) return face;
        face = SKTypeface.FromFamilyName(style.FontFamily, style.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, style.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        if (face is null) face = SKTypeface.Default; else _owned.Add(face);
        _faces[key] = face; return face;
    }
    public SKFont Font(TextStyle style)
    {
        if (_fonts.TryGetValue(style, out var font)) return font;
        font = new(Typeface(style), (float)style.EffectiveSize) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true, Hinting = SKFontHinting.Normal };
        _fonts[style] = font; return font;
    }
    public SKShaper Shaper(TextStyle style)
    {
        var key = Key(style); if (_shapers.TryGetValue(key, out var shaper)) return shaper;
        shaper = new(Typeface(style)); _shapers[key] = shaper; return shaper;
    }
    public TextMeasurement Measure(string text, TextStyle style)
    {
        var key = (text, style); if (_measurements.TryGetValue(key, out var value)) return value;
        var font = Font(style); var fm = font.Metrics; var width = string.IsNullOrEmpty(text) ? 0 : Shaper(style).Shape(text, font).Width;
        value = new(width, -fm.Ascent, fm.Descent);
        if (_measurements.Count > 30_000) _measurements.Clear(); _measurements[key] = value; return value;
    }
    public double[] CaretPositions(string text, TextStyle style)
    {
        var carets = new double[text.Length + 1];
        var boundaries = StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();
        for (var i = 1; i < boundaries.Length; i++)
        {
            var end = boundaries[i]; var width = Measure(text[..end], style).Width;
            for (var j = boundaries[i - 1] + 1; j <= end; j++) carets[j] = width;
        }
        return carets;
    }
    public void Draw(SKCanvas canvas, string text, double x, double baseline, TextStyle style, SKPaint paint)
    {
        if (text.Length == 0 || text == "\t" || text == "\u2028") return;
        var shift = style.Superscript ? -style.FontSize * 0.32 : style.Subscript ? style.FontSize * 0.18 : 0;
        DrawCachedText(canvas, text, x, baseline + shift, style, paint);
    }
    public void Dispose()
    {
        ClearShapedText();
        foreach (var shaper in _shapers.Values) shaper.Dispose(); foreach (var font in _fonts.Values) font.Dispose(); foreach (var face in _owned) face.Dispose();
        _shapers.Clear(); _fonts.Clear(); _faces.Clear(); _owned.Clear(); _measurements.Clear();
    }
}
