namespace TextSpace.Controls;

/// <summary>Reusable tabbed ribbon. Each tab creates its command groups on demand.</summary>
public sealed class RibbonBar : UserControl
{
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 1, Margin = new(9, 0, 0, 0) };
    private readonly StackPanel _groups = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<string, (OfficeButton Button, Border Underline, Func<IEnumerable<RibbonGroup>> Factory)> _definitions = [];
    private readonly Border _body;
    private string _selected = "";
    public StackPanel Actions { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    public event Action<string>? TabChanged;
    public string SelectedTab => _selected;
    public bool IsCollapsed { get => _body.Visibility == Visibility.Collapsed; set => _body.Visibility = value ? Visibility.Collapsed : Visibility.Visible; }
    public RibbonBar()
    {
        var tabScroll = new ScrollViewer { Content = _tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled };
        var header = OfficeTheme.Columns((tabScroll, -1), (Actions, 0)); header.Height = 34;
        _body = new Border { Background = OfficeTheme.Brush("#FFFFFF"), CornerRadius = new(7), Margin = new(7, 0, 7, 5), Padding = new(3, 2, 3, 2), Child = new ScrollViewer { Content = _groups, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled } };
        Content = OfficeTheme.Rows((header, 34), (_body, 0)); Background = OfficeTheme.Brush(OfficeTheme.Chrome);
    }
    public void AddTab(string name, Func<IEnumerable<RibbonGroup>> factory, bool contextual = false)
    {
        var underline = new Border { Height = 3, Background = OfficeTheme.Brush(contextual ? "#007C78" : OfficeTheme.Accent), CornerRadius = new(2), Margin = new(10, 0, 10, 0), Visibility = Visibility.Collapsed };
        var text = OfficeTheme.Text(name, 12, contextual ? "#007C78" : OfficeTheme.Ink);
        var content = OfficeTheme.Rows((text, -1), (underline, 3)); text.HorizontalAlignment = HorizontalAlignment.Center;
        var button = new OfficeButton { Content = content, Height = 34, Padding = new(11, 0), CornerRadius = new(0) };
        AutomationProperties.SetName(button, name + " tab"); button.Click += (_, _) => SelectTab(name);
        button.DoubleTapped += (_, e) => { IsCollapsed = !IsCollapsed; e.Handled = true; };
        _tabs.Children.Add(button); _definitions[name] = (button, underline, factory);
        if (_selected.Length == 0) SelectTab(name);
    }
    public void AddFileTab(Action open)
    {
        var button = new OfficeButton("File", open) { Height = 34, Padding = new(12, 0), ForegroundOverride = OfficeTheme.Accent };
        AutomationProperties.SetName(button, "File tab"); _tabs.Children.Insert(0, button);
    }
    public void SetTabVisible(string name, bool visible)
    {
        if (!_definitions.TryGetValue(name, out var definition)) return;
        definition.Button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible && _selected == name) SelectTab("Home");
    }
    public void SelectTab(string name)
    {
        if (!_definitions.TryGetValue(name, out var selected)) return;
        _selected = name; _groups.Children.Clear();
        foreach (var definition in _definitions) { definition.Value.Underline.Visibility = definition.Key == name ? Visibility.Visible : Visibility.Collapsed; definition.Value.Button.FontWeight = new() { Weight = (ushort)(definition.Key == name ? 600 : 400) }; }
        foreach (var group in selected.Factory()) _groups.Children.Add(group);
        TabChanged?.Invoke(name);
    }
    public void RefreshTab() { if (_selected.Length > 0) SelectTab(_selected); }
}
