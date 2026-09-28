using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    public TableCellRegion CurrentCell => new TableGrid(CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.")).RegionOf(CurrentParagraph);

    /// <summary>Merges a logical rectangle without dropping paragraphs, pictures, nested tables or their anchors.</summary>
    public void MergeTableCells(int row, int column, int rowCount, int columnCount)
    {
        EnsureWritable();
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var grid = new TableGrid(table);
        if (row < 0 || column < 0 || rowCount < 1 || columnCount < 1 || row > grid.RowCount - rowCount || column > grid.ColumnCount - columnCount)
            throw new ArgumentOutOfRangeException(nameof(rowCount), "The merge rectangle lies outside the table.");
        var bottom = row + rowCount; var right = column + columnCount;
        var affected = grid.Regions.Where(r => r.Row < bottom && r.RowEnd > row && r.Column < right && r.ColumnEnd > column).ToArray();
        if (affected.Any(r => r.Row < row || r.RowEnd > bottom || r.Column < column || r.ColumnEnd > right))
            throw new InvalidOperationException("The selection cuts through an existing merged cell. Select its entire rectangle or split it first.");
        if (affected.Length == 1) return;
        StructuralEdit("Merge cells", () =>
        {
            var anchor = grid.At(row, column).Cell;
            var content = affected.SelectMany(r => r.Cell.Blocks).ToList();
            anchor.Blocks = content; anchor.RowSpan = rowCount; anchor.ColumnSpan = columnCount;
            var regions = grid.Regions.Except(affected).Append(new(anchor, row, column, rowCount, columnCount));
            RebuildTable(table, grid.RowCount, grid.ColumnCount, regions);
        });
    }

    public void MergeSelectedTableCells()
    {
        var index = Index; var first = index.At(Selection.Start); var last = index.At(Selection.End);
        if (first.Table is null || !ReferenceEquals(first.Table, last.Table))
            throw new InvalidOperationException("Select cells in one table before merging.");
        var grid = new TableGrid(first.Table); var a = grid.RegionOf(first.Paragraph); var b = grid.RegionOf(last.Paragraph);
        var top = Math.Min(a.Row, b.Row); var left = Math.Min(a.Column, b.Column);
        MergeTableCells(top, left, Math.Max(a.RowEnd, b.RowEnd) - top, Math.Max(a.ColumnEnd, b.ColumnEnd) - left);
    }

    /// <summary>Restores the original logical slots; all content remains in the top-left cell.</summary>
    public void SplitTableCell()
    {
        EnsureWritable();
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var grid = new TableGrid(table); var region = grid.RegionOf(CurrentParagraph);
        if (region.RowSpan == 1 && region.ColumnSpan == 1) return;
        StructuralEdit("Split merged cell", () =>
        {
            region.Cell.RowSpan = region.Cell.ColumnSpan = 1;
            RebuildTable(table, grid.RowCount, grid.ColumnCount,
                grid.Regions.Where(r => !ReferenceEquals(r.Cell, region.Cell)).Append(new(region.Cell, region.Row, region.Column, 1, 1)));
        });
    }

    public void SetCellVerticalAlignment(CellVerticalAlignment alignment)
    {
        EnsureWritable(); if (!Enum.IsDefined(alignment)) throw new ArgumentOutOfRangeException(nameof(alignment));
        var cell = CurrentCell.Cell; Execute("Cell vertical alignment", () => cell.VerticalAlignment = alignment);
    }

    private static void RebuildTable(TableBlock table, int rows, int columns, IEnumerable<TableCellRegion> source,
        IReadOnlyList<TableRow>? rowProperties = null)
    {
        var regions = source.ToArray();
        var result = new List<TableRow>(rows);
        var occupied = new bool[rows, columns];
        for (var r = 0; r < rows; r++)
        {
            var basis = rowProperties is not null ? rowProperties[r] : r < table.Rows.Count ? table.Rows[r] : null;
            result.Add(new() { MinimumHeight = basis?.MinimumHeight ?? 0, AllowSplit = basis?.AllowSplit ?? true,
                Cells = Enumerable.Repeat<TableCell>(null!, columns).ToList() });
        }
        foreach (var region in regions)
        {
            for (var r = region.Row; r < region.RowEnd; r++)
            {
                for (var c = region.Column; c < region.ColumnEnd; c++)
                {
                    if ((uint)r >= (uint)rows || (uint)c >= (uint)columns || occupied[r, c])
                        throw new InvalidOperationException("The structural edit produced overlapping or invalid cell spans.");
                    occupied[r, c] = true;
                    result[r].Cells[c] = r == region.Row && c == region.Column ? region.Cell : new() { Blocks = [] };
                }
            }
            region.Cell.RowSpan = region.RowSpan; region.Cell.ColumnSpan = region.ColumnSpan;
        }
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
                result[r].Cells[c] ??= new TableCell();
        table.Rows = result;
    }

    private void InsertGridRow(bool above)
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var grid = new TableGrid(table); var active = grid.RegionOf(CurrentParagraph);
        if (grid.RowCount >= 200) throw new InvalidOperationException("A table may contain at most 200 rows.");
        var at = above ? active.Row : active.RowEnd;
        StructuralEdit(above ? "Insert row above" : "Insert row below", () =>
        {
            var rows = table.Rows.ToList(); rows.Insert(at, new());
            var regions = grid.Regions.Select(r => r.Row >= at ? r with { Row = r.Row + 1 }
                : r.RowEnd > at ? r with { RowSpan = r.RowSpan + 1 } : r);
            RebuildTable(table, grid.RowCount + 1, grid.ColumnCount, regions, rows);
            var target = new TableGrid(table).At(at, active.Column).Cell;
            var paragraph = DocumentModel.Walk(target.Blocks).First(); var caret = Index.StartOf(paragraph); Selection = new(caret, caret);
        }, preserveSelection: false);
    }

    private void InsertGridColumn(bool before)
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var grid = new TableGrid(table); var active = grid.RegionOf(CurrentParagraph);
        if (grid.ColumnCount >= 20) throw new InvalidOperationException("A table may contain at most 20 columns.");
        var at = before ? active.Column : active.ColumnEnd;
        StructuralEdit(before ? "Insert column left" : "Insert column right", () =>
        {
            var regions = grid.Regions.Select(r => r.Column >= at ? r with { Column = r.Column + 1 }
                : r.ColumnEnd > at ? r with { ColumnSpan = r.ColumnSpan + 1 } : r);
            RebuildTable(table, grid.RowCount, grid.ColumnCount + 1, regions);
            var widths = table.ColumnWidths.Count == grid.ColumnCount && table.ColumnWidths.All(w => double.IsFinite(w) && w > 0)
                ? table.ColumnWidths : Enumerable.Repeat(1d, grid.ColumnCount).ToList();
            widths.Insert(at, widths[active.Column]); table.ColumnWidths = widths;
        });
    }

    private void DeleteGridRow()
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var grid = new TableGrid(table); var active = grid.RegionOf(CurrentParagraph);
        if (grid.RowCount == 1) { DeleteTable(); return; }
        var at = active.Row;
        StructuralEdit("Delete row", () =>
        {
            var rows = table.Rows.ToList(); rows.RemoveAt(at);
            var regions = grid.Regions.Where(r => !(r.Row == at && r.RowSpan == 1))
                .Select(r => r.Row > at ? r with { Row = r.Row - 1 } : r.RowEnd > at ? r with { RowSpan = r.RowSpan - 1 } : r);
            RebuildTable(table, grid.RowCount - 1, grid.ColumnCount, regions, rows);
        });
    }

    private void DeleteGridColumn()
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var grid = new TableGrid(table); var active = grid.RegionOf(CurrentParagraph);
        if (grid.ColumnCount == 1) { DeleteTable(); return; }
        var at = active.Column;
        StructuralEdit("Delete column", () =>
        {
            var regions = grid.Regions.Where(r => !(r.Column == at && r.ColumnSpan == 1))
                .Select(r => r.Column > at ? r with { Column = r.Column - 1 } : r.ColumnEnd > at ? r with { ColumnSpan = r.ColumnSpan - 1 } : r);
            RebuildTable(table, grid.RowCount, grid.ColumnCount - 1, regions);
            if (at < table.ColumnWidths.Count) table.ColumnWidths.RemoveAt(at);
        });
    }
}
