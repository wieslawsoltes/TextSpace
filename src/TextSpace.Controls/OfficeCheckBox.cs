namespace TextSpace.Controls;

public sealed class OfficeCheckBox : OfficeButton
{
    private readonly OfficeIcon _icon;
    private bool _checked;
    public event Action<bool>? CheckedChanged;
    public bool IsChecked { get => _checked; set { _checked = value; _icon.Glyph = value ? "checked" : "checkbox"; _icon.Color = value ? OfficeTheme.Accent : OfficeTheme.Muted; } }
    public OfficeCheckBox(string label, bool value = false)
    {
        _icon = new("checkbox", 15); var text = OfficeTheme.Text(label, 11);
        Content = OfficeTheme.Row(_icon, text); HorizontalContentAlignment = HorizontalAlignment.Left; Height = 25; Padding = new(4, 2);
        AutomationProperties.SetName(this, label); Click += (_, _) => { IsChecked = !IsChecked; CheckedChanged?.Invoke(IsChecked); }; IsChecked = value;
    }
}
