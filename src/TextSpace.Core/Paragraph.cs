using System.Text.Json.Serialization;

namespace TextSpace.Core;

public sealed class Paragraph : Block
{
    public ParagraphFormat Format { get; set; } = new();
    public TextStyle DefaultStyle { get; set; } = new();
    public List<TextRun> Runs { get; set; } = [];
    [JsonIgnore] public string Text => string.Concat(Runs.Select(r => r.Text));
    [JsonIgnore] public int Length => Runs.Sum(r => r.Text.Length);

    public Paragraph() { }
    public Paragraph(string text, TextStyle? style = null, ParagraphFormat? format = null)
    {
        DefaultStyle = style ?? new(); Format = format ?? new();
        if (text.Length > 0) Runs.Add(new(text, DefaultStyle));
    }
    public TextStyle StyleAt(int offset)
    {
        if (Runs.Count == 0) return DefaultStyle;
        offset = Math.Clamp(offset, 0, Math.Max(0, Length - 1));
        foreach (var run in Runs) { if (offset < run.Text.Length) return run.Style; offset -= run.Text.Length; }
        return Runs[^1].Style;
    }
    public List<TextRun> Slice(int start, int length)
    {
        var result = new List<TextRun>(); var end = start + length; var position = 0;
        foreach (var run in Runs)
        {
            var from = Math.Max(start, position); var to = Math.Min(end, position + run.Text.Length);
            if (to > from) result.Add(new(run.Text.Substring(from - position, to - from), run.Style));
            position += run.Text.Length;
        }
        return result;
    }
    public void Normalize()
    {
        var normalized = new List<TextRun>();
        foreach (var run in Runs.Where(r => r.Text.Length > 0))
        {
            if (normalized.Count > 0 && normalized[^1].Style == run.Style) normalized[^1].Text += run.Text;
            else normalized.Add(new(run.Text, run.Style));
        }
        Runs = normalized;
    }
}
