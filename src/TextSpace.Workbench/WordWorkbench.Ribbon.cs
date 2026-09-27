using System.Globalization;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private void ConfigureRibbon()
    {
        Ribbon.AddTab("Home", HomeRibbon); Ribbon.AddFileTab(() => ShowBackstage());
        Ribbon.AddTab("Insert", InsertRibbon); Ribbon.AddTab("Design", DesignRibbon); Ribbon.AddTab("Layout", LayoutRibbon);
        Ribbon.AddTab("References", ReferencesRibbon); Ribbon.AddTab("Mailings", MailingsRibbon); Ribbon.AddTab("Review", ReviewRibbon); Ribbon.AddTab("View", ViewRibbon); Ribbon.AddTab("Help", HelpRibbon);
        Ribbon.AddTab("Table Design", TableDesignRibbon, true); Ribbon.AddTab("Table Layout", TableLayoutRibbon, true); Ribbon.AddTab("Picture Format", PictureRibbon, true);
        Ribbon.SetTabVisible("Table Design", false); Ribbon.SetTabVisible("Table Layout", false); Ribbon.SetTabVisible("Picture Format", false);
        var comments = Tool("comment", "Comments", "comments", showLabel: true); comments.BorderThickness = new(1); comments.Height = 26;
        var editing = new RibbonButton("document", "Editing", showLabel: true, dropdown: true) { BorderThickness = new(1), Height = 26 };
        editing.Flyout = new OfficeMenu().Add("Editing", () => _ = ExecuteCommandAsync("editing"), "document").Add("Reviewing", () => _ = ExecuteCommandAsync("reviewing"), "track").Add("Viewing", () => _ = ExecuteCommandAsync("read-mode"), "book").AsFlyout();
        var share = new RibbonButton("export", "Share", showLabel: true, dropdown: true) { IsPrimary = true, Height = 26, Padding = new(12, 3) };
        share.Flyout = new OfficeMenu().Heading("Share a copy").Add("Word document (.docx)", () => _ = ExecuteCommandAsync("export-docx"), "document").Add("PDF document (.pdf)", () => _ = ExecuteCommandAsync("export-pdf"), "pdf").Add("Copy document text", () => _ = ExecuteCommandAsync("copy-all"), "copy").AsFlyout();
        Ribbon.Actions.Children.Add(comments); Ribbon.Actions.Children.Add(editing); Ribbon.Actions.Children.Add(share);
    }
    private static RibbonGroup Group(string title, params UIElement[] children)
    {
        var group = new RibbonGroup(title); foreach (var child in children) group.Body.Children.Add(child); return group;
    }
    private RibbonButton MenuButton(string glyph, string title, OfficeMenu menu, bool large = true, bool showLabel = false)
    {
        var button = new RibbonButton(glyph, title, large: large, showLabel: showLabel, dropdown: true); button.Flyout = menu.AsFlyout(); return button;
    }
    private RibbonButton ColorButton(string glyph, string title, Action<string?> action, bool none = false, bool large = false)
    {
        var palette = new ColorPalette(none); palette.ColorSelected += color => RunEdit(title, () => action(color));
        var button = new RibbonButton(glyph, title, large: large, dropdown: true); button.Flyout = palette.AsFlyout(); return button;
    }
    private RibbonButton Unavailable(string glyph, string title, string reason, bool large = true)
    {
        var button = new RibbonButton(glyph, title, large: large) { IsEnabled = false }; ToolTipService.SetToolTip(button, reason); return button;
    }
    private IEnumerable<RibbonGroup> HomeRibbon()
    {
        var paste = MenuButton("paste", "Paste", new OfficeMenu().Add("Paste", () => _ = ExecuteCommandAsync("paste"), "paste", "Ctrl+V").Add("Keep text only", () => _ = ExecuteCommandAsync("paste"), "document"));
        paste.SetIconColor("#A66F16");
        yield return Group("Clipboard", paste, OfficeTheme.Column(Tool("cut", "Cut", "cut", showLabel: true), Tool("copy", "Copy", "copy", showLabel: true), Tool("paint", "Format Painter", "format-painter", showLabel: true)));
        _fontField = new("Font family", ["Aptos", "Aptos Display", "Calibri", "Arial", "Times New Roman", "Georgia", "Courier New", "Carlito", "Inter"], 132);
        _fontField.ValueCommitted += value => RunEdit("Font", () => { if (string.IsNullOrWhiteSpace(value) || value.Length > 100) throw new InvalidOperationException("Enter a valid font family."); Session.SetFont(value.Trim()); });
        _sizeField = new("Font size", ["8", "9", "10", "11", "12", "14", "16", "18", "20", "22", "24", "26", "28", "36", "48", "72"], 45);
        _sizeField.ValueCommitted += value => RunEdit("Font size", () => Session.SetFontSize(ParseNumber(value)));
        var caseMenu = new OfficeMenu().Add("UPPERCASE", () => RunEdit("Uppercase", () => Session.ChangeCase(true))).Add("lowercase", () => RunEdit("Lowercase", () => Session.ChangeCase(false))).Add("Capitalize Each Word", () => RunEdit("Title case", () => { if (!Session.Selection.IsEmpty) Session.InsertText(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Session.SelectedText().ToLowerInvariant())); }));
        var caseButton = MenuButton("", "Aa", caseMenu, false, true);
        var fontTop = OfficeTheme.Row(_fontField, _sizeField, Tool("grow", "Increase font size", "grow"), Tool("shrink", "Decrease font size", "shrink"), caseButton, Tool("clear", "Clear all formatting", "clear-format"));
        var fontBottom = OfficeTheme.Row(Tool("bold", "Bold (Ctrl+B)", "bold"), Tool("italic", "Italic (Ctrl+I)", "italic"), Tool("underline", "Underline (Ctrl+U)", "underline"), Tool("strike", "Strikethrough", "strike"), Tool("subscript", "Subscript", "subscript"), Tool("superscript", "Superscript", "superscript"), ColorButton("highlight", "Text highlight color", color => Session.FormatText("Highlight", s => s with { Highlight = color }), true), ColorButton("font", "Font color", color => Session.FormatText("Font color", s => s with { Color = color ?? "#202020" })));
        var fontGroup = new RibbonGroup("Font", () => _ = ExecuteCommandAsync("font-dialog")); fontGroup.Body.Children.Add(new StackPanel { Spacing = 7, Children = { fontTop, fontBottom } }); yield return fontGroup;
        var bullets = Tool("bullets", "Bullets", "bullets"); var numbers = Tool("numbering", "Numbering", "numbering");
        var lists = MenuButton("multilist", "Multilevel list", new OfficeMenu().Add("Bulleted list", () => RunEdit("Bullets", () => Session.ToggleList(ListKind.Bullet)), "bullets").Add("Numbered list", () => RunEdit("Numbering", () => Session.ToggleList(ListKind.Number)), "numbering").Add("Increase list level", () => RunEdit("Indent", () => Session.Indent(1)), "indent").Add("Decrease list level", () => RunEdit("Outdent", () => Session.Indent(-1)), "outdent"), false);
        var paragraphTop = OfficeTheme.Row(bullets, numbers, lists, Tool("outdent", "Decrease indent", "outdent"), Tool("indent", "Increase indent", "indent"), Tool("settings", "Sort paragraphs", "sort"), Tool("paragraph", "Show/Hide ¶", "format-marks"));
        var spacingMenu = new OfficeMenu(); foreach (var value in new[] { 1d, 1.15, 1.5, 2, 2.5, 3 }) { var selected = value; spacingMenu.Add(value.ToString("0.##", CultureInfo.InvariantCulture), () => RunEdit("Line spacing", () => Session.FormatParagraph("Line spacing", p => p with { LineSpacing = selected }))); }
        spacingMenu.Separator().Add("Line Spacing Options…", () => _ = ExecuteCommandAsync("paragraph-dialog"));
        var paragraphBottom = OfficeTheme.Row(Tool("align-left", "Align left (Ctrl+L)", "align-left"), Tool("align-center", "Center (Ctrl+E)", "align-center"), Tool("align-right", "Align right (Ctrl+R)", "align-right"), Tool("justify", "Justify (Ctrl+J)", "justify"), MenuButton("spacing", "Line and paragraph spacing", spacingMenu, false), ColorButton("shade", "Paragraph shading", color => Session.FormatParagraph("Shading", p => p with { Shading = color }), true), Tool("borders", "Bottom border", "border-bottom"));
        var paragraphGroup = new RibbonGroup("Paragraph", () => _ = ExecuteCommandAsync("paragraph-dialog")); paragraphGroup.Body.Children.Add(new StackPanel { Spacing = 7, Children = { paragraphTop, paragraphBottom } }); yield return paragraphGroup;
        _styleGallery = new([DocumentStyles.Find("Normal"), DocumentStyles.Find("No Spacing"), DocumentStyles.Find("Heading 1"), DocumentStyles.Find("Heading 2"), DocumentStyles.Find("Title")]); _styleGallery.StyleInvoked += name => RunEdit(name, () => Session.ApplyStyle(name)); yield return Group("Styles", _styleGallery);
        yield return Group("Editing", OfficeTheme.Column(Tool("search", "Find", "find", showLabel: true), Tool("replace", "Replace", "replace", showLabel: true), MenuButton("select", "Select", new OfficeMenu().Add("Select All", () => RunEdit("Select all", Session.SelectAll), "select", "Ctrl+A").Add("Select Paragraph", () => RunEdit("Select paragraph", () => { var p = Session.Index.At(Session.Selection.Active); Session.SetSelection(p.Start, p.End); })), false, true)));
        yield return Group("Proofing", Tool("spell", "Editor", "proofing", true));
    }
    private IEnumerable<RibbonGroup> InsertRibbon()
    {
        yield return Group("Pages", Tool("new", "Blank Page", "blank-page", true), Tool("pagebreak", "Page Break", "page-break", true));
        var tablePicker = new TablePicker(); tablePicker.TableSelected += (rows, columns) => RunEdit("Insert table", () => Session.InsertTable(rows, columns));
        var table = new RibbonButton("table", "Table", large: true, dropdown: true) { Flyout = tablePicker.AsFlyout() }; yield return Group("Tables", table);
        yield return Group("Illustrations", Tool("image", "Pictures", "insert-picture", true), Unavailable("borders", "Shapes", "Drawing shapes and floating text boxes are not implemented in this release."));
        yield return Group("Links", Tool("link", "Link", "link", true));
        yield return Group("Comments", Tool("new-comment", "Comment", "new-comment", true));
        yield return Group("Header & Footer", Tool("header", "Header", "header", true), Tool("footer", "Footer", "footer", true), MenuButton("page-number", "Page Number", new OfficeMenu().Add("Page number", () => SetFooter("{PAGE}")).Add("Page X of Y", () => SetFooter("Page {PAGE} of {NUMPAGES}")).Add("Remove page numbers", () => SetFooter(""))));
        yield return Group("Text", Tool("date", "Date & Time", "date", true), Tool("borders", "Horizontal Line", "border-bottom", true));
        var symbols = new OfficeMenu(); foreach (var symbol in new[] { "©", "®", "™", "€", "£", "¥", "°", "±", "×", "÷", "Ω", "α", "β", "μ", "π", "∞", "→", "←", "✓", "§", "¶", "…", "—", "–", "½", "¼", "¾" }) { var selected = symbol; symbols.Add(symbol, () => RunEdit("Insert symbol", () => Session.InsertText(selected))); }
        yield return Group("Symbols", MenuButton("symbol", "Symbol", symbols));
    }
    private IEnumerable<RibbonGroup> DesignRibbon()
    {
        var themes = new OfficeMenu(); foreach (var (name, color) in new[] { ("Office", "#0F4761"), ("Blue", "#185ABD"), ("Green", "#386641"), ("Violet", "#7030A0"), ("Monochrome", "#242424") }) { var selected = color; themes.Add(name, () => ApplyTheme(selected)); }
        yield return Group("Document Formatting", MenuButton("document", "Themes", themes), ColorButton("shade", "Colors", color => ApplyTheme(color ?? "#0F4761"), large: true), MenuButton("spacing", "Paragraph Spacing", new OfficeMenu().Add("No paragraph space", () => SetDocumentSpacing(0, 1)).Add("Compact", () => SetDocumentSpacing(4, 1.05)).Add("Open", () => SetDocumentSpacing(8, 1.15)).Add("Double", () => SetDocumentSpacing(8, 2))));
        yield return Group("Page Background", Tool("font", "Watermark", "watermark", true), ColorButton("shade", "Page Color", color => Session.SetPage(p => p with { Color = color ?? "#FFFFFF" }), large: true));
    }
    private IEnumerable<RibbonGroup> LayoutRibbon()
    {
        var margins = new OfficeMenu().Add("Normal · 1 inch", () => SetMargins(72, 72, 72, 72)).Add("Narrow · 0.5 inch", () => SetMargins(36, 36, 36, 36)).Add("Moderate", () => SetMargins(72, 54, 72, 54)).Add("Wide", () => SetMargins(72, 144, 72, 144)).Separator().Add("Custom Margins…", () => _ = ExecuteCommandAsync("page-setup"));
        var orientation = new OfficeMenu().Add("Portrait", () => RunEdit("Portrait", () => Session.SetPage(p => p.Portrait())), "document").Add("Landscape", () => RunEdit("Landscape", () => Session.SetPage(p => p.Landscape())), "orientation");
        var sizes = new OfficeMenu().Add("Letter · 8.5 × 11 in", () => SetPageSize(612, 792)).Add("A4 · 210 × 297 mm", () => SetPageSize(595.276, 841.89)).Add("Legal · 8.5 × 14 in", () => SetPageSize(612, 1008)).Add("A5 · 148 × 210 mm", () => SetPageSize(419.528, 595.276)).Separator().Add("More Paper Sizes…", () => _ = ExecuteCommandAsync("page-setup"));
        var columns = new OfficeMenu(); foreach (var value in new[] { 1, 2, 3 }) { var count = value; columns.Add(value == 1 ? "One" : value == 2 ? "Two" : "Three", () => RunEdit("Columns", () => Session.SetPage(p => p with { Columns = count })), "columns"); }
        yield return Group("Page Setup", MenuButton("margins", "Margins", margins), MenuButton("orientation", "Orientation", orientation), MenuButton("document", "Size", sizes), MenuButton("columns", "Columns", columns), Tool("pagebreak", "Breaks", "page-break", true));
        UIElement Field(string name, double value, Action<double> apply)
        {
            var field = new OfficeComboField(name, ["0", "6", "8", "12", "18", "24", "36", "72"], 66) { Value = value.ToString("0.##", CultureInfo.InvariantCulture) };
            field.ValueCommitted += text => RunEdit(name, () => apply(ParseNumber(text)));
            return OfficeTheme.Row(OfficeTheme.Text(name, 11), field);
        }
        var p = Session.CurrentParagraph.Format;
        yield return Group("Paragraph", OfficeTheme.Column(Field("Left", p.LeftIndent, n => Session.FormatParagraph("Left indent", f => f with { LeftIndent = Math.Max(0, n) })), Field("Right", p.RightIndent, n => Session.FormatParagraph("Right indent", f => f with { RightIndent = Math.Max(0, n) }))), OfficeTheme.Column(Field("Before", p.SpaceBefore, n => Session.FormatParagraph("Spacing before", f => f with { SpaceBefore = Math.Max(0, n) })), Field("After", p.SpaceAfter, n => Session.FormatParagraph("Spacing after", f => f with { SpaceAfter = Math.Max(0, n) }))));
        yield return Group("Arrange", Tool("settings", "Paragraph", "paragraph-dialog", true));
    }
    private IEnumerable<RibbonGroup> ReferencesRibbon()
    {
        yield return Group("Table of Contents", Tool("toc", "Table of Contents", "toc", true), Tool("redo", "Update Table", "update-toc", true));
        yield return Group("Captions", Tool("document", "Insert Caption", "caption", true));
        yield return Group("Footnotes", Unavailable("page-number", "Insert Footnote", "Footnotes and endnotes require a separate pagination story and are not yet supported."));
        yield return Group("Citations & Bibliography", Unavailable("book", "Bibliography", "Citation databases and bibliography fields are not yet supported."));
    }
    private IEnumerable<RibbonGroup> MailingsRibbon()
    {
        yield return Group("Start Mail Merge", Tool("people", "Select Recipients", "merge-recipients", true));
        var fields = new OfficeMenu(); if (_mergeData is null) fields.Add("Load a CSV recipient list first", () => { }, enabled: false); else foreach (var column in _mergeData.Columns) { var name = column; fields.Add(name, () => RunEdit("Insert merge field", () => Session.InsertText("«" + name + "»"))); }
        yield return Group("Write & Insert Fields", MenuButton("document", "Insert Merge Field", fields));
        yield return Group("Preview Results", Tool("search", "Preview Results", "merge-preview", true));
        yield return Group("Finish", Tool("mail", "Finish & Merge", "merge-finish", true));
    }
    private IEnumerable<RibbonGroup> ReviewRibbon()
    {
        yield return Group("Proofing", Tool("spell", "Editor", "proofing", true), Tool("document", "Word Count", "word-count", true));
        yield return Group("Comments", Tool("new-comment", "New Comment", "new-comment", true), OfficeTheme.Column(Tool("delete", "Delete", "delete-comment", showLabel: true), Tool("undo", "Previous", "previous-comment", showLabel: true), Tool("redo", "Next", "next-comment", showLabel: true)));
        yield return Group("Tracking", Tool("track", "Track Changes", "track", true), Tool("comment", "Reviewing Pane", "changes", true));
        yield return Group("Changes", MenuButton("check", "Accept", new OfficeMenu().Add("Accept this change", () => _ = ExecuteCommandAsync("accept-change"), "check").Add("Accept all changes", () => RunEdit("Accept all", Session.AcceptAllChanges), "check")), MenuButton("reject", "Reject", new OfficeMenu().Add("Reject this change", () => _ = ExecuteCommandAsync("reject-change"), "reject").Add("Reject all safe changes", () => _ = ExecuteCommandAsync("reject-all"), "reject")));
        yield return Group("Compare", Tool("copy", "Compare", "compare", true));
        yield return Group("Protect", Tool("lock", "Restrict Editing", "read-mode", true));
    }
    private IEnumerable<RibbonGroup> ViewRibbon()
    {
        yield return Group("Views", Tool("book", "Read Mode", "read-mode", true), Tool("document", "Print Layout", "print-layout", true));
        var ruler = new OfficeCheckBox("Ruler", Surface.ShowRuler); ruler.CheckedChanged += value => Surface.ShowRuler = value;
        var navigation = new OfficeCheckBox("Navigation Pane", _navigationVisible); navigation.CheckedChanged += value => SetNavigation(value);
        var boundaries = new OfficeCheckBox("Text Boundaries", Surface.ShowBoundaries); boundaries.CheckedChanged += value => { Surface.ShowBoundaries = value; Surface.Invalidate(); };
        yield return Group("Show", OfficeTheme.Column(ruler, navigation, boundaries));
        yield return Group("Zoom", Tool("zoom", "Zoom", "zoom", true), Tool("document", "100%", "zoom-100", true), OfficeTheme.Column(Tool("document", "One Page", "one-page", showLabel: true), Tool("margins", "Page Width", "page-width", showLabel: true)));
        yield return Group("Immersive", Tool("focus", "Focus", "focus", true));
    }
    private IEnumerable<RibbonGroup> HelpRibbon()
    {
        yield return Group("Help", Tool("help", "Help", "help", true), Tool("settings", "Keyboard Shortcuts", "shortcuts", true), Tool("info", "About TextSpace", "about", true));
        yield return Group("Feedback", Tool("comment", "Give Feedback", "feedback", true));
    }
    private IEnumerable<RibbonGroup> TableDesignRibbon()
    {
        var table = Session.CurrentTable;
        var header = new OfficeCheckBox("Header Row", table?.HeaderRow ?? false); header.CheckedChanged += value => RunEdit("Header row", () => { if (Session.CurrentTable is { } t) Session.Execute("Header row", () => t.HeaderRow = value); });
        var bands = new OfficeCheckBox("Banded Rows", table?.BandedRows ?? false); bands.CheckedChanged += value => RunEdit("Banded rows", () => { if (Session.CurrentTable is { } t) Session.Execute("Banded rows", () => t.BandedRows = value); });
        yield return Group("Table Style Options", OfficeTheme.Column(header, bands));
        yield return Group("Table Styles", ColorButton("shade", "Cell Shading", color => SetCellShading(color), true, true));
    }
    private IEnumerable<RibbonGroup> TableLayoutRibbon()
    {
        yield return Group("Rows & Columns", Tool("row", "Insert Below", "table-row", true), Tool("columns", "Insert Right", "table-column", true), MenuButton("delete", "Delete", new OfficeMenu().Add("Delete Row", () => RunEdit("Delete row", Session.DeleteTableRow), "row").Add("Delete Table", () => RunEdit("Delete table", Session.DeleteTable), "table")));
        yield return Group("Alignment", Tool("align-left", "Align Left", "align-left", true), Tool("align-center", "Center", "align-center", true), Tool("align-right", "Align Right", "align-right", true));
        yield return Group("Table", Tool("settings", "Properties", "table-properties", true));
    }
    private IEnumerable<RibbonGroup> PictureRibbon()
    {
        yield return Group("Size", Tool("image", "Picture Size", "picture-size", true));
        yield return Group("Arrange", MenuButton("align-center", "Align", new OfficeMenu().Add("Left", () => AlignPicture(TextSpace.Core.TextAlignment.Left), "align-left").Add("Center", () => AlignPicture(TextSpace.Core.TextAlignment.Center), "align-center").Add("Right", () => AlignPicture(TextSpace.Core.TextAlignment.Right), "align-right")), Tool("delete", "Remove Picture", "delete-picture", true));
        yield return Group("Accessibility", Tool("document", "Alt Text", "picture-alt", true));
    }
    private void SetFooter(string value) => RunEdit("Footer", () => Session.Execute("Footer", () => Session.Document.Footer = value));
    private void SetMargins(double top, double right, double bottom, double left) => RunEdit("Margins", () => Session.SetPage(p => p with { MarginTop = top, MarginRight = right, MarginBottom = bottom, MarginLeft = left }));
    private void SetPageSize(double width, double height) => RunEdit("Page size", () => Session.SetPage(p => p with { Width = width, Height = height }));
    private void ApplyTheme(string color) => RunEdit("Document theme", () => Session.Execute("Document theme", () => { foreach (var p in Session.Document.Paragraphs().Where(p => p.Format.OutlineLevel > 0 || p.Format.StyleName == "Title")) { p.DefaultStyle = p.DefaultStyle with { Color = color }; foreach (var run in p.Runs) run.Style = run.Style with { Color = color }; } }));
    private void SetDocumentSpacing(double after, double line) => RunEdit("Document spacing", () => Session.Execute("Document spacing", () => { foreach (var p in Session.Document.Paragraphs().Where(p => p.Format.OutlineLevel == 0)) p.Format = p.Format with { SpaceAfter = after, LineSpacing = line }; }));
    private void SetCellShading(string? color)
    {
        var p = Session.CurrentParagraph; var cell = Session.CurrentTable?.Rows.SelectMany(r => r.Cells).FirstOrDefault(c => DocumentModel.Walk(c.Blocks).Contains(p));
        if (cell is not null) Session.Execute("Cell shading", () => cell.Shading = color);
    }
    private void AlignPicture(TextSpace.Core.TextAlignment alignment) => RunEdit("Align picture", () => { var picture = SelectedPicture(); if (picture is not null) Session.Execute("Align picture", () => picture.Alignment = alignment); });
}
