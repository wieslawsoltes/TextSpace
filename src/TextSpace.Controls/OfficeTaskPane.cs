namespace TextSpace.Controls;

public sealed class OfficeTaskPane : UserControl
{
    public StackPanel Body { get; } = new() { Spacing = 12, Padding = new(16, 8, 16, 18) };
    public StackPanel Toolbar { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    public event Action? CloseRequested;
    public OfficeTaskPane(string title, double width = 290)
    {
        Width = width; Background = OfficeTheme.Brush("#FFFFFF");
        var heading = OfficeTheme.Text(title, 19); var close = new RibbonButton("close", "Close " + title, () => CloseRequested?.Invoke()) { Width = 26, Height = 26 };
        var header = OfficeTheme.Columns((heading, -1), (Toolbar, 0), (close, 26)); header.Margin = new(16, 14, 10, 6);
        Content = new Border { BorderBrush = OfficeTheme.Brush(OfficeTheme.Border), BorderThickness = new(1, 0, 1, 0), Child = OfficeTheme.Rows((header, 46), (new ScrollViewer { Content = Body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, -1)) };
    }
}
