using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    private static TextStyle ValidateTypingStyle(TextStyle? style)
    {
        if (style is null || string.IsNullOrWhiteSpace(style.FontFamily) || style.FontFamily.Length > 256 ||
            !double.IsFinite(style.FontSize) || style.FontSize is < 1 or > 400)
            throw new ArgumentException("Character formatting requires a font family and a finite size between 1 and 400 points.");
        return style;
    }

    public void FormatText(string label, Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        EnsureWritable();
        var style = ValidateTypingStyle(transform(TypingStyle));
        if (Selection.IsEmpty)
        {
            TypingStyle = style;
            Notify(EditorChangeKind.Selection, label);
            return;
        }
        var index = Index;
        var selection = Selection;
        Execute(label, () =>
        {
            foreach (var address in index.Paragraphs)
            {
                var start = Math.Max(selection.Start, address.Start);
                var end = Math.Min(selection.End, address.End);
                if (end <= start) continue;
                var paragraph = address.Paragraph;
                var before = paragraph.Slice(0, start - address.Start);
                var middle = paragraph.Slice(start - address.Start, end - start);
                var after = paragraph.Slice(end - address.Start, address.End - end);
                foreach (var run in middle) run.Style = ValidateTypingStyle(transform(run.Style));
                paragraph.Runs = [.. before, .. middle, .. after];
                paragraph.Normalize();
            }
            TypingStyle = style;
        });
    }

    public void ToggleBold() { var value = !TypingStyle.Bold; FormatText("Bold", s => s with { Bold = value }); }
    public void ToggleItalic() { var value = !TypingStyle.Italic; FormatText("Italic", s => s with { Italic = value }); }
    public void ToggleUnderline() { var value = !TypingStyle.Underline; FormatText("Underline", s => s with { Underline = value }); }
    public void SetFontSize(double size)
    {
        if (!double.IsFinite(size)) throw new ArgumentOutOfRangeException(nameof(size), "Font size must be finite.");
        FormatText("Font size", s => s with { FontSize = Math.Clamp(size, 1, 400) });
    }
    public void SetFont(string family)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        FormatText("Font", s => s with { FontFamily = family.Trim() });
    }

    private IEnumerable<ParagraphAddress> SelectedParagraphs(TextIndex index, TextSelection selection) =>
        selection.IsEmpty
            ? [index.At(selection.Active)]
            : index.Paragraphs.Where(a => a.End >= selection.Start && a.Start < selection.End);

    public void FormatParagraph(string label, Func<ParagraphFormat, ParagraphFormat> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var index = Index;
        var selection = Selection;
        Execute(label, () =>
        {
            foreach (var address in SelectedParagraphs(index, selection))
                address.Paragraph.Format = transform(address.Paragraph.Format)
                    ?? throw new ArgumentException("Paragraph formatting cannot be null.", nameof(transform));
        });
    }

    public void ApplyStyle(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var style = DocumentStyles.Find(name);
        var index = Index;
        var selection = Selection;
        Execute("Style: " + name, () =>
        {
            foreach (var address in SelectedParagraphs(index, selection))
            {
                var paragraph = address.Paragraph;
                paragraph.Format = style.Paragraph with { StyleName = style.Name };
                paragraph.DefaultStyle = style.Character;
                foreach (var run in paragraph.Runs)
                    run.Style = style.Character with { Hyperlink = run.Style.Hyperlink };
            }
            TypingStyle = style.Character;
        });
    }

    public void ClearFormatting() => Execute("Clear all formatting", () =>
    {
        FormatText("Clear character formatting", _ => new());
        FormatParagraph("Clear paragraph formatting", _ => new());
    });

    public void ToggleList(ListKind kind) => FormatParagraph("List", p => p with { List = p.List == kind ? ListKind.None : kind });
    public void Indent(int direction) => FormatParagraph("Indent", p => p with
    {
        LeftIndent = Math.Clamp(p.LeftIndent + direction * 18, 0, Math.Max(0, CurrentSection.Page.ColumnWidth - 36)),
        ListLevel = Math.Clamp(p.ListLevel + direction, 0, 8)
    });

    /// <summary>Changes selected text without flattening character styles or table-cell boundaries.</summary>
    public void ChangeCase(bool upper)
    {
        EnsureWritable();
        if (Selection.IsEmpty) return;
        var selection = Selection;
        var segments = new List<(int Start, int Length, string Text, TextStyle Style)>();
        foreach (var address in Index.Paragraphs)
        {
            var start = Math.Max(selection.Start, address.Start);
            var end = Math.Min(selection.End, address.End);
            if (end <= start) continue;
            foreach (var run in address.Paragraph.Slice(start - address.Start, end - start))
            {
                var converted = upper ? run.Text.ToUpperInvariant() : run.Text.ToLowerInvariant();
                if (converted != run.Text) segments.Add((start, run.Text.Length, converted, run.Style));
                start += run.Text.Length;
            }
        }
        if (segments.Count == 0) return;
        Execute("Change case", () =>
        {
            var delta = 0;
            foreach (var segment in segments.AsEnumerable().Reverse())
            {
                TypingStyle = segment.Style;
                Replace(segment.Start, segment.Length, segment.Text, "Change case");
                delta += segment.Text.Length - segment.Length;
            }
            if (selection.Anchor <= selection.Active) SetSelection(selection.Start, selection.End + delta);
            else SetSelection(selection.End + delta, selection.Start);
        });
    }

    public void SetPage(Func<PageSettings, PageSettings> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        SetSection(section => section with { Page = transform(section.Page)
            ?? throw new ArgumentException("Page settings cannot be null.", nameof(transform)) });
    }
}
