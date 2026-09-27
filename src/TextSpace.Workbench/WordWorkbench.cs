using System.Globalization;
using TextSpace.Editor;

namespace TextSpace.Workbench;

/// <summary>Office-style application shell assembled from independently reusable Uno controls and engines.</summary>
public sealed partial class WordWorkbench : UserControl, IDisposable
{
    private readonly Grid _root = new(), _overlay = new();
    private readonly Grid _workspace = new();
    private readonly Border _navigationHost = new(), _reviewHost = new();
    private readonly Border _notice = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _noticeText = OfficeTheme.Text("", 12);
    private readonly TextBlock _documentTitle = OfficeTheme.Text("", 12, OfficeTheme.Ink, true);
    private readonly TextBlock _saveState = OfficeTheme.Text("Saved locally", 10, OfficeTheme.Muted);
    private readonly TextBlock _pageStatus = OfficeTheme.Text("Page 1 of 1", 10);
    private readonly TextBlock _wordStatus = OfficeTheme.Text("", 10);
    private readonly TextBlock _status = OfficeTheme.Text("", 10, OfficeTheme.Muted);
    private readonly OfficeZoomSlider _zoomSlider = new();
    private readonly OfficeButton _zoomLabel = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(850) };
    private readonly Dictionary<string, RibbonButton> _buttons = [];
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private OfficeComboField? _fontField, _sizeField;
    private StyleGallery? _styleGallery;
    private Grid? _titleBar;
    private Border? _backstage;
    private bool _nativeUiRefreshPending;
    private bool _disposed, _autoSave = true, _focusMode, _busy, _reviewVisible, _navigationVisible;
    private string _navigationMode = "Headings", _reviewMode = "Comments";
    private string _searchQuery = "", _replaceText = "";
    private int _searchMatchIndex;
    private TextBox? _searchField;
    private MergeData? _mergeData;
    public EditorSession Session { get; }
    public IWorkspaceHost Host { get; }
    public DocumentSurface Surface { get; }
    public RibbonBar Ribbon { get; } = new();
    public string StatusText => _status.Text;
    public bool IsDialogOpen => _overlay.Children.Count > 0;
    public event Action? StateChanged;

    public WordWorkbench(EditorSession session, IWorkspaceHost host)
    {
        Session = session; Host = host; Surface = new(session);
        FontFamily = OfficeTheme.Font; RequestedTheme = ElementTheme.Light;
        _workspace.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _workspace.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _workspace.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Grid.SetColumn(_navigationHost, 0); Grid.SetColumn(Surface, 1); Grid.SetColumn(_reviewHost, 2); _workspace.Children.Add(_navigationHost); _workspace.Children.Add(Surface); _workspace.Children.Add(_reviewHost);
        _titleBar = CreateTitleBar(); ConfigureRibbon(); InitializeDocumentNavigation();
        _noticeText.TextWrapping = TextWrapping.Wrap; var dismiss = new RibbonButton("close", "Dismiss notification", () => _notice.Visibility = Visibility.Collapsed);
        _notice.Child = OfficeTheme.Columns((_noticeText, -1), (dismiss, 26)); _notice.Background = OfficeTheme.Brush("#FFF4CE"); _notice.Padding = new(16, 5, 10, 5);
        var main = OfficeTheme.Rows((_titleBar, 42), (Ribbon, 0), (_notice, 0), (_workspace, -1), (CreateStatusBar(), 25));
        _overlay.IsHitTestVisible = false; _root.Children.Add(main); _root.Children.Add(_overlay); Content = _root;
        Surface.CommandRequested += id => _ = ExecuteCommandAsync(id); Surface.Error += message => Notify(message, true); Surface.ViewChanged += RefreshStatus;
        Surface.ContextRequested += _ => ShowContextMenu();
        Ribbon.TabChanged += _ => RefreshFormatting();
        Session.Changed += OnSessionChanged;
        _saveTimer.Tick += async (_, _) => { _saveTimer.Stop(); await SaveRecoveryAsync(); };
        Loaded += (_, _) => { RefreshStatus(); RefreshFormatting(); };
        SizeChanged += (_, e) =>
        {
            if (e.NewSize.Width < 900 && _reviewVisible && _navigationVisible) SetNavigation(false);
            if (_titleBar?.Tag is FrameworkElement search) search.Visibility = e.NewSize.Width < 1000 ? Visibility.Collapsed : Visibility.Visible;
        };
        RefreshStatus(); RefreshFormatting();
    }
    private Grid CreateTitleBar()
    {
        var brand = new OfficeButton { Content = OfficeTheme.Text("T", 20, "#FFFFFF", true), Width = 29, Height = 29, RestBackground = OfficeTheme.Accent, CornerRadius = new(3), Padding = new(0), Margin = new(0, 0, 11, 0) };
        brand.Click += (_, _) => ShowBackstage(); brand.SetToolTip("TextSpace · File");
        var autoLabel = OfficeTheme.Text("AutoSave", 11); var auto = new OfficeButton { Height = 19, Width = 37, Padding = new(2), CornerRadius = new(10), IsPrimary = true };
        var knob = new Border { Width = 13, Height = 13, Background = OfficeTheme.Brush("#FFFFFF"), CornerRadius = new(8), HorizontalAlignment = HorizontalAlignment.Right };
        var track = new Grid { Width = 30, Children = { knob } }; auto.Content = track; auto.SetToolTip("AutoSave to " + Host.StorageDescription);
        auto.Click += (_, _) => { _autoSave = !_autoSave; auto.IsPrimary = _autoSave; knob.HorizontalAlignment = _autoSave ? HorizontalAlignment.Right : HorizontalAlignment.Left; _saveState.Text = _autoSave ? "AutoSave on" : "AutoSave off"; if (_autoSave) _ = SaveRecoveryAsync(); };
        var save = Tool("save", "Save a copy (Ctrl+S)", "save"); var undo = Tool("undo", "Undo (Ctrl+Z)", "undo"); var redo = Tool("redo", "Redo (Ctrl+Y)", "redo");
        var left = OfficeTheme.Row(brand, autoLabel, auto, save, undo, redo); left.Spacing = 6;
        var title = new OfficeButton { Content = OfficeTheme.Row(_documentTitle, new OfficeIcon("chevron", 11)), Padding = new(12, 3), HorizontalContentAlignment = HorizontalAlignment.Left, MaxWidth = 390 };
        title.SetToolTip("Rename document"); title.Click += (_, _) => _ = ExecuteCommandAsync("rename");
        var search = new OfficeButton { Content = OfficeTheme.Row(new OfficeIcon("search", 15, "#666666"), OfficeTheme.Text("Search for tools, help, and more", 11, "#666666")), Width = 295, Height = 28, RestBackground = "#FAFBFC", BorderThickness = new(1), BorderBrush = OfficeTheme.Brush("#D8DEE7"), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(12, 4) };
        search.Click += (_, _) => _ = ExecuteCommandAsync("command-search"); search.SetToolTip("Search commands (Alt+Q)");
        var avatar = new OfficeButton { Content = OfficeTheme.Text("Y", 12, OfficeTheme.Accent, true), Width = 27, Height = 27, RestBackground = "#DDE8F7", CornerRadius = new(16), Padding = new(0) }; avatar.SetToolTip("Local workspace · You"); avatar.Click += (_, _) => _ = ExecuteCommandAsync("about");
        var right = OfficeTheme.Row(_saveState, Tool("help", "Help", "help"), avatar); right.Spacing = 13; right.Margin = new(16, 0, 0, 0);
        var grid = OfficeTheme.Columns((left, 0), (title, -1), (search, 0), (right, 0)); grid.Padding = new(12, 4, 14, 4); grid.Background = OfficeTheme.Brush(OfficeTheme.TitleBar); grid.Tag = search; return grid;
    }
    private Grid CreateStatusBar()
    {
        var pageButton = new OfficeButton { Content = _pageStatus, Padding = new(9, 2), Height = 24 }; pageButton.Click += (_, _) => _ = ExecuteCommandAsync("go-to"); pageButton.SetToolTip("Go to page");
        var wordButton = new OfficeButton { Content = _wordStatus, Padding = new(9, 2), Height = 24 }; wordButton.Click += (_, _) => _ = ExecuteCommandAsync("word-count"); wordButton.SetToolTip("Word count");
        var left = OfficeTheme.Row(pageButton, wordButton, OfficeTheme.Text("English (United States)", 10, OfficeTheme.Muted), _status); left.Spacing = 10;
        var focus = Tool("focus", "Focus", "focus", showLabel: true); focus.Height = 24;
        var reading = Tool("book", "Read mode", "read-mode"); reading.Height = 24;
        var print = Tool("document", "Print layout", "print-layout"); print.Height = 24;
        var minus = new RibbonButton("minus", "Zoom out", () => Surface.SetZoom(Surface.Zoom - 0.1)) { Width = 23, Height = 23 };
        var plus = new RibbonButton("plus", "Zoom in", () => Surface.SetZoom(Surface.Zoom + 0.1)) { Width = 23, Height = 23 };
        _zoomLabel.Height = 24; _zoomLabel.Width = 45; _zoomLabel.Padding = new(2); _zoomLabel.FontSize = 10; _zoomLabel.Click += (_, _) => _ = ExecuteCommandAsync("zoom"); AutomationProperties.SetName(_zoomLabel, "Zoom percentage");
        _zoomSlider.ValueChanged += value => Surface.SetZoom(value);
        var right = OfficeTheme.Row(focus, reading, print, minus, _zoomSlider, plus, _zoomLabel); right.Margin = new(4, 0, 10, 0);
        var grid = OfficeTheme.Columns((left, -1), (right, 0)); grid.Background = OfficeTheme.Brush("#F7F7F7"); return grid;
    }
    private void OnSessionChanged(object? sender, EditorChangedEventArgs e)
    {
        if (_disposed) return;
        if (Surface.IsProcessingNativeInput)
        {
            if (!_nativeUiRefreshPending)
            {
                _nativeUiRefreshPending = true;
                DispatcherQueue.TryEnqueue(() =>
                {
                    _nativeUiRefreshPending = false;
                    if (!_disposed) OnSessionChanged(Session, new(EditorChangeKind.Document, "Typing"));
                });
            }
            return;
        }
        RefreshStatus(); RefreshFormatting();
        if (e.Kind == EditorChangeKind.Document)
        {
            if (_autoSave) { _saveTimer.Stop(); _saveTimer.Start(); _saveState.Text = "Saving…"; }
            if (_navigationVisible) RefreshNavigation(); if (_reviewVisible) RefreshReview();
        }
        StateChanged?.Invoke();
    }
    private void RefreshStatus()
    {
        var caret = Surface.Layout.Caret(Session.Selection.Active); _pageStatus.Text = $"Page {caret.PageIndex + 1} of {Surface.Layout.Pages.Count}";
        _wordStatus.Text = Session.Selection.IsEmpty ? $"{Session.Document.WordCount:N0} words" : $"{System.Text.RegularExpressions.Regex.Matches(Session.SelectedText(), @"\b[\p{L}\p{N}]+\b").Count:N0} of {Session.Document.WordCount:N0} words";
        _documentTitle.Text = Session.Document.Title; _zoomSlider.Value = Surface.Zoom; _zoomLabel.Content = Math.Round(Surface.Zoom * 100).ToString(CultureInfo.InvariantCulture) + "%";
        Ribbon.SetTabVisible("Table Design", Session.CurrentTable is not null); Ribbon.SetTabVisible("Table Layout", Session.CurrentTable is not null); Ribbon.SetTabVisible("Picture Format", Surface.SelectedImageId is not null);
        StateChanged?.Invoke();
    }
    private void RefreshFormatting()
    {
        var style = Session.TypingStyle; var paragraph = Session.CurrentParagraph.Format;
        void Selected(string id, bool value) { if (_buttons.TryGetValue(id, out var button)) button.IsSelected = value; }
        Selected("bold", style.Bold); Selected("italic", style.Italic); Selected("underline", style.Underline); Selected("strike", style.StrikeThrough); Selected("superscript", style.Superscript); Selected("subscript", style.Subscript);
        Selected("align-left", paragraph.Alignment == TextSpace.Core.TextAlignment.Left); Selected("align-center", paragraph.Alignment == TextSpace.Core.TextAlignment.Center); Selected("align-right", paragraph.Alignment == TextSpace.Core.TextAlignment.Right); Selected("justify", paragraph.Alignment == TextSpace.Core.TextAlignment.Justify);
        Selected("bullets", paragraph.List == ListKind.Bullet); Selected("numbering", paragraph.List == ListKind.Number); Selected("format-marks", Surface.ShowFormatting); Selected("track", Session.TrackChanges);
        if (_buttons.TryGetValue("undo", out var undo)) undo.IsEnabled = Session.CanUndo; if (_buttons.TryGetValue("redo", out var redo)) redo.IsEnabled = Session.CanRedo;
        if (_fontField is not null) _fontField.Value = style.FontFamily; if (_sizeField is not null) _sizeField.Value = style.FontSize.ToString("0.#", CultureInfo.InvariantCulture); if (_styleGallery is not null) _styleGallery.SelectedStyle = paragraph.StyleName;
    }
    private RibbonButton Tool(string glyph, string label, string id, bool large = false, bool showLabel = false, bool dropdown = false)
    {
        var button = new RibbonButton(glyph, label, () => _ = ExecuteCommandAsync(id), large, showLabel, dropdown) { CommandId = id }; _buttons[id] = button; return button;
    }
    private void Notify(string message, bool warning = false)
    {
        _status.Text = message.Length > 75 ? message[..72] + "…" : message;
        if (warning) { _noticeText.Text = message; _notice.Visibility = Visibility.Visible; }
        StateChanged?.Invoke();
    }
    private async Task<bool> ShowDialogAsync(OfficeDialog dialog)
    {
        if (_overlay.Children.Count > 0) return false;
        _overlay.IsHitTestVisible = true; _overlay.Children.Add(dialog);
        try { return await dialog.Completion; }
        finally { _overlay.Children.Remove(dialog); _overlay.IsHitTestVisible = false; Surface.FocusEditor(); StateChanged?.Invoke(); }
    }
    private void ShowContextMenu()
    {
        var menu = new OfficeMenu().Add("Cut", () => _ = ExecuteCommandAsync("cut"), "cut", "Ctrl+X").Add("Copy", () => _ = ExecuteCommandAsync("copy"), "copy", "Ctrl+C").Add("Paste", () => _ = ExecuteCommandAsync("paste"), "paste", "Ctrl+V").Separator().Add("Font…", () => _ = ExecuteCommandAsync("font-dialog"), "font").Add("Paragraph…", () => _ = ExecuteCommandAsync("paragraph-dialog"), "paragraph").Add("New comment", () => _ = ExecuteCommandAsync("new-comment"), "new-comment");
        menu.AsFlyout().ShowAt(Surface);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _saveTimer.Stop(); Session.Changed -= OnSessionChanged; Surface.Dispose(); _saveGate.Dispose();
    }
}
