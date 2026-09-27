namespace TextSpace.Core;

public sealed class TableBlock : Block
{
    public List<TableRow> Rows { get; set; } = [];
    public List<double> ColumnWidths { get; set; } = [];
    public bool HeaderRow { get; set; } = true;
    public bool BandedRows { get; set; } = true;
    public string AccentColor { get; set; } = "#185ABD";
    public double CellPadding { get; set; } = 6;
    public static TableBlock Create(int rows, int columns)
    {
        if (rows is < 1 or > 200 || columns is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(rows));
        var table = new TableBlock { ColumnWidths = Enumerable.Repeat(1d / columns, columns).ToList() };
        for (var row = 0; row < rows; row++)
        {
            var item = new TableRow();
            for (var column = 0; column < columns; column++) item.Cells.Add(new());
            table.Rows.Add(item);
        }
        return table;
    }
}

public sealed class TableRow
{
    public List<TableCell> Cells { get; set; } = [];
}

public sealed class TableCell
{
    public List<Block> Blocks { get; set; } = [new Paragraph()];
    public string? Shading { get; set; }
}
