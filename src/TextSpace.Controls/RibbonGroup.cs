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
    protected override Size MeasureOverride(Size availableSize)
    {
        // Three command rows share the same band as one large command. Do not let
        // the third button overflow the group's fixed-height content area.
        foreach (var column in Body.Children.OfType<StackPanel>())
        {
            if (column.Orientation != Orientation.Vertical || column.Children.Count != 3 ||
                !column.Children.All(c => c is RibbonButton or OfficeCheckBox)) continue;
            column.Spacing = 0;
            foreach (var command in column.Children.OfType<FrameworkElement>()) { command.Height = 22; command.MinHeight = 0; }
        }
        return base.MeasureOverride(availableSize);
    }
}
