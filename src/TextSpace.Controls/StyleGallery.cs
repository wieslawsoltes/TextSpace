using TextSpace.Core;

namespace TextSpace.Controls;

public sealed class StyleGallery : UserControl
{
    private readonly Dictionary<string, OfficeButton> _buttons = [];
    public event Action<string>? StyleInvoked;
    public string SelectedStyle
    {
        set { foreach (var pair in _buttons) { pair.Value.IsSelected = pair.Key == value; pair.Value.BorderThickness = new(pair.Key == value ? 1.5 : 1); } }
    }
    public StyleGallery(IEnumerable<DocumentStyle>? styles = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var style in (styles ?? DocumentStyles.BuiltIn).Take(5))
        {
            var preview = OfficeTheme.Text("AaBbCc", style.Name == "Title" ? 23 : style.Paragraph.OutlineLevel > 0 ? 20 : 18, style.Character.Color, style.Character.Bold);
            preview.FontStyle = style.Character.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
            preview.HorizontalAlignment = HorizontalAlignment.Center;
            var label = OfficeTheme.Text(style.Name, 10); label.HorizontalAlignment = HorizontalAlignment.Center;
            var content = OfficeTheme.Column(preview, label); content.Spacing = 5;
            var button = new OfficeButton { Content = content, Width = 80, Height = 61, Padding = new(4), BorderThickness = new(1), BorderBrush = OfficeTheme.Brush("#E6E6E6"), CornerRadius = new(2) };
            AutomationProperties.SetName(button, style.Name + " style"); var name = style.Name; button.Click += (_, _) => StyleInvoked?.Invoke(name);
            row.Children.Add(button); _buttons[name] = button;
        }
        var more = new RibbonButton("chevron", "More styles", large: false) { Height = 61, Width = 19, Padding = new(3), BorderThickness = new(1) };
        var menu = new OfficeMenu(); foreach (var style in DocumentStyles.BuiltIn) { var name = style.Name; menu.Add(name, () => StyleInvoked?.Invoke(name)); } more.Flyout = menu.AsFlyout(); row.Children.Add(more);
        Content = row; SelectedStyle = "Normal";
    }
}
