namespace TextSpace.Controls;

/// <summary>A templated, keyboard-accessible office button with bindable checked, primary and color treatments.</summary>
public class OfficeButton : Button
{
    private bool _hover;
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(OfficeButton), new PropertyMetadata(false, OnAppearanceChanged));
    public static readonly DependencyProperty IsPrimaryProperty = DependencyProperty.Register(nameof(IsPrimary), typeof(bool), typeof(OfficeButton), new PropertyMetadata(false, OnAppearanceChanged));
    public static readonly DependencyProperty RestBackgroundProperty = DependencyProperty.Register(nameof(RestBackground), typeof(string), typeof(OfficeButton), new PropertyMetadata("#00FFFFFF", OnAppearanceChanged));
    public static readonly DependencyProperty ForegroundOverrideProperty = DependencyProperty.Register(nameof(ForegroundOverride), typeof(string), typeof(OfficeButton), new PropertyMetadata(null, OnAppearanceChanged));
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsPrimary { get => (bool)GetValue(IsPrimaryProperty); set => SetValue(IsPrimaryProperty, value); }
    public string RestBackground { get => (string)GetValue(RestBackgroundProperty); set => SetValue(RestBackgroundProperty, value); }
    public string? ForegroundOverride { get => (string?)GetValue(ForegroundOverrideProperty); set => SetValue(ForegroundOverrideProperty, value); }
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
