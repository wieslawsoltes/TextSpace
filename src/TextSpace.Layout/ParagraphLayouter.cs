using System.Globalization;
using System.Text.RegularExpressions;
using TextSpace.Core;

namespace TextSpace.Layout;

public sealed class ParagraphLayouter(ITextMetrics metrics)
{
    public List<LayoutLine> Layout(Paragraph paragraph, double width, int globalStart)
    {
        var format = paragraph.Format; var listInset = format.List == ListKind.None ? 0 : 18;
        var left = Math.Max(0, format.LeftIndent) + listInset;
        var available = Math.Max(12, width - left - Math.Max(0, format.RightIndent));
        var lines = new List<LayoutLine>(); var chunks = new List<LayoutChunk>(); var used = 0d; var offset = globalStart;
        var defaultMetric = metrics.Measure("M", paragraph.DefaultStyle); var ascent = defaultMetric.Ascent; var descent = defaultMetric.Descent;
        var first = true;
        double Indent() => first ? format.FirstLineIndent : 0;
        void Finish(bool last = false)
        {
            var lineAvailable = Math.Max(12, available - Indent());
            var alignment = format.Alignment;
            var shift = alignment == TextAlignment.Center ? Math.Max(0, (lineAvailable - used) / 2) : alignment == TextAlignment.Right ? Math.Max(0, lineAvailable - used) : 0;
            var spaces = !last && alignment == TextAlignment.Justify ? chunks.Sum(c => c.Text.Count(ch => ch == ' ')) : 0;
            var extra = spaces > 0 ? Math.Max(0, lineAvailable - used) / spaces : 0;
            var x = left + Indent() + shift;
            foreach (var chunk in chunks) { chunk.X = x; chunk.Width += extra * chunk.Text.Count(ch => ch == ' '); x += chunk.Width; }
            var height = Math.Max(3, (ascent + descent) * format.LineSpacing);
            lines.Add(new() { ParagraphId = paragraph.Id, Start = chunks.Count > 0 ? chunks[0].Start : offset, End = chunks.Count > 0 ? chunks[^1].End : offset, X = left + Indent() + shift, Width = x - left - Indent() - shift, Height = height, Ascent = ascent + Math.Max(0, height - ascent - descent) / 2, LastInParagraph = last, Chunks = chunks, Format = format, DefaultStyle = paragraph.DefaultStyle });
            chunks = []; used = 0; ascent = defaultMetric.Ascent; descent = defaultMetric.Descent; first = false;
        }
        void Add(string value, TextStyle style, double? forcedWidth = null)
        {
            var measurement = metrics.Measure(value == "\t" ? " " : value, style);
            var w = forcedWidth ?? measurement.Width;
            chunks.Add(new() { Text = value, Style = style, Start = offset, Width = w, Carets = value == "\t" ? [0, w] : metrics.CaretPositions(value, style) });
            used += w; offset += value.Length; ascent = Math.Max(ascent, measurement.Ascent); descent = Math.Max(descent, measurement.Descent);
        }
        foreach (var run in paragraph.Runs)
        {
            foreach (Match match in Regex.Matches(run.Text, @"\u2028|\t| +|[^ \t\u2028]+"))
            {
                var token = match.Value;
                if (token == "\u2028") { Add(token, run.Style, 0); Finish(); continue; }
                var tokenWidth = token == "\t" ? 36 - used % 36 : metrics.Measure(token, run.Style).Width;
                var limit = Math.Max(12, available - Indent());
                if (used + tokenWidth > limit && chunks.Count > 0) { Finish(); limit = Math.Max(12, available - Indent()); }
                if (tokenWidth <= limit || token == "\t") { Add(token, run.Style, token == "\t" ? 36 - used % 36 : null); continue; }
                var boundaries = StringInfo.ParseCombiningCharacters(token).Append(token.Length).ToArray(); var at = 0;
                while (at < boundaries.Length - 1)
                {
                    var low = at + 1; var high = boundaries.Length - 1; var best = low;
                    while (low <= high)
                    {
                        var mid = (low + high) / 2; var w = metrics.Measure(token[boundaries[at]..boundaries[mid]], run.Style).Width;
                        if (w <= Math.Max(12, available - Indent()) - used) { best = mid; low = mid + 1; } else high = mid - 1;
                    }
                    Add(token[boundaries[at]..boundaries[best]], run.Style); at = best;
                    if (at < boundaries.Length - 1) Finish();
                }
            }
        }
        if (chunks.Count > 0 || lines.Count == 0) Finish(true); else lines[^1].LastInParagraph = true;
        return lines;
    }
}
