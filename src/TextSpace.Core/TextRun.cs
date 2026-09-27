namespace TextSpace.Core;

public sealed class TextRun
{
    public string Text { get; set; } = "";
    public TextStyle Style { get; set; } = new();
    public TextRun() { }
    public TextRun(string text, TextStyle? style = null) { Text = text; Style = style ?? new(); }
}
