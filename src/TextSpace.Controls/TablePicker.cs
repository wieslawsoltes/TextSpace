namespace TextSpace.Controls;

public sealed class TablePicker : UserControl
{
    private readonly List<(int Row, int Column, Border Tile)> _tiles = [];
    private readonly TextBlock _label = OfficeTheme.Text("Insert table", 12, OfficeTheme.Ink, true);
    public event Action<int, int>? TableSelected;
    public TablePicker()
    {
        var grid = new Grid { RowSpacing = 3, ColumnSpacing = 3 };
        for (var r = 0; r < 8; r++) grid.RowDefinitions.Add(new() { Height = new(18) });
        for (var c = 0; c < 10; c++) grid.ColumnDefinitions.Add(new() { Width = new(18) });
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 10; c++)
            {
                var row = r + 1; var column = c + 1; var tile = new Border { BorderBrush = OfficeTheme.Brush("#A0A0A0"), BorderThickness = new(1), Background = OfficeTheme.Brush("#FFFFFF"), Width = 18, Height = 18 };
                var button = new OfficeButton { Content = tile, Padding = new(0), Width = 18, Height = 18, CornerRadius = new(0) };
                AutomationProperties.SetName(button, $"Insert {row} by {column} table"); button.PointerEntered += (_, _) => Highlight(row, column); button.GotFocus += (_, _) => Highlight(row, column); button.Click += (_, _) => TableSelected?.Invoke(row, column);
                Grid.SetRow(button, r); Grid.SetColumn(button, c); grid.Children.Add(button); _tiles.Add((row, column, tile));
            }
        }
        Content = new StackPanel { Spacing = 12, Padding = new(14), Children = { _label, grid, OfficeTheme.Text("Choose the number of rows and columns.", 10, OfficeTheme.Muted) } };
    }
    private void Highlight(int row, int column)
    {
        _label.Text = $"{column} × {row} table";
        foreach (var tile in _tiles) { var selected = tile.Row <= row && tile.Column <= column; tile.Tile.Background = OfficeTheme.Brush(selected ? OfficeTheme.Selection : "#FFFFFF"); tile.Tile.BorderBrush = OfficeTheme.Brush(selected ? OfficeTheme.Accent : "#A0A0A0"); }
    }
    public Flyout AsFlyout() { var flyout = OfficeTheme.Popup(this); TableSelected += (_, _) => flyout.Hide(); return flyout; }
}
