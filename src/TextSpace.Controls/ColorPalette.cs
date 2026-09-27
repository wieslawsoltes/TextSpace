namespace TextSpace.Controls;

public sealed class ColorPalette : UserControl
{
    public event Action<string?>? ColorSelected;
    public ColorPalette(bool allowNone = false)
    {
        var root = new StackPanel { Spacing = 8, Padding = new(12), Width = 250 };
        root.Children.Add(OfficeTheme.Text("Theme colors", 12, OfficeTheme.Ink, true));
        string[][] rows =
        [
            ["#FFFFFF", "#000000", "#E7E6E6", "#44546A", "#4472C4", "#ED7D31", "#A5A5A5", "#FFC000", "#5B9BD5", "#70AD47"],
            ["#F2F2F2", "#7F7F7F", "#D0CECE", "#D6DCE4", "#D9E2F3", "#FBE4D5", "#EDEDED", "#FFF2CC", "#DEEBF7", "#E2F0D9"],
            ["#D9D9D9", "#595959", "#AEAAAA", "#ADB9CA", "#B4C6E7", "#F8CBAD", "#DBDBDB", "#FFE699", "#BDD7EE", "#C6E0B4"],
            ["#BFBFBF", "#3F3F3F", "#757171", "#8497B0", "#8EA9DB", "#F4B183", "#C9C9C9", "#FFD966", "#9DC3E6", "#A9D18E"],
            ["#A6A6A6", "#262626", "#3A3838", "#323F4F", "#2F5496", "#C55A11", "#7B7B7B", "#BF9000", "#2E75B6", "#548235"],
            ["#7F7F7F", "#0D0D0D", "#171616", "#222A35", "#203864", "#833C0B", "#525252", "#7F6000", "#1F4E79", "#375623"]
        ];
        StackPanel ColorRow(IEnumerable<string> colors)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            foreach (var color in colors)
            {
                var swatch = new Border { Width = 17, Height = 17, Background = OfficeTheme.Brush(color), BorderBrush = OfficeTheme.Brush("#B6B6B6"), BorderThickness = new(0.5) };
                var button = new OfficeButton { Content = swatch, Padding = new(1), Width = 19, Height = 21 }; button.SetToolTip(color); var selected = color; button.Click += (_, _) => ColorSelected?.Invoke(selected); panel.Children.Add(button);
            }
            return panel;
        }
        var theme = new StackPanel { Spacing = 0 }; foreach (var row in rows) theme.Children.Add(ColorRow(row)); root.Children.Add(theme);
        root.Children.Add(OfficeTheme.Text("Standard colors", 11, OfficeTheme.Muted));
        root.Children.Add(ColorRow(["#C00000", "#FF0000", "#FFC000", "#FFFF00", "#92D050", "#00B050", "#00B0F0", "#0070C0", "#002060", "#7030A0"]));
        if (allowNone) root.Children.Add(new OfficeButton("No color", () => ColorSelected?.Invoke(null)) { HorizontalContentAlignment = HorizontalAlignment.Left });
        var custom = OfficeTheme.Field("Custom color", "#185ABD", 150); var apply = new OfficeButton("Apply", () => { var value = custom.Text; if (value.Length == 7 && value[0] == '#' && value[1..].All(Uri.IsHexDigit)) ColorSelected?.Invoke(value); });
        root.Children.Add(OfficeTheme.Row(custom, apply)); Content = root;
    }
    public Flyout AsFlyout() { var flyout = OfficeTheme.Popup(this); ColorSelected += _ => flyout.Hide(); return flyout; }
}
