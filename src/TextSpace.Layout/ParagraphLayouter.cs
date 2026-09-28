using System.Globalization;
using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Rich-run-aware line breaking, explicit tab alignment and discretionary hyphenation in points.</summary>
public sealed class ParagraphLayouter(ITextMetrics metrics)
{
    private enum TokenKind { Word, Space, Tab, Break }
    private readonly record struct Token(int Start, int Length, TokenKind Kind);
    private readonly record struct RunRange(int Start, int End, TextStyle Style);
    private readonly record struct Part(int Start, string Text, TextStyle Style);

    public List<LayoutLine> Layout(Paragraph paragraph, double width, int globalStart, double defaultTabStop = 36)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        if (!double.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(defaultTabStop) || defaultTabStop is < 1 or > 720) throw new ArgumentOutOfRangeException(nameof(defaultTabStop));
        var format = paragraph.Format;
        var left = Math.Max(0, format.LeftIndent) + (format.List == ListKind.None ? 0 : 18);
        var available = Math.Max(12, width - left - Math.Max(0, format.RightIndent));
        var text = paragraph.Text; var tokens = Tokenize(text); var ranges = new List<RunRange>(); var runAt = 0;
        foreach (var run in paragraph.Runs) { if (run.Text.Length > 0) ranges.Add(new(runAt, runAt + run.Text.Length, run.Style)); runAt += run.Text.Length; }
        var stops = format.TabStops.Select(s => (Stop: s, Position: s.Resolve(width, format.RightIndent))).OrderBy(s => s.Position).ToArray();
        var bars = stops.Where(s => s.Stop.Alignment == TabAlignment.Bar && s.Position >= 0 && s.Position <= width).Select(s => s.Position).ToArray();
        var lines = new List<LayoutLine>(); var chunks = new List<LayoutChunk>(); var used = 0d; var offset = globalStart; var first = true;
        var defaultMetric = metrics.Measure("M", paragraph.DefaultStyle); var ascent = defaultMetric.Ascent; var descent = defaultMetric.Descent;
        double Indent() => first ? format.FirstLineIndent : 0;
        double Limit() => Math.Max(12, available - Indent());

