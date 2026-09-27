namespace TextSpace.Controls;

/// <summary>A fully templated, keyboard-accessible office button with checked and primary treatments.</summary>
public class OfficeButton : Button
{
    private bool _hover;
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(OfficeButton), new PropertyMetadata(false, OnAppearanceChanged));
    public static readonly DependencyProperty IsPrimaryProperty = DependencyProperty.Register(nameof(IsPrimary), typeof(bool), typeof(OfficeButton), new PropertyMetadata(false, OnAppearanceChanged));
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsPrimary { get => (bool)GetValue(IsPrimaryProperty); set => SetValue(IsPrimaryProperty, value); }
    public string RestBackground { get; set; } = "#00FFFFFF";
    public string? ForegroundOverride { get; set; }
    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((OfficeButton)d).Refresh();
    public OfficeButton()
    {
        Style = (Style)OfficeResources.Current["Office.Button"]; FontFamily = OfficeTheme.Font;
        PointerEntered += (_, _) => { _hover = true; Refresh(); }; PointerExited += (_, _) => { _hover = false; Refresh(); };
        Refresh();
    }
    public OfficeButton(string text, Action action) : this()
    {
        Content = text; AutomationProperties.SetName(this, text); Click += (_, _) => action();
    }
    public void SetToolTip(string text) { ToolTipService.SetToolTip(this, text); AutomationProperties.SetName(this, text.Split('\n')[0]); }
    protected virtual void Refresh()
    {
        Background = OfficeTheme.Brush(IsPrimary ? OfficeTheme.Accent : IsSelected ? OfficeTheme.Selection : _hover ? OfficeTheme.Hover : RestBackground);
        Foreground = OfficeTheme.Brush(ForegroundOverride ?? (IsPrimary ? "#FFFFFF" : OfficeTheme.Ink));
        BorderBrush = OfficeTheme.Brush(IsSelected ? "#A4C0E8" : OfficeTheme.Border);
    }
}
