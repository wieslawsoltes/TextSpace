using System.Globalization;
using TextSpace.Core;

namespace TextSpace.Layout;

public sealed record VisualTextLine(string Text, double X, double Y, double Baseline);

public static class VisualTextLayout
{
    /// <summary>Grapheme-safe bounded wrapping for an independent plain-text shape story.</summary>
    public static IReadOnlyList<VisualTextLine> Layout(ShapeBlock shape, ITextMetrics metrics)
    {
        var width = Math.Max(1, shape.Width - shape.Padding * 2); var height = Math.Max(1, shape.Height - shape.Padding * 2);
        var m = metrics.Measure("Mg", shape.TextStyle); var lineHeight = Math.Max(1, m.Ascent + m.Descent) * 1.15;
        var lines = new List<string>(); var maximum = Math.Max(1, (int)Math.Ceiling(height / lineHeight));
        foreach (var paragraph in shape.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (lines.Count >= maximum) break;
            if (paragraph.Length == 0) { lines.Add(""); continue; }
            var positions = StringInfo.ParseCombiningCharacters(paragraph).Append(paragraph.Length).ToArray(); var at = 0;
            while (at < positions.Length - 1 && lines.Count < maximum)
            {
                var low = at + 1; var high = positions.Length - 1; var fit = low;
                while (low <= high)
                {
                    var middle = low + (high - low) / 2;
                    if (metrics.Measure(paragraph[positions[at]..positions[middle]], shape.TextStyle).Width <= width)
                    { fit = middle; low = middle + 1; }
                    else high = middle - 1;
                }
                if (fit < positions.Length - 1)
                    for (var word = fit; word > at + 1; word--)
                        if (char.IsWhiteSpace(paragraph[positions[word - 1]])) { fit = word; break; }
                lines.Add(paragraph[positions[at]..positions[fit]]); at = fit;
            }
        }
        var used = lines.Count * lineHeight;
        var top = shape.Padding + (shape.VerticalAlignment == CellVerticalAlignment.Center ? Math.Max(0, (height - used) / 2)
            : shape.VerticalAlignment == CellVerticalAlignment.Bottom ? Math.Max(0, height - used) : 0);
        return lines.Select((line, i) => new VisualTextLine(line, shape.Padding, top + i * lineHeight, top + i * lineHeight + m.Ascent)).ToArray();
    }
}
