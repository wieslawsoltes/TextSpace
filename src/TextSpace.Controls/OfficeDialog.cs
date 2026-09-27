namespace TextSpace.Controls;

/// <summary>An embeddable office modal surface. The host owns its overlay layer and focus restoration.</summary>
public sealed class OfficeDialog : UserControl
{
    private readonly TaskCompletionSource<bool> _completion = new();
    public StackPanel Body { get; } = new() { Spacing = 13 };
    public StackPanel Footer { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
    public OfficeButton PrimaryButton { get; }
    public Task<bool> Completion => _completion.Task;
    public OfficeDialog(string title, string primary = "OK", double width = 470)
    {
        Background = OfficeTheme.Brush("#33000000"); IsTabStop = true;
        var heading = OfficeTheme.Text(title, 21); var close = new RibbonButton("close", "Close dialog", () => Close(false));
        var header = OfficeTheme.Columns((heading, -1), (close, 26));
        PrimaryButton = new(primary, () => Close(true)) { IsPrimary = true, MinWidth = 85, Height = 32, Padding = new(18, 5) };
        Footer.Children.Add(PrimaryButton); Footer.Children.Add(new OfficeButton("Cancel", () => Close(false)) { MinWidth = 85, Height = 32, BorderThickness = new(1) });
        var panel = new StackPanel { Spacing = 20, Padding = new(24), Children = { header, new ScrollViewer { Content = Body, MaxHeight = 520, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, Footer } };
        Content = new Border { Background = OfficeTheme.Brush("#FFFFFF"), BorderBrush = OfficeTheme.Brush("#C6C6C6"), BorderThickness = new(1), CornerRadius = new(8), Width = width, MaxWidth = width, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = panel };
        KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { Close(false); e.Handled = true; } };
        Loaded += (_, _) => PrimaryButton.Focus(FocusState.Programmatic);
        AutomationProperties.SetName(this, title + " dialog");
    }
    public void Close(bool accepted) => _completion.TrySetResult(accepted);
    public TextBox AddField(string label, string value = "", bool multiline = false)
    {
        var field = OfficeTheme.Field(label, value, multiline: multiline); if (multiline) field.MinHeight = 90;
        Body.Children.Add(OfficeTheme.Column(OfficeTheme.Text(label, 12), field)); return field;
    }
    public void AddDescription(string text)
    {
        var block = OfficeTheme.Text(text, 12, OfficeTheme.Muted); block.TextWrapping = TextWrapping.Wrap; Body.Children.Add(block);
    }
}
