namespace TextSpace.Controls;

public sealed class OfficeMenu : UserControl
{
    public StackPanel Items { get; } = new() { Spacing = 1, Padding = new(5), MinWidth = 190 };
    public event Action? Invoked;
    public OfficeMenu() => Content = new ScrollViewer { Content = Items, MaxHeight = 520, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    public OfficeMenu Add(string label, Action action, string? glyph = null, string? shortcut = null, bool enabled = true)
    {
        var icon = new OfficeIcon(glyph ?? "document", 16) { Opacity = glyph is null ? 0 : 1, Margin = new(0, 0, 8, 0) };
        var text = OfficeTheme.Text(label); var keys = OfficeTheme.Text(shortcut ?? "", 11, OfficeTheme.Muted); keys.Margin = new(22, 0, 0, 0);
        var row = OfficeTheme.Columns((icon, 24), (text, -1), (keys, 0));
        var button = new OfficeButton { Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch, Height = 31, IsEnabled = enabled, Padding = new(8, 3) };
        AutomationProperties.SetName(button, label); button.Click += (_, _) => { Invoked?.Invoke(); action(); }; Items.Children.Add(button); return this;
    }
    public OfficeMenu Separator() { Items.Children.Add(OfficeTheme.Rule()); return this; }
    public OfficeMenu Heading(string label) { var text = OfficeTheme.Text(label, 11, OfficeTheme.Muted, true); text.Margin = new(8, 7, 8, 5); Items.Children.Add(text); return this; }
    public Flyout AsFlyout()
    {
        var flyout = OfficeTheme.Popup(this); Invoked += flyout.Hide; return flyout;
    }
}
