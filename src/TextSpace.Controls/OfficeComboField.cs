namespace TextSpace.Controls;

/// <summary>An editable office field with a custom keyboard-accessible value menu.</summary>
public sealed class OfficeComboField : UserControl
{
    private readonly TextBox _field;
    private readonly OfficeButton _arrow;
    private string _committed = "";
    private bool _updating;
    public event Action<string>? ValueCommitted;
    public string Value { get => _field.Text; set { _updating = true; _field.Text = value; _committed = value; _updating = false; } }
    public OfficeComboField(string label, IEnumerable<string> values, double width = 140)
    {
        Width = width; Height = 26;
        _field = OfficeTheme.Field(label); _field.BorderThickness = new(0); _field.Background = OfficeTheme.Brush("#00FFFFFF"); _field.Padding = new(6, 3); _field.Height = 24;
        _field.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { Commit(); e.Handled = true; } };
        _field.LostFocus += (_, _) => Commit();
        _arrow = new OfficeButton { Content = new OfficeIcon("chevron", 10), Width = 20, Padding = new(4), Height = 24 };
        AutomationProperties.SetName(_arrow, label + " choices");
        var menu = new OfficeMenu(); foreach (var value in values) { var selected = value; menu.Add(value, () => { Value = selected; ValueCommitted?.Invoke(selected); }); }
        _arrow.Flyout = menu.AsFlyout();
        Content = new Border { BorderThickness = new(1), BorderBrush = OfficeTheme.Brush("#B6B6B6"), Background = OfficeTheme.Brush("#FFFFFF"), CornerRadius = new(2), Child = OfficeTheme.Columns((_field, -1), (_arrow, 20)) };
    }
    private void Commit()
    {
        if (_updating || _committed == _field.Text) return; _committed = _field.Text; ValueCommitted?.Invoke(_committed);
    }
}
