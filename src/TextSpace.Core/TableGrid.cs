namespace TextSpace.Core;

/// <summary>An anchor cell and its half-open rectangle in the table's logical grid.</summary>
public sealed record TableCellRegion(TableCell Cell, int Row, int Column, int RowSpan, int ColumnSpan)
{
    public int RowEnd => Row + RowSpan;
    public int ColumnEnd => Column + ColumnSpan;
    public bool Contains(int row, int column) => row >= Row && row < RowEnd && column >= Column && column < ColumnEnd;
}

/// <summary>
/// Validated, immutable topology snapshot over a dense table. Spans live on the top-left
/// anchor; covered slots contain unit-span, empty cells, never duplicated document text.
/// Rebuild after topology edits. Construction and coordinate lookup are O(rows*columns) and O(1).
/// </summary>
public sealed class TableGrid
{
    private readonly TableCellRegion[,] _slots;
    private readonly IReadOnlyList<TableCellRegion> _regions;
    public int RowCount { get; }
    public int ColumnCount { get; }
    public IReadOnlyList<TableCellRegion> Regions => _regions;

    public TableGrid(TableBlock table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Rows is null || table.Rows.Count is < 1 or > 200 || table.Rows.Any(r => r?.Cells is null))
            throw new InvalidDataException("A table requires 1–200 non-null rows.");
        RowCount = table.Rows.Count;
        ColumnCount = table.Rows.Max(r => r.Cells.Count);
        if (ColumnCount is < 1 or > 20 || table.Rows.Any(r => r.Cells.Count != ColumnCount))
            throw new InvalidDataException("Table rows must share a 1–20-column logical grid.");
        _slots = new TableCellRegion[RowCount, ColumnCount];
        var regions = new List<TableCellRegion>();
        var identities = new HashSet<TableCell>(ReferenceEqualityComparer.Instance);
        for (var row = 0; row < RowCount; row++)
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                var cell = table.Rows[row].Cells[column];
                if (cell is null || cell.Blocks is null || !identities.Add(cell))
                    throw new InvalidDataException("Table slots require independent cells and non-null content.");
                if (cell.RowSpan < 1 || cell.RowSpan > RowCount - row || cell.ColumnSpan < 1 || cell.ColumnSpan > ColumnCount - column
                    || !Enum.IsDefined(cell.VerticalAlignment))
                    throw new InvalidDataException("A cell span or alignment lies outside the table grid.");
                if (_slots[row, column] is not null)
                {
                    if (cell.RowSpan != 1 || cell.ColumnSpan != 1 || cell.Blocks.Count != 0)
                        throw new InvalidDataException("Covered table slots cannot contain content or another span.");
                    continue;
                }
                var region = new TableCellRegion(cell, row, column, cell.RowSpan, cell.ColumnSpan);
                for (var y = row; y < region.RowEnd; y++)
                {
                    for (var x = column; x < region.ColumnEnd; x++)
                    {
                        if (_slots[y, x] is not null) throw new InvalidDataException("Merged table cells overlap.");
                        _slots[y, x] = region;
                    }
                }
                regions.Add(region);
            }
        }
        _regions = regions.AsReadOnly();
    }

    public TableCellRegion At(int row, int column)
    {
        if ((uint)row >= (uint)RowCount) throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column >= (uint)ColumnCount) throw new ArgumentOutOfRangeException(nameof(column));
        return _slots[row, column];
    }
    public bool IsAnchor(int row, int column)
    {
        var region = At(row, column); return region.Row == row && region.Column == column;
    }
    public TableCellRegion RegionOf(Paragraph paragraph) => _regions.FirstOrDefault(r => DocumentModel.Walk(r.Cell.Blocks).Contains(paragraph))
        ?? throw new ArgumentException("The paragraph does not belong to this table.", nameof(paragraph));
}
