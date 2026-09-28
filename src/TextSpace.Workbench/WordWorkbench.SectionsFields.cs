using System.Globalization;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private void InitializeSectionsAndFields()
    {
        Ribbon.InsertGroup("Layout", 1, () => Group("Sections",
            MenuButton("pagebreak", "Section Break", new OfficeMenu()
                .Add("Next Page Section", () => RunEdit("Section break", () => Session.InsertSectionBreak(SectionBreakKind.NextPage)))
                .Add("Continuous Section", () => RunEdit("Section break", () => Session.InsertSectionBreak(SectionBreakKind.Continuous)))
                .Add("Next Column Section", () => RunEdit("Section break", () => Session.InsertSectionBreak(SectionBreakKind.NextColumn)))
                .Add("Odd Page Section", () => RunEdit("Section break", () => Session.InsertSectionBreak(SectionBreakKind.OddPage)))
                .Add("Even Page Section", () => RunEdit("Section break", () => Session.InsertSectionBreak(SectionBreakKind.EvenPage)))
                .Separator().Add("Column Break", () => RunEdit("Column break", Session.InsertColumnBreak))),
            Tool("settings", "Section Settings", "section-settings", true),
            Tool("delete", "Remove Section Break", "remove-section", true)));
        Ribbon.InsertGroup("Insert", 1, () => Group("Fields",
            Tool("document", "Field", "insert-field", true),
            Tool("redo", "Update Fields", "update-fields", true),
            Tool("settings", "Manage Fields", "manage-fields", true)));
        Ribbon.InsertGroup("References", 1, () => Group("Cross-references",
            Tool("link", "Cross-reference", "cross-reference", true),
            Tool("redo", "Update Fields", "update-fields", true)));
    }

    private FieldContext CreateFieldContext(DocumentModel document)
    {
        var layout = Surface.Renderer.Layout(document);
        return new FieldContext { PageAt = layout.FieldPageAt, CultureName = "en-US" };
    }

    private void UpdateDocumentFields()
    {
        var results = Session.UpdateFields(CreateFieldContext);
        var problems = results.Where(r => r.Error is not null).ToArray();
        Notify($"Updated {results.Count(r => r.Evaluated)} fields" + (problems.Length == 0 ? "." : $"; {problems.Length} retained or unresolved. " + problems[0].Error), problems.Length > 0);
    }

    private async Task SectionSettingsAsync()
    {
        var number = Session.CurrentSectionIndex;
        var definition = Session.CurrentSection;
        var effective = DocumentSections.First(Session.Document);
        foreach (var item in DocumentSections.Definitions(Session.Document).Skip(1).Take(number))
            effective = DocumentSections.Resolve(item, effective);
        var dialog = new OfficeDialog($"Section {number + 1} Settings", "Apply", 620);
        dialog.AddDescription("These settings apply to the current section. Use Layout → Page Setup for its paper size, margins, and columns. Unchecked Link to previous with an empty story creates an explicitly blank header or footer.");
        var sectionStart = Choice(dialog, "Section start", Enum.GetNames<SectionBreakKind>(), Session.CurrentSectionStart.ToString());
        sectionStart.IsEnabled = number > 0;
        dialog.AddDescription("Continuous sections share a physical page when paper sizes match. Next-column sections require the same column grid. Shared pages keep the first region's header/footer; body PAGE and SECTION fields use the containing region.");
        var first = new OfficeCheckBox("Different first page", definition.Options.DifferentFirstPage);
        var even = new OfficeCheckBox("Different odd and even pages", definition.Options.DifferentOddAndEven);
        dialog.Body.Children.Add(OfficeTheme.Column(first, even));
        var start = dialog.AddField("Start page numbering at (blank to continue)", definition.Options.PageNumberStart?.ToString(CultureInfo.InvariantCulture) ?? "");
        var numberStyle = Choice(dialog, "Page number format", Enum.GetNames<PageNumberStyle>(), definition.Options.NumberStyle.ToString());
        (TextBox Text, OfficeCheckBox Linked) Story(string label, string? value, string? inherited)
        {
            var field = dialog.AddField(label, value ?? inherited ?? "", multiline: true);
            field.MinHeight = 52; field.Height = 52;
            var linked = new OfficeCheckBox("Link " + label.ToLowerInvariant() + " to previous", number > 0 && value is null) { IsEnabled = number > 0 };
            field.IsEnabled = !linked.IsChecked;
            linked.CheckedChanged += state => field.IsEnabled = !state;
            dialog.Body.Children.Add(linked);
            return (field, linked);
        }
        var header = Story("Default header", definition.Header, effective.Header);
        var footer = Story("Default footer", definition.Footer, effective.Footer);
        var firstHeader = Story("First page header", definition.Options.FirstHeader, effective.Options.FirstHeader);
        var firstFooter = Story("First page footer", definition.Options.FirstFooter, effective.Options.FirstFooter);
        var evenHeader = Story("Even page header", definition.Options.EvenHeader, effective.Options.EvenHeader);
        var evenFooter = Story("Even page footer", definition.Options.EvenFooter, effective.Options.EvenFooter);
        dialog.AddDescription("Story text supports {PAGE}, {NUMPAGES}, {SECTION}, {SECTIONPAGES}, {TITLE}, and {AUTHOR}. Rich text, pictures, and tables inside headers/footers are not supported yet.");
        dialog.PrimaryButton.IsEnabled = !Session.IsReadOnly;
        if (!await ShowDialogAsync(dialog)) return;
        int? restart = null;
        if (!string.IsNullOrWhiteSpace(start.Text))
        {
            if (!int.TryParse(start.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value is < 1 or > 1_000_000)
                throw new InvalidOperationException("The first page number must be an integer from 1 to 1,000,000, or blank to continue.");
            restart = value;
        }
        if (!Enum.TryParse<PageNumberStyle>(numberStyle.Value, out var format) || !Enum.IsDefined(format))
            throw new InvalidOperationException("Choose a valid page number format.");
        if (!Enum.TryParse<SectionBreakKind>(sectionStart.Value, out var startKind) || !Enum.IsDefined(startKind))
            throw new InvalidOperationException("Choose a valid section start.");
        static string? Value((TextBox Text, OfficeCheckBox Linked) story) => story.Linked.IsChecked ? null : story.Text.Text;
        Session.SetSection(section => section with
        {
            Header = Value(header), Footer = Value(footer),
            Options = section.Options with
            {
                DifferentFirstPage = first.IsChecked, DifferentOddAndEven = even.IsChecked,
                PageNumberStart = restart, NumberStyle = format,
                FirstHeader = Value(firstHeader), FirstFooter = Value(firstFooter), EvenHeader = Value(evenHeader), EvenFooter = Value(evenFooter)
            }
        }, startKind);
        Notify($"Updated section {number + 1}. Use F9 to refresh body field results.");
    }

    private async Task InsertFieldAsync(string? id = null)
    {
        var existing = id is null ? null : Session.Document.Fields.FirstOrDefault(f => f.Id == id)
            ?? throw new InvalidOperationException("The field no longer exists.");
        var dialog = new OfficeDialog(existing is null ? "Insert Field" : "Edit Field", existing is null ? "Insert" : "Apply", 570);
        var code = dialog.AddField("Field code", existing?.Instruction ?? "PAGE");
        var presets = new OfficeMenu();
        foreach (var instruction in new[] { "PAGE", "NUMPAGES", "SECTION", "SECTIONPAGES", "TITLE", "AUTHOR", "SUBJECT", "FILENAME", "NUMWORDS", "NUMCHARS", "SEQ Figure", "DATE \\@ \"yyyy-MM-dd\"", "TIME \\@ \"HH:mm\"", "CREATEDATE", "SAVEDATE" })
        {
            var selected = instruction;
            presets.Add(instruction, () => code.Text = selected);
        }
        dialog.Body.Children.Add(MenuButton("document", "Common Fields", presets, false, true));
        var status = Wrapped("", 11, "#A4262C"); dialog.Body.Children.Add(status);
        dialog.AddDescription("Use REF BookmarkName for its text, PAGEREF BookmarkName for its page, or SEQ Figure for numbered captions. Add \\h to a reference for a clickable link; \\* ROMAN, roman, ALPHABETIC, or alphabetic formats numbers. DATE and TIME accept \\@ \"format\" using .NET format patterns.");
        dialog.AddDescription("F9 updates live results. Locked fields retain their cached text. Editing inside a result converts that field to ordinary text. Unknown imported codes remain inert and are not executed.");
        void Validate()
        {
            try
            {
                var instruction = FieldInstruction.Parse(code.Text);
                dialog.PrimaryButton.IsEnabled = instruction.Supported && !Session.IsReadOnly;
                status.Text = instruction.Supported ? "" : "This instruction or switch is not supported by the local field evaluator.";
            }
            catch (Exception error) { dialog.PrimaryButton.IsEnabled = false; status.Text = error.Message; }
        }
        code.TextChanged += (_, _) => Validate(); Validate();
        if (!await ShowDialogAsync(dialog)) return;
        if (existing is null) Session.InsertField(code.Text, CreateFieldContext);
        else Session.Execute("Edit field", () => { Session.SetFieldInstruction(existing.Id, code.Text); Session.UpdateFields(CreateFieldContext); });
        Notify(existing is null ? "Inserted live field." : "Updated field instruction.");
    }

    private async Task CrossReferenceAsync()
    {
        if (Session.Document.Bookmarks.Count == 0) throw new InvalidOperationException("Create a bookmark before inserting a cross-reference.");
        var dialog = new OfficeDialog("Cross-reference", "Insert", 500);
        var name = Choice(dialog, "Reference bookmark", Session.Document.Bookmarks.Select(b => b.Name).Order(StringComparer.OrdinalIgnoreCase), Session.Document.Bookmarks[0].Name);
        var kind = Choice(dialog, "Reference content", ["Bookmark text", "Page number"], "Bookmark text");
        var link = new OfficeCheckBox("Insert as hyperlink", true); dialog.Body.Children.Add(link);
        dialog.AddDescription("The reference remains linked to its bookmark. Press F9 after editing the target or changing pagination.");
        dialog.PrimaryButton.IsEnabled = !Session.IsReadOnly;
        if (!await ShowDialogAsync(dialog)) return;
        var bookmark = Session.Document.Bookmarks.FirstOrDefault(b => b.Name.Equals(name.Value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Choose an existing bookmark.");
        if (kind.Value is not ("Bookmark text" or "Page number")) throw new InvalidOperationException("Choose a reference content type.");
        Session.InsertField((kind.Value == "Page number" ? "PAGEREF " : "REF ") + bookmark.Name + (link.IsChecked ? " \\h" : ""), CreateFieldContext);
    }

    private async Task ManageFieldsAsync()
    {
        var dialog = new OfficeDialog("Document Fields", "Close", 640);
        dialog.AddDescription("Choose a field to navigate, edit its code, freeze updates, or unlink its cached result. All changes can be undone.");
        var body = new StackPanel { Spacing = 9 }; dialog.Body.Children.Add(body);
        var status = Wrapped("", 11, "#A4262C"); dialog.Body.Children.Add(status);
        string? edit = null, navigate = null;
        void Refresh()
        {
            body.Children.Clear();
            if (Session.Document.Fields.Count == 0) body.Children.Add(Wrapped("No live fields in this document. Use Insert → Field to create one.", 12, OfficeTheme.Muted));
            foreach (var field in Session.Document.Fields.OrderBy(f => f.Start).Take(200))
            {
                var fieldId = field.Id;
                void Mutate(Action action)
                {
                    try { action(); status.Text = ""; Refresh(); }
                    catch (Exception error) { status.Text = error.Message; }
                }
                var title = Wrapped(field.Instruction + (field.Locked ? " · Locked" : ""), 12, OfficeTheme.Accent, true);
                var value = Wrapped(Abbreviate(Session.Index.Text.Substring(field.Start, field.End - field.Start), 160), 11);
                var buttons = OfficeTheme.Row(
                    new OfficeButton("Go to", () => { navigate = fieldId; dialog.Close(true); }),
                    new OfficeButton("Edit code", () => { edit = fieldId; dialog.Close(true); }) { IsEnabled = !Session.IsReadOnly },
                    new OfficeButton(field.Locked ? "Unlock" : "Lock", () => Mutate(() => Session.LockField(fieldId, !field.Locked))) { IsEnabled = !Session.IsReadOnly },
                    new OfficeButton("Unlink", () => Mutate(() => Session.UnlinkField(fieldId))) { IsEnabled = !Session.IsReadOnly });
                body.Children.Add(Card(OfficeTheme.Column(title, value, buttons)));
            }
            if (Session.Document.Fields.Count > 200) body.Children.Add(Wrapped("Showing the first 200 fields. Use document navigation or selections to reach later fields.", 11, OfficeTheme.Muted));
        }
        Refresh(); await ShowDialogAsync(dialog);
        if (navigate is not null && Session.Document.Fields.FirstOrDefault(f => f.Id == navigate) is { } target)
            Session.SetSelection(target.Start, target.End);
        if (edit is not null) await InsertFieldAsync(edit);
    }
}
