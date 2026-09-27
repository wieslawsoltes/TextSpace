using System.Globalization;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private static string N(double n) => n.ToString("0.##", CultureInfo.InvariantCulture);
    private static OfficeComboField Choice(OfficeDialog dialog, string label, IEnumerable<string> choices, string value)
    {
        var field = new OfficeComboField(label, choices, 230) { Value = value }; dialog.Body.Children.Add(OfficeTheme.Column(OfficeTheme.Text(label), field)); return field;
    }
    private async Task RenameAsync()
    {
        var dialog = new OfficeDialog("Rename document", "Rename"); var title = dialog.AddField("Document name", Session.Document.Title);
        if (await ShowDialogAsync(dialog) && !string.IsNullOrWhiteSpace(title.Text)) Session.Execute("Rename document", () => Session.Document.Title = title.Text.Trim()[..Math.Min(200, title.Text.Trim().Length)]);
    }
    private async Task FontDialogAsync()
    {
        var style = Session.TypingStyle; var dialog = new OfficeDialog("Font", "Apply", 510);
        var family = Choice(dialog, "Font family", ["Aptos", "Calibri", "Arial", "Times New Roman", "Georgia", "Courier New", "Carlito", "Inter"], style.FontFamily);
        var size = dialog.AddField("Size (points)", N(style.FontSize));
        var bold = new OfficeCheckBox("Bold", style.Bold); var italic = new OfficeCheckBox("Italic", style.Italic); var underline = new OfficeCheckBox("Underline", style.Underline); var strike = new OfficeCheckBox("Strikethrough", style.StrikeThrough);
        dialog.Body.Children.Add(OfficeTheme.Row(bold, italic, underline, strike));
        var color = dialog.AddField("Font color (hex)", style.Color);
        dialog.AddDescription("Formatting applies to selected text, or to the next text you type. Fonts unavailable on this device use the bundled open-source substitutes.");
        if (!await ShowDialogAsync(dialog)) return;
        var fontSize = ParseNumber(size.Text); if (fontSize is < 1 or > 400) throw new InvalidOperationException("Font size must be between 1 and 400 points.");
        if (string.IsNullOrWhiteSpace(family.Value)) throw new InvalidOperationException("Enter a font family.");
        var hex = ValidColor(color.Text);
        Session.FormatText("Font", s => s with { FontFamily = family.Value, FontSize = fontSize, Bold = bold.IsChecked, Italic = italic.IsChecked, Underline = underline.IsChecked, StrikeThrough = strike.IsChecked, Color = hex });
    }
    private static string ValidColor(string text)
    {
        text = text.Trim(); if (text.Length != 7 || text[0] != '#' || !text[1..].All(Uri.IsHexDigit)) throw new InvalidOperationException("Enter a color in #RRGGBB format."); return text;
    }
    private async Task ParagraphDialogAsync()
    {
        var p = Session.CurrentParagraph.Format; var dialog = new OfficeDialog("Paragraph", "Apply", 520);
        var alignment = Choice(dialog, "Alignment", ["Left", "Center", "Right", "Justify"], p.Alignment.ToString());
        var left = dialog.AddField("Left indent (points)", N(p.LeftIndent)); var right = dialog.AddField("Right indent (points)", N(p.RightIndent)); var first = dialog.AddField("First line indent (negative for hanging)", N(p.FirstLineIndent));
        var before = dialog.AddField("Space before (points)", N(p.SpaceBefore)); var after = dialog.AddField("Space after (points)", N(p.SpaceAfter));
        var line = Choice(dialog, "Line spacing multiplier", ["1", "1.15", "1.5", "2", "2.5", "3"], N(p.LineSpacing));
        var keep = new OfficeCheckBox("Keep with next", p.KeepWithNext); var page = new OfficeCheckBox("Page break before", p.PageBreakBefore); dialog.Body.Children.Add(OfficeTheme.Row(keep, page));
        if (!await ShowDialogAsync(dialog)) return;
        if (!Enum.TryParse<TextSpace.Core.TextAlignment>(alignment.Value, true, out var a)) throw new InvalidOperationException("Choose a valid alignment.");
        var l = ParseNumber(left.Text); var r = ParseNumber(right.Text); var f = ParseNumber(first.Text); var b = ParseNumber(before.Text); var af = ParseNumber(after.Text); var spacing = ParseNumber(line.Value);
        if (l < 0 || r < 0 || l + r > Session.CurrentSection.Page.ColumnWidth - 24 || b is < 0 or > 720 || af is < 0 or > 720 || spacing is < 0.5 or > 10 || f < -l || f > Session.CurrentSection.Page.ColumnWidth - l - r - 12) throw new InvalidOperationException("Paragraph geometry is outside the available page width or spacing limits.");
        Session.FormatParagraph("Paragraph", format => format with { Alignment = a, LeftIndent = l, RightIndent = r, FirstLineIndent = f, SpaceBefore = b, SpaceAfter = af, LineSpacing = spacing, KeepWithNext = keep.IsChecked, PageBreakBefore = page.IsChecked });
    }
    private async Task PageSetupDialogAsync()
    {
        var page = Session.CurrentSection.Page; var dialog = new OfficeDialog("Page Setup", "Apply", 520); dialog.AddDescription("Measurements are in typographic points: 72 points = 1 inch. Settings apply to the current section.");
        var width = dialog.AddField("Paper width", N(page.Width)); var height = dialog.AddField("Paper height", N(page.Height));
        var top = dialog.AddField("Top margin", N(page.MarginTop)); var bottom = dialog.AddField("Bottom margin", N(page.MarginBottom)); var left = dialog.AddField("Left margin", N(page.MarginLeft)); var right = dialog.AddField("Right margin", N(page.MarginRight));
        var columns = Choice(dialog, "Columns", ["1", "2", "3"], page.Columns.ToString()); var gap = dialog.AddField("Column gap", N(page.ColumnGap));
        if (!await ShowDialogAsync(dialog)) return;
        Session.SetPage(p => p with { Width = ParseNumber(width.Text), Height = ParseNumber(height.Text), MarginTop = ParseNumber(top.Text), MarginBottom = ParseNumber(bottom.Text), MarginLeft = ParseNumber(left.Text), MarginRight = ParseNumber(right.Text), Columns = checked((int)ParseNumber(columns.Value)), ColumnGap = ParseNumber(gap.Text) });
    }
    private async Task HyperlinkDialogAsync()
    {
        var selection = Session.Selection; var dialog = new OfficeDialog("Insert Hyperlink", "Insert");
        var label = dialog.AddField("Text to display", Session.SelectedText()); var url = dialog.AddField("Address", Session.TypingStyle.Hyperlink ?? "https://");
        dialog.AddDescription("Supported links: HTTPS, HTTP, mailto, or a local #bookmark address. External content is never downloaded into the document.");
        if (!await ShowDialogAsync(dialog)) return;
        var target = url.Text.Trim(); if (!(target.StartsWith('#') && target.Length > 1) && !(Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")) throw new InvalidOperationException("Enter a valid HTTP, HTTPS, mailto or #bookmark address.");
        var text = string.IsNullOrEmpty(label.Text) ? target : label.Text;
        Session.Execute("Insert hyperlink", () => { Session.SetSelection(selection.Anchor, selection.Active); Session.InsertText(text); Session.SetSelection(selection.Start, selection.Start + text.Length); Session.FormatText("Hyperlink", s => s with { Hyperlink = target, Underline = true, Color = "#0563C1" }); });
    }
    private async Task HeaderFooterAsync(bool header)
    {
        var dialog = new OfficeDialog(header ? "Header" : "Footer", "Apply"); var text = dialog.AddField(header ? "Header text" : "Footer text", header ? Session.CurrentSection.Header ?? "" : Session.CurrentSection.Footer ?? "");
        dialog.AddDescription("Use {PAGE} for the current page number and {NUMPAGES} for the total. This is the default text for the current section. Section Settings provides first/even-page variants and linking.");
        if (await ShowDialogAsync(dialog)) Session.SetSection(section => header ? section with { Header = text.Text } : section with { Footer = text.Text });
    }
    private async Task DateDialogAsync()
    {
        var dialog = new OfficeDialog("Date and Time", "Insert"); var now = DateTime.Now;
        var choice = Choice(dialog, "Available formats", [now.ToString("D"), now.ToString("d"), now.ToString("yyyy-MM-dd"), now.ToString("f"), now.ToString("HH:mm")], now.ToString("D"));
        dialog.AddDescription("The date is inserted as editable text, not an automatically updating field."); if (await ShowDialogAsync(dialog)) Session.InsertText(choice.Value);
    }
    private async Task WatermarkAsync()
    {
        var dialog = new OfficeDialog("Printed Watermark", "Apply"); var text = dialog.AddField("Watermark text", Session.CurrentSection.Page.Watermark ?? "DRAFT"); dialog.AddDescription("Leave the field empty to remove the watermark. Watermarks are preserved in native files and PDF, not DOCX export.");
        if (await ShowDialogAsync(dialog)) Session.SetPage(p => p with { Watermark = string.IsNullOrWhiteSpace(text.Text) ? null : text.Text.Trim()[..Math.Min(80, text.Text.Trim().Length)] });
    }
    private async Task ZoomDialogAsync()
    {
        var dialog = new OfficeDialog("Zoom", "Apply", 390); var value = dialog.AddField("Percent", N(Surface.Zoom * 100)); dialog.AddDescription("Choose a zoom level from 25% to 500%.");
        if (await ShowDialogAsync(dialog)) Surface.SetZoom(ParseNumber(value.Text) / 100);
    }
    private async Task GoToAsync()
    {
        var dialog = new OfficeDialog("Go To", "Go", 390); var field = dialog.AddField("Page number", (Surface.Layout.Caret(Session.Selection.Active).PageIndex + 1).ToString()); dialog.AddDescription($"This document has {Surface.Layout.Pages.Count} pages.");
        if (await ShowDialogAsync(dialog)) Surface.ScrollToPage((int)ParseNumber(field.Text) - 1);
    }
    private async Task WordCountAsync()
    {
        var stats = WritingAnalysis.Statistics(Session.Document); var dialog = new OfficeDialog("Word Count", "Close", 410);
        foreach (var (label, value) in new[] { ("Pages", Surface.Layout.Pages.Count), ("Words", stats.Words), ("Characters (no spaces)", stats.CharactersWithoutSpaces), ("Characters (with spaces)", stats.Characters), ("Paragraphs", stats.Paragraphs), ("Sentences", stats.Sentences), ("Estimated reading time (minutes)", stats.ReadingMinutes) }) dialog.Body.Children.Add(OfficeTheme.Columns((OfficeTheme.Text(label), -1), (OfficeTheme.Text(value.ToString("N0"), 12, OfficeTheme.Ink, true), 0)));
        await ShowDialogAsync(dialog);
    }
    private async Task NewCommentAsync()
    {
        var dialog = new OfficeDialog("New Comment", "Post"); var selected = Session.SelectedText(); if (selected.Length > 0) dialog.AddDescription("Selected text: “" + selected[..Math.Min(180, selected.Length)] + "”");
        var field = dialog.AddField("Comment text", multiline: true);
        if (await ShowDialogAsync(dialog) && !string.IsNullOrWhiteSpace(field.Text)) { Session.AddComment(field.Text); _selectedCommentId = Session.Document.Comments.LastOrDefault()?.Id; SetReview(true, "Comments"); }
    }
    private async Task ReplyAsync(string id)
    {
        var dialog = new OfficeDialog("Reply to comment", "Post"); var field = dialog.AddField("Reply text", multiline: true);
        if (await ShowDialogAsync(dialog) && !string.IsNullOrWhiteSpace(field.Text)) Session.ReplyToComment(id, field.Text.Trim());
    }
    private async Task PictureSizeAsync()
    {
        var image = SelectedPicture() ?? throw new InvalidOperationException("Select a picture on the page first."); var dialog = new OfficeDialog("Picture Size", "Apply");
        var width = dialog.AddField("Width (points)", N(image.Width)); var height = dialog.AddField("Height (points)", N(image.Height)); var aspect = new OfficeCheckBox("Lock aspect ratio", true); dialog.Body.Children.Add(aspect);
        if (!await ShowDialogAsync(dialog)) return; var w = ParseNumber(width.Text); var h = aspect.IsChecked ? w * image.Height / image.Width : ParseNumber(height.Text);
        Session.Execute("Picture size", () => { image.Width = w; image.Height = h; });
    }
    private async Task PictureAltAsync()
    {
        var image = SelectedPicture() ?? throw new InvalidOperationException("Select a picture first."); var dialog = new OfficeDialog("Alt Text", "Apply"); var field = dialog.AddField("Describe this picture", image.AltText, true);
        if (await ShowDialogAsync(dialog)) Session.Execute("Picture alt text", () => image.AltText = field.Text);
    }
    private async Task TablePropertiesAsync()
    {
        var table = Session.CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first."); var dialog = new OfficeDialog("Table Properties", "Apply");
        dialog.AddDescription($"{table.Rows.Count} rows × {table.Rows[0].Cells.Count} columns. Tables fit the current text column."); var padding = dialog.AddField("Cell padding (points)", N(table.CellPadding));
        var widths = dialog.AddField("Relative column widths (comma-separated)", string.Join(", ", table.ColumnWidths.Select(N)));
        if (!await ShowDialogAsync(dialog)) return; var values = widths.Text.Split(',').Select(ParseNumber).ToList();
        if (values.Count != table.Rows.Max(r => r.Cells.Count) || values.Any(v => v <= 0)) throw new InvalidOperationException("Enter one positive relative width for each column.");
        Session.Execute("Table properties", () => { table.CellPadding = ParseNumber(padding.Text); table.ColumnWidths = values; });
    }
    private async Task SortDialogAsync()
    {
        if (Session.Selection.IsEmpty) throw new InvalidOperationException("Select two or more paragraphs to sort.");
        var dialog = new OfficeDialog("Sort Text", "Sort"); var order = Choice(dialog, "Order", ["Ascending", "Descending"], "Ascending"); dialog.AddDescription("Sorts selected paragraphs as text. Individual character formatting is normalized to the current typing style.");
        if (!await ShowDialogAsync(dialog)) return;
        var lines = Session.SelectedText().Split('\n'); var sorted = order.Value == "Descending" ? lines.OrderDescending(StringComparer.CurrentCultureIgnoreCase) : lines.Order(StringComparer.CurrentCultureIgnoreCase); Session.InsertText(string.Join("\n", sorted));
    }
    private async Task CaptionAsync()
    {
        var dialog = new OfficeDialog("Caption", "Insert"); var field = dialog.AddField("Caption text", "Figure 1. ");
        if (await ShowDialogAsync(dialog)) { Session.InsertText(field.Text + "\n"); var active = Session.Selection.Active; var previous = Session.Index.Paragraphs.LastOrDefault(p => p.End < active); if (previous is not null) { Session.SetSelection(previous.Start, previous.End); Session.ApplyStyle("Caption"); Session.SetSelection(active, active); } }
    }
    private Task InsertContentsAsync(bool update)
    {
        var count = Session.InsertTableOfContents(update, CreateFieldContext);
        Notify($"Generated {count} live contents entries. F9 updates text and pages; Update Table also rebuilds the heading list.");
        return Task.CompletedTask;
    }

    private static readonly (string Label, string Id)[] SearchableCommands =
    [
        ("Open a document", "open"), ("New blank document", "new"), ("Save a complete copy", "save"), ("Export Word document", "export-docx"), ("Export PDF", "export-pdf"), ("Export HTML", "export-html"), ("Export plain text", "export-text"), ("Export current page as PNG", "export-png"), ("Print", "print"),
        ("Section settings", "section-settings"), ("Insert a live field", "insert-field"), ("Update fields", "update-fields"), ("Manage fields", "manage-fields"), ("Insert cross-reference", "cross-reference"), ("Find text", "find"), ("Replace text", "replace"), ("Font settings", "font-dialog"), ("Paragraph settings", "paragraph-dialog"), ("Page setup", "page-setup"), ("Insert picture", "insert-picture"), ("Insert hyperlink", "link"), ("Insert table of contents", "toc"), ("New comment", "new-comment"), ("Track changes", "track"), ("Word count", "word-count"), ("Focus mode", "focus"), ("Zoom to page width", "page-width"), ("Keyboard shortcuts", "shortcuts")
    ];
    private async Task CommandSearchAsync()
    {
        var dialog = new OfficeDialog("Search commands", "Close", 510); var query = dialog.AddField("Search commands"); var results = new StackPanel { Spacing = 2 }; dialog.Body.Children.Add(results); string? chosen = null;
        void Refresh()
        {
            results.Children.Clear(); foreach (var command in SearchableCommands.Where(c => c.Label.Contains(query.Text, StringComparison.OrdinalIgnoreCase)).Take(10))
            {
                var selected = command; var button = new OfficeButton(command.Label, () => { chosen = selected.Id; dialog.Close(true); }) { HorizontalContentAlignment = HorizontalAlignment.Left, Height = 30 }; results.Children.Add(button);
            }
        }
        query.TextChanged += (_, _) => Refresh(); Refresh(); await ShowDialogAsync(dialog); if (chosen is not null) await ExecuteCommandAsync(chosen);
    }
    private async Task ShortcutsAsync()
    {
        var dialog = new OfficeDialog("Keyboard Shortcuts", "Close", 540);
        foreach (var (label, keys) in new[] { ("Bold / Italic / Underline", "Ctrl+B / Ctrl+I / Ctrl+U"), ("Undo / Redo", "Ctrl+Z / Ctrl+Y"), ("Save / Open / New / Print", "Ctrl+S / Ctrl+O / Ctrl+N / Ctrl+P"), ("Find / Replace / Hyperlink", "Ctrl+F / Ctrl+H / Ctrl+K"), ("Select all", "Ctrl+A"), ("Align left / center / right / justify", "Ctrl+L / Ctrl+E / Ctrl+R / Ctrl+J"), ("Page break / Soft line break", "Ctrl+Enter / Shift+Enter"), ("Next / Previous table cell", "Tab / Shift+Tab"), ("Select text", "Shift + arrow keys"), ("Move by word", "Ctrl + Left/Right"), ("Update live fields", "F9"), ("Zoom", "Ctrl + mouse wheel"), ("Leave focus mode", "Escape") }) dialog.Body.Children.Add(OfficeTheme.Columns((OfficeTheme.Text(label, 11), -1), (OfficeTheme.Text(keys, 11, OfficeTheme.Muted), 0)));
        dialog.AddDescription("On macOS, Command is also recognized for editor shortcuts. Browser-reserved shortcuts may require the document canvas to have focus."); await ShowDialogAsync(dialog);
    }
    private async Task HelpAsync()
    {
        var dialog = new OfficeDialog("Welcome to TextSpace", "Start writing", 540);
        dialog.AddDescription("Click on the paper to write. Drag to select text, double-click to select a word, and use the ribbon to format your document. Tab moves between table cells.");
        dialog.AddDescription("File opens templates, import, export, and local version history. AutoSave writes to this device only. Download .textspace files for complete copies; DOCX and PDF make your work portable.");
        dialog.AddDescription("This is an independent development release, not Microsoft Word. It does not yet support full Word layout, collaboration, macros, equations, footnotes, floating shapes, or lossless DOCX round-tripping. Disabled commands indicate unsupported features rather than simulated results.");
        await ShowDialogAsync(dialog);
    }
    private async Task AboutAsync()
    {
        var dialog = new OfficeDialog("About TextSpace", "Close", 530);
        dialog.Body.Children.Add(OfficeTheme.Text("TextSpace", 30, OfficeTheme.Accent, true));
        dialog.AddDescription("A local-first word processor built with Uno Platform, .NET, SkiaSharp and HarfBuzz. Version 0.2.0-alpha.1.");
        dialog.AddDescription("Original office-style controls and reusable document libraries. Open-source font substitutes are included; Microsoft fonts and branding are not distributed.");
        dialog.AddDescription("TextSpace is not affiliated with Microsoft. Microsoft Word and Microsoft 365 are trademarks of Microsoft. Source code: MIT License.");
        dialog.AddDescription("Recovery storage: " + Host.StorageDescription + ". No document upload, account, analytics, or AI service is required."); await ShowDialogAsync(dialog);
    }
}
