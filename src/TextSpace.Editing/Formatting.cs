using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    public void FormatText(string label, Func<TextStyle, TextStyle> transform)
    {
        var style = transform(TypingStyle);
        if (Selection.IsEmpty) { TypingStyle = style; Notify(EditorChangeKind.Selection, label); return; }
        var index = Index; var selection = Selection;
        Execute(label, () =>
        {
            foreach (var address in index.Paragraphs)
            {
                var start = Math.Max(selection.Start, address.Start); var end = Math.Min(selection.End, address.End);
                if (end <= start) continue;
                var p = address.Paragraph; var before = p.Slice(0, start - address.Start);
                var middle = p.Slice(start - address.Start, end - start); var after = p.Slice(end - address.Start, address.End - end);
                foreach (var run in middle) run.Style = transform(run.Style);
                p.Runs = [.. before, .. middle, .. after]; p.Normalize();
            }
            TypingStyle = style;
        });
    }
    public void ToggleBold() { var value = !TypingStyle.Bold; FormatText("Bold", s => s with { Bold = value }); }
    public void ToggleItalic() { var value = !TypingStyle.Italic; FormatText("Italic", s => s with { Italic = value }); }
    public void ToggleUnderline() { var value = !TypingStyle.Underline; FormatText("Underline", s => s with { Underline = value }); }
    public void SetFontSize(double size) => FormatText("Font size", s => s with { FontSize = Math.Clamp(size, 1, 400) });
    public void SetFont(string family) => FormatText("Font", s => s with { FontFamily = family });
    public void FormatParagraph(string label, Func<ParagraphFormat, ParagraphFormat> transform)
    {
        var index = Index; var selection = Selection;
        Execute(label, () =>
        {
            foreach (var a in index.Paragraphs.Where(a => selection.IsEmpty ? Selection.Active >= a.Start && Selection.Active <= a.End : a.End >= selection.Start && a.Start < selection.End)) a.Paragraph.Format = transform(a.Paragraph.Format);
        });
    }
    public void ApplyStyle(string name)
    {
        var style = DocumentStyles.Find(name); var index = Index; var selection = Selection;
        Execute("Style: " + name, () =>
        {
            foreach (var a in index.Paragraphs.Where(a => selection.IsEmpty ? selection.Active >= a.Start && selection.Active <= a.End : a.End >= selection.Start && a.Start < selection.End))
            {
                a.Paragraph.Format = style.Paragraph with { StyleName = style.Name }; a.Paragraph.DefaultStyle = style.Character;
                foreach (var run in a.Paragraph.Runs) run.Style = style.Character with { Hyperlink = run.Style.Hyperlink };
            }
            TypingStyle = style.Character;
        });
    }
    public void ToggleList(ListKind kind) => FormatParagraph("List", p => p with { List = p.List == kind ? ListKind.None : kind });
    public void Indent(int direction) => FormatParagraph("Indent", p => p with { LeftIndent = Math.Clamp(p.LeftIndent + direction * 18, 0, Document.Page.ColumnWidth - 36), ListLevel = Math.Clamp(p.ListLevel + direction, 0, 8) });
    public void ChangeCase(bool upper)
    {
        if (Selection.IsEmpty) return;
        var start = Selection.Start; var text = SelectedText(); var changed = upper ? text.ToUpperInvariant() : text.ToLowerInvariant();
        Replace(start, Selection.Length, changed, "Change case"); SetSelection(start, start + changed.Length);
    }
    public void SetPage(Func<PageSettings, PageSettings> transform) => Execute("Page setup", () => Document.Page = transform(Document.Page));
}
