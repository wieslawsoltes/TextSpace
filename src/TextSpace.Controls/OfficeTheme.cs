using System.Globalization;

namespace TextSpace.Controls;

/// <summary>Shared office design tokens. Hosts may supply their own installed or bundled UI font.</summary>
public static class OfficeTheme
{
    public const string Accent = "#185ABD";
    public const string Ink = "#242424";
    public const string Muted = "#616161";
    public const string Chrome = "#F3F3F3";
    public const string TitleBar = "#EFF3F8";
    public const string Border = "#D6D6D6";
    public const string Hover = "#E8E8E8";
    public const string Selection = "#D9E8FA";
    public static FontFamily Font { get; set; } = new("Segoe UI");
    public static SolidColorBrush Brush(string hex)
    {
        var text = hex.TrimStart('#'); var value = uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new(Windows.UI.Color.FromArgb(text.Length == 8 ? (byte)(value >> 24) : (byte)255, (byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }
    public static TextBlock Text(string text, double size = 12, string color = Ink, bool bold = false) => new() { Text = text, FontFamily = Font, FontSize = size, Foreground = Brush(color), FontWeight = new() { Weight = (ushort)(bold ? 600 : 400) }, VerticalAlignment = VerticalAlignment.Center };
    public static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 }; foreach (var child in children) row.Children.Add(child); return row;
    }
    public static StackPanel Column(params UIElement[] children)
    {
        var column = new StackPanel { Spacing = 2 }; foreach (var child in children) column.Children.Add(child); return column;
    }
    public static Border Rule(bool vertical = false) => new() { Background = Brush(Border), Width = vertical ? 1 : double.NaN, Height = vertical ? double.NaN : 1, Margin = vertical ? new(5, 3, 5, 3) : new(0, 5, 0, 5) };
    public static Grid Columns(params (UIElement Element, double Width)[] items)
    {
        var grid = new Grid(); var index = 0;
        foreach (var item in items)
        {
            grid.ColumnDefinitions.Add(new() { Width = item.Width < 0 ? new(1, GridUnitType.Star) : item.Width == 0 ? GridLength.Auto : new(item.Width) });
            Grid.SetColumn(item.Element, index++); grid.Children.Add(item.Element);
        }
        return grid;
    }
    public static Grid Rows(params (UIElement Element, double Height)[] items)
    {
        var grid = new Grid(); var index = 0;
        foreach (var item in items)
        {
            grid.RowDefinitions.Add(new() { Height = item.Height < 0 ? new(1, GridUnitType.Star) : item.Height == 0 ? GridLength.Auto : new(item.Height) });
            Grid.SetRow(item.Element, index++); grid.Children.Add(item.Element);
        }
        return grid;
    }
    public static TextBox Field(string label, string text = "", double width = double.NaN, bool multiline = false)
    {
        var field = new TextBox { Style = (Style)OfficeResources.Current["Office.TextBox"], FontFamily = Font, Text = text, Width = width, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap };
        AutomationProperties.SetName(field, label); return field;
    }
    public static Flyout Popup(UIElement content) => new() { Content = content, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft, FlyoutPresenterStyle = (Style)OfficeResources.Current["Office.Flyout"] };
}
