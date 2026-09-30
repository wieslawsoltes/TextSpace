using TextSpace.Layout;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private const int ObjectPageSize = 100;
    private OfficeTaskPane? _objectSelectionPane;
    private StackPanel? _objectSelectionRows;
    private TextBlock? _objectSelectionStatus;
    private OfficeButton? _objectListPrevious, _objectListNext, _objectListEdit;
    private string _objectFilter = "";
    private int _objectListPage;
    private IReadOnlyList<VisualObjectLocation>? _objectListSource;
    private readonly List<(string Id, string Label)> _objectMatches = [];
    private readonly Dictionary<string, OfficeButton> _objectSelectionButtons = new(StringComparer.Ordinal);

    private void InitializeObjectNavigation()
    {
        foreach (var tab in new[] { "Home", "View", "Shape Format", "Picture Format", "Equation" })
            Ribbon.InsertGroup(tab, 0, () => Group("Object Selection",
                new RibbonButton("select", "Selection Pane", ShowObjectSelectionPane, large: true),
                new RibbonButton("up", "Previous Object", () => Surface.SelectNextObject(true)),
                new RibbonButton("down", "Next Object", () => Surface.SelectNextObject())));
    }
    private void ReleaseObjectSelectionRows()
    {
        // A closed or displaced pane must not retain the previous document's
        // image bytes through its list of VisualObjectLocation references.
        _objectListSource = null; _objectMatches.Clear();
        _objectSelectionButtons.Clear(); _objectSelectionRows?.Children.Clear();
    }
    private void ShowObjectSelectionPane()
    {
        if (_objectSelectionPane is not null && ReferenceEquals(_reviewHost.Child, _objectSelectionPane)) return;
        ReleaseObjectSelectionRows(); SetReview(false, "Selection");
        var pane = new OfficeTaskPane("Selection", 310);
        _objectSelectionPane = pane; _reviewMode = "Selection"; _reviewVisible = true;
        pane.CloseRequested += () => { ReleaseObjectSelectionRows(); _objectSelectionPane = null; SetReview(false, "Selection"); Surface.FocusEditor(); };
        var filter = OfficeTheme.Field("Find object", _objectFilter); filter.PlaceholderText = "Find a shape, equation, picture…";
        filter.TextChanged += (_, _) => { _objectFilter = filter.Text; _objectListPage = 0; _objectListSource = null; RefreshObjectSelectionPane(); };
        _objectSelectionStatus = OfficeTheme.Text("", 11, OfficeTheme.Muted);
        _objectSelectionStatus.TextWrapping = TextWrapping.Wrap;
        _objectSelectionRows = new StackPanel { Spacing = 3 };
        _objectListPrevious = new OfficeButton("Previous results", () => { _objectListPage--; RenderObjectSelectionPage(); });
        _objectListNext = new OfficeButton("Next results", () => { _objectListPage++; RenderObjectSelectionPage(); });
        _objectListEdit = new OfficeButton("Edit selected", () => Surface.BeginObjectEditor());
        pane.Body.Children.Add(filter);
        pane.Body.Children.Add(Wrapped("Select objects even when they overlap. Tab / Shift+Tab cycles objects on the paper; Enter edits shape text or an equation.", 11, OfficeTheme.Muted));
        pane.Body.Children.Add(OfficeTheme.Row(new OfficeButton("Previous object", () => Surface.SelectNextObject(true)), new OfficeButton("Next object", () => Surface.SelectNextObject())));
        pane.Body.Children.Add(OfficeTheme.Row(_objectListEdit, new OfficeButton("Clear selection", () => { Surface.SelectObject(null); Surface.FocusEditor(); })));
        pane.Body.Children.Add(_objectSelectionStatus);
        pane.Body.Children.Add(OfficeTheme.Row(_objectListPrevious, _objectListNext));
        pane.Body.Children.Add(_objectSelectionRows);
        _reviewHost.Child = pane; RefreshObjectSelectionPane();
    }
    private void RefreshObjectSelectionPane()
    {
        if (_objectSelectionPane is null || !ReferenceEquals(_reviewHost.Child, _objectSelectionPane))
        {
            if (_objectListSource is not null || _objectSelectionButtons.Count != 0) ReleaseObjectSelectionRows();
            return;
        }
        var source = Surface.ObjectLocations;
        if (!ReferenceEquals(source, _objectListSource))
        {
            _objectListSource = source; _objectMatches.Clear();
            foreach (var location in source)
            {
                var block = location.Item.Object;
                var kind = block is ShapeBlock shape ? shape.Kind.ToString() : block is EquationBlock ? "Equation" : "Picture";
                var text = block switch { ShapeBlock s => s.Text, EquationBlock e => e.Root.ToLinearText(), ImageBlock image => image.AltText, _ => "" };
                var fullLabel = kind + " · Page " + (location.PageIndex + 1) + (text.Length == 0 ? "" : " · " + text);
                if (_objectFilter.Length > 0 && !fullLabel.Contains(_objectFilter, StringComparison.OrdinalIgnoreCase) && !block.Id.Contains(_objectFilter, StringComparison.OrdinalIgnoreCase)) continue;
                var length = Math.Min(80, text.Length);
                if (length < text.Length && length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
                var excerpt = text[..length].Replace('\r', ' ').Replace('\n', ' ');
                _objectMatches.Add((block.Id, kind + " · Page " + (location.PageIndex + 1) + (excerpt.Length == 0 ? "" : "\n" + excerpt)));
            }
            RenderObjectSelectionPage();
        }
        else RefreshObjectSelectionHighlight();
    }
    private void RenderObjectSelectionPage()
    {
        if (_objectSelectionRows is null || _objectSelectionStatus is null) return;
        var pages = Math.Max(1, (_objectMatches.Count + ObjectPageSize - 1) / ObjectPageSize);
        _objectListPage = Math.Clamp(_objectListPage, 0, pages - 1);
        _objectSelectionRows.Children.Clear(); _objectSelectionButtons.Clear();
        var start = _objectListPage * ObjectPageSize; var end = Math.Min(start + ObjectPageSize, _objectMatches.Count);
        for (var i = start; i < end; i++)
        {
            var entry = _objectMatches[i];
            var button = new OfficeButton { Content = Wrapped(entry.Label, 11), Padding = new(9, 7), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(button, "Select object: " + entry.Id);
            button.Click += (_, _) => { if (Surface.IsObjectEditorOpen || Surface.IsVisualGestureActive) return; Surface.SelectObject(entry.Id, true); Surface.FocusEditor(); };
            _objectSelectionRows.Children.Add(button); _objectSelectionButtons.Add(entry.Id, button);
        }
        _objectSelectionStatus.Text = _objectMatches.Count == 0 ? "No matching objects." : $"{_objectMatches.Count} objects · showing {start + 1}–{end}";
        _objectListPrevious!.IsEnabled = _objectListPage > 0; _objectListNext!.IsEnabled = _objectListPage + 1 < pages;
        RefreshObjectSelectionHighlight();
    }
    private void RefreshObjectSelectionHighlight()
    {
        foreach (var pair in _objectSelectionButtons)
        {
            pair.Value.IsSelected = pair.Key == Surface.SelectedObjectId;
            pair.Value.IsEnabled = !Surface.IsObjectEditorOpen && !Surface.IsVisualGestureActive;
        }
        if (_objectListEdit is not null) _objectListEdit.IsEnabled = !Session.IsReadOnly && !Surface.IsObjectEditorOpen && Surface.SelectedObject is ShapeBlock or EquationBlock;
    }
}
