using TextSpace.Editor;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private StackPanel? _navigationResults;
    private OfficeTaskPane? _reviewPane;
    private bool _matchCase, _wholeWord;
    private IReadOnlyList<SearchMatch> _matches = [];
    private List<(string Kind, string Text)> _comparison = [];
    private string _comparisonName = "";
    private static TextBlock Wrapped(string text, double size = 12, string color = OfficeTheme.Ink, bool bold = false)
    {
        var block = OfficeTheme.Text(text, size, color, bold); block.TextWrapping = TextWrapping.Wrap; return block;
    }
    private static Border Card(UIElement child, string fill = "#FFFFFF") => new() { Child = child, Background = OfficeTheme.Brush(fill), BorderBrush = OfficeTheme.Brush("#E0E0E0"), BorderThickness = new(1), CornerRadius = new(5), Padding = new(12) };
    private void SetNavigation(bool visible)
    {
        _navigationVisible = visible;
        if (!visible) { _navigationHost.Child = null; _navigationResults = null; _searchField = null; return; }
        var pane = new OfficeTaskPane("Navigation", 270); pane.CloseRequested += () => SetNavigation(false);
        var modes = OfficeTheme.Row(); foreach (var mode in new[] { "Headings", "Pages", "Results" }) { var selected = mode; var button = new OfficeButton(mode, () => { _navigationMode = selected; SetNavigation(true); }) { Height = 30, IsSelected = _navigationMode == mode, Padding = new(10, 4) }; modes.Children.Add(button); }
        pane.Body.Children.Add(modes);
        _searchField = OfficeTheme.Field("Search document", _searchQuery); _searchField.PlaceholderText = "Search document";
        _searchField.TextChanged += (_, _) => { _searchQuery = _searchField.Text; if (_searchQuery.Length > 0 && _navigationMode != "Replace") _navigationMode = "Results"; _searchMatchIndex = 0; RefreshNavigation(); };
        _searchField.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { FindNext(DocumentSurface.KeyDown(VirtualKey.Shift) ? -1 : 1); e.Handled = true; } };
        pane.Body.Children.Add(_searchField);
        if (_navigationMode is "Results" or "Replace")
        {
            var matchCase = new OfficeCheckBox("Match case", _matchCase); var wholeWord = new OfficeCheckBox("Whole words", _wholeWord);
            matchCase.CheckedChanged += value => { _matchCase = value; RefreshNavigation(); }; wholeWord.CheckedChanged += value => { _wholeWord = value; RefreshNavigation(); }; pane.Body.Children.Add(OfficeTheme.Column(matchCase, wholeWord));
            var previous = new RibbonButton("chevron-up", "Previous search result", () => FindNext(-1)); var next = new RibbonButton("chevron", "Next search result", () => FindNext(1));
            var replaceMode = new OfficeButton(_navigationMode == "Replace" ? "Hide replace" : "Replace", () => { _navigationMode = _navigationMode == "Replace" ? "Results" : "Replace"; SetNavigation(true); }); pane.Body.Children.Add(OfficeTheme.Row(previous, next, replaceMode));
        }
        if (_navigationMode == "Replace")
        {
            var replacement = OfficeTheme.Field("Replace with", _replaceText); replacement.PlaceholderText = "Replace with"; replacement.TextChanged += (_, _) => _replaceText = replacement.Text; pane.Body.Children.Add(replacement);
            pane.Body.Children.Add(OfficeTheme.Row(new OfficeButton("Replace", () => RunEdit("Replace", ReplaceOne)) { BorderThickness = new(1) }, new OfficeButton("Replace All", () => RunEdit("Replace all", () => { var count = Session.ReplaceAll(_searchQuery, _replaceText, _matchCase, _wholeWord); Notify($"Replaced {count} occurrences"); })) { IsPrimary = true }));
        }
        _navigationResults = new StackPanel { Spacing = 6 }; pane.Body.Children.Add(_navigationResults); _navigationHost.Child = pane; RefreshNavigation();
    }
    private void RefreshNavigation()
    {
        if (_navigationResults is null) return; _navigationResults.Children.Clear();
        if (_navigationMode == "Pages")
        {
            for (var i = 0; i < Math.Min(100, Surface.Layout.Pages.Count); i++)
            {
                var page = i; var preview = new DocumentPreview { Document = Session.Document, Renderer = Surface.Renderer, Layout = Surface.Layout, PageIndex = i, Width = 158, Height = 200 };
                var label = OfficeTheme.Text("Page " + (i + 1), 10, OfficeTheme.Muted); label.HorizontalAlignment = HorizontalAlignment.Center;
                var button = new OfficeButton { Content = OfficeTheme.Column(preview, label), HorizontalAlignment = HorizontalAlignment.Center, Padding = new(6), BorderThickness = new(1) }; AutomationProperties.SetName(button, "Go to page " + (i + 1)); button.Click += (_, _) => Surface.ScrollToPage(page); _navigationResults.Children.Add(button);
            }
            return;
        }
        if (_navigationMode == "Headings" && _searchQuery.Length == 0)
        {
            var headings = Session.Index.Paragraphs.Where(p => p.Paragraph.Format.OutlineLevel > 0).ToArray();
            if (headings.Length == 0) _navigationResults.Children.Add(Wrapped("Apply heading styles to organize your document and navigate its sections.", 12, OfficeTheme.Muted));
            foreach (var heading in headings)
            {
                var selected = heading; var button = new OfficeButton { Content = Wrapped(heading.Paragraph.Text, 12, OfficeTheme.Ink, heading.Paragraph.Format.OutlineLevel == 1), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(6 + 12 * Math.Max(0, heading.Paragraph.Format.OutlineLevel - 1), 7, 6, 7) };
                AutomationProperties.SetName(button, "Heading: " + heading.Paragraph.Text); button.Click += (_, _) => { Session.SetSelection(selected.Start, selected.Start); Surface.FocusEditor(); }; _navigationResults.Children.Add(button);
            }
            return;
        }
        try { _matches = Session.Find(_searchQuery, _matchCase, _wholeWord); }
        catch (Exception ex) { _navigationResults.Children.Add(Wrapped(ex.Message, 11, "#A4262C")); return; }
        _navigationResults.Children.Add(Wrapped(_searchQuery.Length == 0 ? "Search for text in your document." : $"{_matches.Count} results", 11, OfficeTheme.Muted));
        foreach (var match in _matches.Take(100))
        {
            var found = match; var button = new OfficeButton { Content = Wrapped(match.Preview, 11), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(9), BorderThickness = new(1), IsSelected = Session.Selection.Start == match.Start && Session.Selection.Length == match.Length };
            AutomationProperties.SetName(button, "Search result: " + match.Preview); button.Click += (_, _) => { Session.SetSelection(found.Start, found.Start + found.Length); Surface.FocusEditor(); }; _navigationResults.Children.Add(button);
        }
    }
    private void FindNext(int direction)
    {
        _matches = Session.Find(_searchQuery, _matchCase, _wholeWord); if (_matches.Count == 0) return;
        var current = _matches.ToList().FindIndex(m => m.Start == Session.Selection.Start && m.Length == Session.Selection.Length);
        _searchMatchIndex = current < 0 ? direction > 0 ? 0 : _matches.Count - 1 : (current + direction + _matches.Count) % _matches.Count;
        var match = _matches[_searchMatchIndex]; Session.SetSelection(match.Start, match.Start + match.Length); RefreshNavigation();
    }
    private void ReplaceOne()
    {
        var matches = Session.Find(_searchQuery, _matchCase, _wholeWord); if (matches.Count == 0) return;
        var match = matches.FirstOrDefault(m => m.Start == Session.Selection.Start && m.Length == Session.Selection.Length) ?? matches.FirstOrDefault(m => m.Start >= Session.Selection.Active) ?? matches[0];
        Session.Replace(match.Start, match.Length, _replaceText, "Replace"); RefreshNavigation();
    }
    private void SetReview(bool visible, string mode)
    {
        _reviewVisible = visible; _reviewMode = mode;
        if (!visible) { _reviewHost.Child = null; _reviewPane = null; return; }
        var pane = new OfficeTaskPane(mode, mode == "Comments" ? 310 : 300); pane.CloseRequested += () => SetReview(false, mode);
        if (mode == "Comments") pane.Toolbar.Children.Add(new RibbonButton("new-comment", "New comment", () => _ = ExecuteCommandAsync("new-comment")));
        _reviewPane = pane; _reviewHost.Child = pane; RefreshReview();
    }
    private void RefreshReview()
    {
        if (_reviewPane is null) return; var body = _reviewPane.Body; body.Children.Clear();
        if (_reviewMode == "Comments")
        {
            var unresolved = Session.Document.Comments.Count(c => !c.Resolved); body.Children.Add(Wrapped($"{unresolved} open · {Session.Document.Comments.Count - unresolved} resolved", 11, OfficeTheme.Muted));
            if (Session.Document.Comments.Count == 0) body.Children.Add(Wrapped("Select text, then choose New Comment to start a conversation about your document.", 12, OfficeTheme.Muted));
            foreach (var comment in Session.Document.Comments.OrderBy(c => c.Start))
            {
                var id = comment.Id; var author = OfficeTheme.Text(comment.Author, 12, OfficeTheme.Ink, true); var avatar = new Border { Width = 27, Height = 27, CornerRadius = new(14), Background = OfficeTheme.Brush("#DDE8F7"), Child = OfficeTheme.Text(comment.Author.Length > 0 ? comment.Author[..1].ToUpperInvariant() : "?", 12, OfficeTheme.Accent, true), Padding = new(8, 2, 0, 0) };
                var header = OfficeTheme.Row(avatar, author); header.Spacing = 8;
                var content = new StackPanel { Spacing = 9, Children = { header, OfficeTheme.Text(comment.Created.ToLocalTime().ToString("MMM d · HH:mm"), 10, OfficeTheme.Muted) } };
                var start = Math.Clamp(comment.Start, 0, Session.Index.Length); var length = Math.Clamp(comment.End - start, 0, Session.Index.Length - start);
                if (length > 0)
                {
                    var quote = Session.Index.Text.Substring(start, Math.Min(length, 120)); var button = new OfficeButton { Content = Wrapped("“" + quote + "”", 11, OfficeTheme.Muted), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(6), RestBackground = "#F5F7FA" };
                    button.Click += (_, _) => { _selectedCommentId = id; Session.SetSelection(comment.Start, comment.End); Surface.FocusEditor(); }; content.Children.Add(button);
                }
                content.Children.Add(Wrapped(comment.Text)); foreach (var reply in comment.Replies) content.Children.Add(new Border { BorderThickness = new(2, 0, 0, 0), BorderBrush = OfficeTheme.Brush("#D6E4F7"), Padding = new(8, 2, 0, 2), Child = Wrapped("You: " + reply, 11) });
                var replyButton = new OfficeButton("Reply", () => _ = ReplyAsync(id)); var resolve = new OfficeButton(comment.Resolved ? "Reopen" : "Resolve", () => RunEdit("Comment", () => Session.ResolveComment(id))); var delete = new RibbonButton("delete", "Delete comment", () => RunEdit("Delete comment", () => Session.DeleteComment(id)));
                content.Children.Add(OfficeTheme.Row(replyButton, resolve, delete)); if (comment.Resolved) content.Children.Add(OfficeTheme.Text("Resolved", 10, "#107C41")); body.Children.Add(Card(content, comment.Resolved ? "#F7F7F7" : "#FFFFFF"));
            }
        }
        else if (_reviewMode == "Changes")
        {
            body.Children.Add(Wrapped("Local text-edit history. Overlapping later edits can make an individual rejection unsafe. Word revision history is not exported in this release.", 11, OfficeTheme.Muted));
            if (Session.Document.Changes.Count == 0) body.Children.Add(Wrapped("No tracked changes. Turn on Track Changes to record new insertions and deletions.", 12, OfficeTheme.Muted));
            foreach (var change in Session.Document.Changes)
            {
                var id = change.Id; var content = new StackPanel { Spacing = 8, Children = { OfficeTheme.Text(change.Author, 12, OfficeTheme.Ink, true), OfficeTheme.Text(change.Created.ToLocalTime().ToString("MMM d · HH:mm"), 10, OfficeTheme.Muted) } };
                if (change.Removed.Length > 0) content.Children.Add(Wrapped("Deleted: " + Abbreviate(change.Removed, 200), 11, "#A4262C"));
                if (change.Inserted.Length > 0) content.Children.Add(Wrapped("Inserted: " + Abbreviate(change.Inserted, 200), 11, "#107C41"));
                var locate = new OfficeButton("Show", () => { Session.SetSelection(change.Start, change.Start + change.Inserted.Length); Surface.FocusEditor(); });
                var accept = new OfficeButton("Accept", () => RunEdit("Accept change", () => Session.AcceptChange(id))); var reject = new OfficeButton("Reject", () => RunEdit("Reject change", () => Session.RejectChange(id))) { IsEnabled = change.CanReject };
                content.Children.Add(OfficeTheme.Row(locate, accept, reject)); if (!change.CanReject) content.Children.Add(Wrapped("Overlapping edit: use Undo or accept this change.", 10, OfficeTheme.Muted)); body.Children.Add(Card(content));
            }
        }
        else if (_reviewMode == "Editor")
        {
            var stats = WritingAnalysis.Statistics(Session.Document); body.Children.Add(OfficeTheme.Text("Writing overview", 16, OfficeTheme.Accent, true)); body.Children.Add(Wrapped($"{stats.Words:N0} words · about {stats.ReadingMinutes} minutes to read", 12));
            body.Children.Add(Wrapped("Offline checks for repeated words, spacing, punctuation spacing, and long sentences. This is not a spelling dictionary or an AI grammar checker.", 11, OfficeTheme.Muted));
            var issues = WritingAnalysis.Check(Session.Document); body.Children.Add(OfficeTheme.Text(issues.Count + " suggestions", 12, OfficeTheme.Ink, true));
            foreach (var issue in issues)
            {
                var content = new StackPanel { Spacing = 8, Children = { OfficeTheme.Text(issue.Kind, 12, OfficeTheme.Ink, true), Wrapped(issue.Message, 11) } };
                var show = new OfficeButton("Show", () => { Session.SetSelection(issue.Start, issue.Start + issue.Length); Surface.FocusEditor(); }); content.Children.Add(show);
                if (issue.Replacement is not null) content.Children.Add(new OfficeButton("Apply suggestion", () => RunEdit("Writing suggestion", () => Session.Replace(issue.Start, issue.Length, issue.Replacement, "Writing suggestion"))) { IsPrimary = true }); body.Children.Add(Card(content));
            }
        }
        else if (_reviewMode == "Compare")
        {
            body.Children.Add(Wrapped("Compared with “" + _comparisonName + "”. This is a paragraph-level text comparison; formatting changes are not included.", 11, OfficeTheme.Muted));
            foreach (var entry in _comparison.Take(200)) body.Children.Add(Card(OfficeTheme.Column(OfficeTheme.Text(entry.Kind, 11, entry.Kind == "Only in current" ? "#107C41" : "#A4262C", true), Wrapped(Abbreviate(entry.Text, 500), 11)), entry.Kind == "Only in current" ? "#F1FAF3" : "#FDF2F2"));
            if (_comparison.Count == 0) body.Children.Add(Wrapped("The paragraph text is identical.", 12, "#107C41"));
        }
    }
    private static string Abbreviate(string text, int max) => text.Length > max ? text[..max] + "…" : text;
    private async Task CompareAsync()
    {
        var file = await Host.OpenFileAsync(".docx,.textspace,.txt,.md"); if (file is null) return;
        var document = ReadDocument(file).Document; var current = Session.Document.Paragraphs().Select(p => p.Text).ToList(); var other = document.Paragraphs().Select(p => p.Text).ToList(); _comparison = []; _comparisonName = file.Name;
        var remaining = other.ToList();
        foreach (var paragraph in current) { var at = remaining.IndexOf(paragraph); if (at >= 0) remaining.RemoveAt(at); else _comparison.Add(("Only in current", paragraph)); }
        foreach (var paragraph in remaining) _comparison.Add(("Only in compared document", paragraph)); SetReview(true, "Compare");
    }
}
