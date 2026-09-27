using TextSpace.Editor;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private void HideBackstage()
    {
        if (_backstage is null) return; _root.Children.Remove(_backstage); _backstage = null; Surface.FocusEditor();
    }
    private void ShowBackstage(string section = "Home")
    {
        if (_backstage is not null) _root.Children.Remove(_backstage);
        var menu = new StackPanel { Spacing = 3, Padding = new(0, 12, 0, 18), Background = OfficeTheme.Brush(OfficeTheme.Accent) };
        var back = new OfficeButton { Content = OfficeTheme.Row(new OfficeIcon("undo", 22, "#FFFFFF"), OfficeTheme.Text("Back", 13, "#FFFFFF")), Height = 48, Padding = new(20, 8), HorizontalContentAlignment = HorizontalAlignment.Left };
        back.Click += (_, _) => HideBackstage(); AutomationProperties.SetName(back, "Back to document"); menu.Children.Add(back);
        foreach (var name in new[] { "Home", "New", "Open", "Info", "Save As", "Print", "Export", "History", "Options" })
        {
            var chosen = name; var text = OfficeTheme.Text(name, 14, "#FFFFFF", name == section); var button = new OfficeButton { Content = text, Height = 43, Padding = new(26, 8), CornerRadius = new(0), HorizontalContentAlignment = HorizontalAlignment.Left, RestBackground = name == section ? "#12468F" : "#00185ABD", ForegroundOverride = "#FFFFFF" };
            button.Click += (_, _) => ShowBackstage(chosen); AutomationProperties.SetName(button, "File " + name); menu.Children.Add(button);
        }
        var body = new StackPanel { Spacing = 22, Padding = new(42, 34, 42, 40), MaxWidth = 1060, HorizontalAlignment = HorizontalAlignment.Left };
        body.Children.Add(OfficeTheme.Text(section == "Home" ? "Good writing starts here." : section, 30, OfficeTheme.Ink, true));
        var content = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _backstage = new Border { Background = OfficeTheme.Brush("#FAFAFA"), Child = OfficeTheme.Columns((new ScrollViewer { Content = menu, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = OfficeTheme.Brush(OfficeTheme.Accent) }, 175), (content, -1)) };
        _root.Children.Insert(Math.Max(0, _root.Children.Count - 1), _backstage);
        void Action(string title, string description, string glyph, string command)
        {
            var icon = new OfficeIcon(glyph, 32, OfficeTheme.Accent) { Margin = new(0, 0, 16, 0) }; var text = OfficeTheme.Column(OfficeTheme.Text(title, 17, OfficeTheme.Ink, true), Wrapped(description, 12, OfficeTheme.Muted)); text.Spacing = 6;
            var button = new OfficeButton { Content = OfficeTheme.Columns((icon, 50), (text, -1)), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(20), BorderThickness = new(1), Width = 640, MaxWidth = 640, RestBackground = "#FFFFFF" };
            AutomationProperties.SetName(button, title); button.Click += (_, _) => _ = ExecuteCommandAsync(command); body.Children.Add(button);
        }
        if (section is "Home" or "New")
        {
            body.Children.Add(Wrapped("Start with a blank page or make a template your own. Everything stays on this device.", 14, OfficeTheme.Muted));
            var cards = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };
            foreach (var (name, factory) in new (string, Func<DocumentModel>)[] { ("Blank document", () => new()), ("Project report", SampleDocument.Report), ("Letter", SampleDocument.Letter), ("Welcome to TextSpace", SampleDocument.Create) })
            {
                var previewDoc = factory(); var preview = new DocumentPreview { Document = previewDoc, Renderer = Surface.Renderer, Width = 126, Height = 164 };
                var label = OfficeTheme.Text(name, 11); label.HorizontalAlignment = HorizontalAlignment.Center;
                var button = new OfficeButton { Content = new StackPanel { Spacing = 12, Children = { preview, label } }, Padding = new(14), BorderThickness = new(1), RestBackground = "#FFFFFF" }; var create = factory;
                AutomationProperties.SetName(button, name + " template"); button.Click += async (_, _) => { try { await NewDocumentAsync(create()); } catch (Exception ex) { Notify(ex.Message, true); } }; cards.Children.Add(button);
            }
            body.Children.Add(new ScrollViewer { Content = cards, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxWidth = 790 });
            if (section == "Home") { body.Children.Add(OfficeTheme.Text("Your writing room", 20, OfficeTheme.Ink, true)); Action("Open a document", "Import Word documents, native TextSpace files, or plain text.", "open", "open"); }
        }
        else if (section == "Open") Action("Browse this device", "Open .docx, .textspace, .txt or .md files.", "open", "open");
        else if (section is "Save As" or "Export")
        {
            body.Children.Add(Wrapped("Choose the format that fits what comes next.", 14, OfficeTheme.Muted));
            Action("TextSpace document", "Complete editable document, comments, and local revision history (.textspace).", "save", "save");
            Action("Word document", "Share rich paragraphs, tables, images, and comments (.docx).", "document", "export-docx");
            Action("PDF document", "Preserve the current Skia-rendered pages with vector text (.pdf).", "pdf", "export-pdf");
            Action("Web page", "Export a self-contained HTML document with pictures (.html).", "export", "export-html");
            Action("Plain text", "Document text without formatting (.txt).", "document", "export-text");
            Action("Page image", "Export the current page at 2× resolution (.png).", "image", "export-png");
        }
        else if (section == "Print")
        {
            Action("Print document", "Open the system print dialog using the current paginated layout.", "print", "print");
            body.Children.Add(new DocumentPreview { Document = Session.Document, Renderer = Surface.Renderer, Layout = Surface.Layout, Width = 360, Height = 466 });
        }
        else if (section == "Info")
        {
            body.Children.Add(OfficeTheme.Text(Session.Document.Title, 22, OfficeTheme.Accent, true));
            body.Children.Add(Wrapped("Local document · " + Host.StorageDescription, 13, OfficeTheme.Muted));
            foreach (var (label, value) in new[] { ("Author", Session.Document.Author), ("Created", Session.Document.Created.ToLocalTime().ToString("f")), ("Modified", Session.Document.Modified.ToLocalTime().ToString("f")), ("Words", Session.Document.WordCount.ToString("N0")), ("Pages", Surface.Layout.Pages.Count.ToString()), ("Comments", Session.Document.Comments.Count.ToString()), ("Local tracked edits", Session.Document.Changes.Count.ToString()) }) body.Children.Add(OfficeTheme.Columns((OfficeTheme.Text(label, 13, OfficeTheme.Muted), 160), (OfficeTheme.Text(value, 13), -1)));
            Action("Rename document", "Change the name used for exports and recovery snapshots.", "document", "rename");
        }
        else if (section == "History")
        {
            body.Children.Add(Wrapped("Up to 12 local recovery snapshots. Browser storage may be cleared by the browser or device owner. Keep file copies of important documents.", 13, OfficeTheme.Muted));
            var list = new StackPanel { Spacing = 10 }; body.Children.Add(list); _ = PopulateHistoryAsync(list);
        }
        else
        {
            Action("About TextSpace", "Version, licenses, privacy, and compatibility information.", "info", "about"); Action("Keyboard shortcuts", "Use familiar editing shortcuts and keyboard navigation.", "settings", "shortcuts"); Action("Help", "Learn the editing, file, and review workflows.", "help", "help"); Action("Feedback", "Open the project's GitHub issue tracker.", "comment", "feedback");
        }
    }
    private async Task PopulateHistoryAsync(StackPanel list)
    {
        try
        {
            var versions = await Host.ListVersionsAsync(); if (versions.Count == 0) list.Children.Add(Wrapped("No previous snapshots yet. AutoSave creates recovery history while you work.", 12, OfficeTheme.Muted));
            foreach (var version in versions)
            {
                var id = version.Id; var label = OfficeTheme.Column(OfficeTheme.Text(version.Title, 14, OfficeTheme.Ink, true), OfficeTheme.Text(version.SavedAt.ToLocalTime().ToString("f") + $" · {version.Bytes / 1024d:0.#} KB", 11, OfficeTheme.Muted));
                var button = new OfficeButton { Content = label, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(16), BorderThickness = new(1), Width = 600 };
                button.Click += async (_, _) => { try { var json = await Host.ReadVersionAsync(id); if (json is not null) await NewDocumentAsync(DocumentJson.Load(json)); } catch (Exception ex) { Notify(ex.Message, true); } }; list.Children.Add(button);
            }
        }
        catch (Exception ex) { list.Children.Add(Wrapped("Recovery history is unavailable: " + ex.Message, 12, "#A4262C")); }
    }
}