        IEnumerable<Part> Parts(int start, int length)
        {
            var end = start + length; var low = 0; var high = ranges.Count;
            while (low < high) { var mid = low + (high - low) / 2; if (ranges[mid].End <= start) low = mid + 1; else high = mid; }
            for (var i = low; i < ranges.Count && ranges[i].Start < end; i++)
            {
                var range = ranges[i]; var from = Math.Max(start, range.Start); var to = Math.Min(end, range.End);
                if (to > from) yield return new(from, text.Substring(from, to - from), range.Style);
            }
        }
        double Measure(int start, int length, bool hyphen = false)
        {
            var total = 0d;
            foreach (var part in Parts(start, length)) total += metrics.Measure(Display(part.Text, hyphen && part.Start + part.Text.Length == start + length), part.Style).Width;
            return total;
        }
        void Add(int start, int length, bool hyphen = false)
        {
            foreach (var part in Parts(start, length))
            {
                var display = Display(part.Text, hyphen && part.Start + part.Text.Length == start + length);
                var measurement = metrics.Measure(display, part.Style); var displayCarets = metrics.CaretPositions(display, part.Style);
                var carets = displayCarets;
                if (display != part.Text)
                {
                    carets = new double[part.Text.Length + 1]; var visible = 0;
                    for (var i = 0; i < part.Text.Length; i++)
                    {
                        if (part.Text[i] is not ('\u00ad' or '\u200b') || (hyphen && part.Start + i + 1 == start + length && part.Text[i] == '\u00ad')) visible++;
                        carets[i + 1] = displayCarets[Math.Min(visible, displayCarets.Length - 1)];
                    }
                }
                chunks.Add(new() { Text = part.Text, DisplayText = display == part.Text ? null : display, Style = part.Style,
                    Start = globalStart + part.Start, Width = measurement.Width, Carets = carets });
                used += measurement.Width; offset = globalStart + part.Start + part.Text.Length;
                ascent = Math.Max(ascent, measurement.Ascent); descent = Math.Max(descent, measurement.Descent);
            }
        }
        void Finish(bool last = false)
        {
            var tabbed = chunks.Any(c => c.Text == "\t"); var alignment = tabbed ? TextAlignment.Left : format.Alignment;
            var shift = alignment == TextAlignment.Center ? Math.Max(0, (Limit() - used) / 2) : alignment == TextAlignment.Right ? Math.Max(0, Limit() - used) : 0;
            var spaces = !last && alignment == TextAlignment.Justify ? chunks.Sum(c => c.Text.Count(ch => ch == ' ')) : 0;
            var extra = spaces > 0 ? Math.Max(0, Limit() - used) / spaces : 0;
            var x = left + Indent() + shift;
            foreach (var chunk in chunks) { chunk.X = x; chunk.Width += extra * chunk.Text.Count(ch => ch == ' '); x += chunk.Width; }
            var height = Math.Max(3, (ascent + descent) * format.LineSpacing);
            lines.Add(new() { ParagraphId = paragraph.Id, Start = chunks.Count > 0 ? chunks[0].Start : offset, End = chunks.Count > 0 ? chunks[^1].End : offset,
                X = left + Indent() + shift, Width = x - left - Indent() - shift, Height = height, Ascent = ascent + Math.Max(0, height - ascent - descent) / 2,
                LastInParagraph = last, Chunks = chunks, Format = format, DefaultStyle = paragraph.DefaultStyle, BarTabs = (double[])bars.Clone() });
            chunks = []; used = 0; ascent = defaultMetric.Ascent; descent = defaultMetric.Descent; first = false;
        }
        (double Advance, TabLeader Leader) Tab(int tokenIndex)
        {
            var current = left + Indent() + used;
            var start = tokens[tokenIndex].Start + 1; var end = start;
            for (var i = tokenIndex + 1; i < tokens.Count && tokens[i].Kind is not (TokenKind.Tab or TokenKind.Break); i++) end = tokens[i].Start + tokens[i].Length;
            var following = Measure(start, end - start);
            foreach (var item in stops)
            {
                if (item.Stop.Alignment == TabAlignment.Bar || item.Position <= current + 0.001 || item.Position > width - format.RightIndent + 0.001) continue;
                var adjust = item.Stop.Alignment switch { TabAlignment.Right => following, TabAlignment.Center => following / 2, _ => 0 };
                if (item.Stop.Alignment == TabAlignment.Decimal)
                {
                    var decimalAt = text.IndexOf(item.Stop.DecimalCharacter, start, end - start);
                    adjust = decimalAt < 0 ? following : Measure(start, decimalAt - start);
                }
                var target = item.Position - adjust;
                if (target >= current - 0.001) return (Math.Max(0, target - current), item.Stop.Leader);
            }
            return (Math.Max(0.001, (Math.Floor((current + 0.001) / defaultTabStop) + 1) * defaultTabStop - current), TabLeader.None);
        }
        for (var tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            if (token.Kind == TokenKind.Break)
            {
                var style = Parts(token.Start, 1).First().Style;
                chunks.Add(new() { Text = "\u2028", DisplayText = "", Start = globalStart + token.Start, Style = style, Width = 0, Carets = [0, 0] });
                var metric = metrics.Measure("M", style); ascent = Math.Max(ascent, metric.Ascent); descent = Math.Max(descent, metric.Descent);
                offset = globalStart + token.Start + 1; Finish();
                if (tokenIndex == tokens.Count - 1) { ascent = metric.Ascent; descent = metric.Descent; }
                continue;
            }
            if (token.Kind == TokenKind.Tab)
            {
                var tab = Tab(tokenIndex);
                if (used + tab.Advance > Limit() && chunks.Count > 0) { Finish(); tab = Tab(tokenIndex); }
                var style = Parts(token.Start, 1).First().Style; var metric = metrics.Measure(" ", style);
                chunks.Add(new() { Text = "\t", Start = globalStart + token.Start, Style = style, Width = tab.Advance, Carets = [0, tab.Advance], TabLeader = tab.Leader });
                used += tab.Advance; offset = globalStart + token.Start + 1; ascent = Math.Max(ascent, metric.Ascent); descent = Math.Max(descent, metric.Descent); continue;
            }
            var at = token.Start; var end = at + token.Length;
            while (at < end)
            {
                if (Measure(at, end - at) <= Limit() - used + 0.0001) { Add(at, end - at); break; }
                // A soft hyphen is invisible unless used as the selected line-break opportunity.
                var candidates = new List<int>();
                for (var i = at; i < end; i++) if (text[i] == '\u00ad') candidates.Add(i + 1);
                var low = 0; var high = candidates.Count - 1; var split = -1;
                while (low <= high)
                {
                    var mid = low + (high - low) / 2;
                    if (Measure(at, candidates[mid] - at, true) <= Limit() - used) { split = candidates[mid]; low = mid + 1; } else high = mid - 1;
                }
                if (split > at) { Add(at, split - at, true); at = split; Finish(); continue; }
                if (chunks.Count > 0) { Finish(); continue; }
                // Oversized words still make progress without splitting a Unicode text element.
                var boundaries = StringInfo.ParseCombiningCharacters(text.Substring(at, end - at)).Append(end - at).ToArray();
                low = 1; high = boundaries.Length - 1; var best = low;
                while (low <= high)
                {
                    var mid = low + (high - low) / 2;
                    if (Measure(at, boundaries[mid]) <= Limit()) { best = mid; low = mid + 1; } else high = mid - 1;
                }
                Add(at, boundaries[best]); at += boundaries[best]; if (at < end) Finish();
            }
        }
        if (chunks.Count > 0 || lines.Count == 0 || tokens.Count > 0 && tokens[^1].Kind == TokenKind.Break) Finish(true);
        else lines[^1].LastInParagraph = true;
        return lines;
    }
    private static string Display(string text, bool hyphen)
    {
        if (!text.Contains('\u00ad') && !text.Contains('\u200b')) return text;
        var value = text.Replace("\u00ad", "").Replace("\u200b", "");
        return hyphen && text.EndsWith('\u00ad') ? value + "-" : value;
    }
    private static List<Token> Tokenize(string text)
    {
        static bool Space(char c) => char.IsWhiteSpace(c) && c is not ('\t' or '\u2028' or '\u00a0' or '\u202f');
        var tokens = new List<Token>(); var at = 0;
        while (at < text.Length)
        {
            var start = at; var c = text[at]; var kind = c == '\t' ? TokenKind.Tab : c == '\u2028' ? TokenKind.Break : Space(c) ? TokenKind.Space : TokenKind.Word;
            at++;
            if (kind == TokenKind.Space) { while (at < text.Length && Space(text[at])) at++; }
            else if (kind == TokenKind.Word)
            {
                while (at < text.Length && text[at] is not ('\t' or '\u2028') && !Space(text[at]))
                {
                    if (text[at - 1] is '-' or '\u2010' or '\u200b') break;
                    at++;
                }
            }
            tokens.Add(new(start, at - start, kind));
        }
        return tokens;
    }
}
