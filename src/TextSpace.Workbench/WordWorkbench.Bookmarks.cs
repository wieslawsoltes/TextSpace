namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private void InitializeDocumentNavigation()
    {
        Ribbon.InsertGroup("Insert", 3, () => Group("Navigation",
            new RibbonButton("book", "Bookmark", async () =>
            {
                try { await BookmarksAsync(); }
                catch (Exception error) { Notify(error.Message, true); }
                Surface.FocusEditor();
            }, large: true),
            new RibbonButton("link", "Open Link", async () =>
            {
                if (Session.TypingStyle.Hyperlink is { } target) await FollowHyperlinkAsync(target);
                else Notify("Place the cursor inside a hyperlink first.", true);
            }, large: true)));
        Surface.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((sender, e) =>
        {
            if (!TextSpace.Editor.DocumentSurface.ControlDown()) return;
            var point = e.GetCurrentPoint(Surface);
            if (!point.Properties.IsLeftButtonPressed) return;
            var x = (point.Position.X - Surface.PaperLeft) / Surface.Scale;
            var y = (point.Position.Y - (Surface.ShowRuler ? 25 : 0) - 18 + Surface.ScrollY) / Surface.Scale;
            var layout = Surface.Layout;
            var pageIndex = (int)Math.Floor(y / (layout.Settings.Height + TextSpace.Layout.DocumentLayout.PageGap));
            if (pageIndex < 0 || pageIndex >= layout.Pages.Count) return;
            var localY = y - layout.PageTop(pageIndex);
            var line = layout.Pages[pageIndex].Lines.FirstOrDefault(l => localY >= l.Y && localY <= l.Y + l.Height && x >= l.X && x <= l.X + l.Width);
            var link = line?.Chunks.FirstOrDefault(c => x >= c.X && x <= c.X + c.Width)?.Style.Hyperlink;
            if (link is not null) { _ = FollowHyperlinkAsync(link); e.Handled = true; }
        }), true);
    }
    private async Task BookmarksAsync()
    {
        var dialog = new OfficeDialog("Bookmark", "Add or update", 530);
        var name = dialog.AddField("Bookmark name");
        dialog.AddDescription("Name the current selection using up to 40 letters, digits, or underscores, beginning with a letter or underscore. Existing names can be moved, renamed, or deleted.");
        var status = Wrapped("", 11, "#A4262C");
        var list = new StackPanel { Spacing = 5 };
        var hidden = new OfficeCheckBox("Show hidden bookmarks", false);
        string? selected = null, navigate = null;
        void Refresh()
        {
            list.Children.Clear();
            foreach (var bookmark in Session.Document.Bookmarks.Where(b => hidden.IsChecked || !b.Name.StartsWith('_')).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
            {
                var target = bookmark.Name;
                var select = new OfficeButton(target, () => { selected = target; name.Text = target; })
                {
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new(8, 5), IsSelected = target.Equals(selected, StringComparison.OrdinalIgnoreCase)
                };
                var go = new OfficeButton("Go To", () => { navigate = target; dialog.Close(false); });
                AutomationProperties.SetName(go, "Go to bookmark " + target);
                var remove = new OfficeButton("Delete", () =>
                {
                    try { Session.DeleteBookmark(target); Refresh(); status.Text = ""; }
                    catch (Exception error) { status.Text = error.Message; }
                }) { IsEnabled = !Session.IsReadOnly };
                AutomationProperties.SetName(remove, "Delete bookmark " + target);
                list.Children.Add(OfficeTheme.Columns((select, -1), (go, 65), (remove, 65)));
            }
            if (list.Children.Count == 0) list.Children.Add(Wrapped("No bookmarks to show.", 12, OfficeTheme.Muted));
        }
        hidden.CheckedChanged += _ => Refresh();
        dialog.Body.Children.Add(hidden);
        dialog.Body.Children.Add(new ScrollViewer { Content = list, MaxHeight = 260, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var rename = new OfficeButton("Rename selected", () =>
        {
            try
            {
                if (selected is null) throw new InvalidOperationException("Select a bookmark name in the list first.");
                Session.RenameBookmark(selected, name.Text.Trim()); selected = name.Text.Trim(); Refresh(); status.Text = "";
            }
            catch (Exception error) { status.Text = error.Message; }
        }) { IsEnabled = !Session.IsReadOnly, HorizontalAlignment = HorizontalAlignment.Left, BorderThickness = new(1) };
        dialog.Body.Children.Add(rename); dialog.Body.Children.Add(status);
        dialog.PrimaryButton.IsEnabled = !Session.IsReadOnly;
        Refresh();
        if (await ShowDialogAsync(dialog)) Session.SetBookmark(name.Text.Trim());
        else if (navigate is not null && !Session.GoToBookmark(navigate)) Notify("The bookmark no longer exists.", true);
    }
    private async Task FollowHyperlinkAsync(string target)
    {
        try
        {
            if (target.StartsWith('#'))
            {
                var name = Uri.UnescapeDataString(target[1..]);
                if (!Session.GoToBookmark(name)) throw new InvalidOperationException("Bookmark not found: " + name);
                Surface.FocusEditor();
            }
            else
            {
                if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http" or "mailto"))
                    throw new InvalidOperationException("Only HTTP, HTTPS and mailto hyperlinks can be opened.");
                await Host.OpenUriAsync(uri.AbsoluteUri);
            }
        }
        catch (Exception error) { Notify(error.Message, true); }
    }
}
