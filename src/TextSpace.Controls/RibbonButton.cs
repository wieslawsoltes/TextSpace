namespace TextSpace.Controls;

public sealed class RibbonButton : OfficeButton
{
    private readonly OfficeIcon? _icon;
    private readonly TextBlock? _label;
    public string CommandId { get; set; } = "";
    public bool Large { get; }
    public RibbonButton(string glyph, string label, Action? action = null, bool large = false, bool showLabel = false, bool dropdown = false)
    {
        Large = large; Height = large ? 66 : 25; MinWidth = large ? 52 : 26; Padding = large ? new(6, 4) : new(5, 3);
        var items = new StackPanel { Orientation = large ? Orientation.Vertical : Orientation.Horizontal, Spacing = large ? 3 : 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(glyph)) { _icon = new(glyph, large ? 27 : 16); items.Children.Add(_icon); }
        if (showLabel || large || string.IsNullOrEmpty(glyph))
        {
            _label = OfficeTheme.Text(label, large ? 11 : 12); _label.TextAlignment = TextAlignment.Center; _label.HorizontalAlignment = HorizontalAlignment.Center;
            items.Children.Add(_label);
        }
        if (dropdown) items.Children.Add(new OfficeIcon("chevron", large ? 10 : 9) { HorizontalAlignment = HorizontalAlignment.Center });
        Content = items; SetToolTip(label);
        if (action is not null) Click += (_, _) => action();
    }
    public void SetIconColor(string color) { if (_icon is not null) _icon.Color = color; }
    protected override void Refresh()
    {
        base.Refresh(); if (_icon is not null) _icon.Color = IsPrimary ? "#FFFFFF" : OfficeTheme.Ink;
        if (_label is not null) _label.Foreground = OfficeTheme.Brush(IsPrimary ? "#FFFFFF" : OfficeTheme.Ink);
    }
}
