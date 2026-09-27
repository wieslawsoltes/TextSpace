namespace TextSpace.Controls;

public sealed class RibbonGroup : UserControl
{
    public StackPanel Body { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Top };
    public RibbonGroup(string title, Action? launch = null)
    {
        var caption = OfficeTheme.Text(title, 10, OfficeTheme.Muted); caption.HorizontalAlignment = HorizontalAlignment.Center;
        var footer = new Grid { Height = 18 }; footer.Children.Add(caption);
        if (launch is not null)
        {
            var button = new RibbonButton("launch", title + " settings", launch) { Width = 16, Height = 16, Padding = new(3), HorizontalAlignment = HorizontalAlignment.Right };
            footer.Children.Add(button);
        }
        var grid = OfficeTheme.Rows((Body, 66), (footer, 18));
        Content = new Border { Child = grid, BorderBrush = OfficeTheme.Brush(OfficeTheme.Border), BorderThickness = new(0, 0, 1, 0), Padding = new(7, 3, 7, 0) };
        AutomationProperties.SetName(this, title + " ribbon group");
    }
}
