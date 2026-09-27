using System.Globalization;
using TextSpace.Core;

namespace TextSpace.Layout;

public readonly record struct TextMeasurement(double Width, double Ascent, double Descent);
public interface ITextMetrics
{
    TextMeasurement Measure(string text, TextStyle style);
    double[] CaretPositions(string text, TextStyle style);
}

/// <summary>Dependency-free deterministic metrics for tests and headless document analysis.</summary>
public sealed class MonospaceTextMetrics : ITextMetrics
{
    public TextMeasurement Measure(string text, TextStyle style) => new(new StringInfo(text).LengthInTextElements * style.EffectiveSize * 0.55, style.EffectiveSize * 0.8, style.EffectiveSize * 0.25);
    public double[] CaretPositions(string text, TextStyle style)
    {
        var positions = new double[text.Length + 1];
        for (var i = 1; i <= text.Length; i++) positions[i] = Measure(text[..i], style).Width;
        return positions;
    }
}
