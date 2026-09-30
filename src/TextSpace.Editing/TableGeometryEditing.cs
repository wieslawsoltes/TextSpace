using TextSpace.Core;

namespace TextSpace.Editing;

public readonly record struct TableSelection(string TableId, int Row, int Column, int RowCount, int ColumnCount)
{
    public int RowEnd => Row + RowCount;
    public int ColumnEnd => Column + ColumnCount;
    public bool Intersects(int row, int column, int rows, int columns) => row < RowEnd && row + rows > Row && column < ColumnEnd && column + columns > Column;
}

public sealed partial class EditorSession
{
    public TableBlock? FindTable(string id) => BlockTree.Find(Document.Blocks, id)?.Block as TableBlock;

    /// <summary>Returns real point widths, normalizing legacy or invalid weight vectors.</summary>
    public static double[] TableColumnWidths(TableBlock table, double availableWidth)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!double.IsFinite(availableWidth) || availableWidth <= 0) throw new ArgumentOutOfRangeException(nameof(availableWidth));
        var count = new TableGrid(table).ColumnCount;
        var weights = table.ColumnWidths is { } source && source.Count == count && source.All(w => double.IsFinite(w) && w > 0)
            ? source.ToArray() : Enumerable.Repeat(1d, count).ToArray();
        var max = weights.Max(); for (var i = 0; i < weights.Length; i++) weights[i] = Math.Max(0.000001, weights[i] / max);
        var sum = weights.Sum(); for (var i = 0; i < weights.Length; i++) weights[i] = availableWidth * weights[i] / sum;
        return weights;
    }

    public static double[] ResizeTableBoundary(IReadOnlyList<double> widths, int boundary, double delta, double minimum = 12)
    {
        ArgumentNullException.ThrowIfNull(widths);
        if (boundary < 1 || boundary >= widths.Count || !double.IsFinite(delta) || !double.IsFinite(minimum) || minimum <= 0
            || widths.Any(w => !double.IsFinite(w) || w <= 0)) throw new ArgumentOutOfRangeException(nameof(boundary));
        var result = widths.ToArray(); var total = result[boundary - 1] + result[boundary];
        var lower = Math.Min(minimum, total / 2);
        result[boundary - 1] = Math.Clamp(result[boundary - 1] + delta, lower, total - lower);
        result[boundary] = total - result[boundary - 1]; return result;
    }
    public void SetTableColumnWidths(string id, IReadOnlyList<double> widths)
    {
        EnsureWritable(); ArgumentNullException.ThrowIfNull(widths);
        var table = FindTable(id) ?? throw new InvalidOperationException("The table no longer exists.");
        var count = new TableGrid(table).ColumnCount;
        if (widths.Count != count || widths.Any(w => !double.IsFinite(w) || w <= 0 || w > 4000)) throw new ArgumentOutOfRangeException(nameof(widths));
        var owned = widths.ToList(); Execute("Resize table columns", () => table.ColumnWidths = owned);
    }
    public void SetTableRowHeight(string id, int row, double height)
    {
        EnsureWritable(); var table = FindTable(id) ?? throw new InvalidOperationException("The table no longer exists.");
        if ((uint)row >= (uint)table.Rows.Count || !double.IsFinite(height) || height is < 0 or > 4000) throw new ArgumentOutOfRangeException(nameof(height));
        Execute("Resize table row", () => table.Rows[row].MinimumHeight = height);
    }
    public TableSelection SelectTableRectangle(string id, int row, int column, int endRow, int endColumn)
    {
        var table = FindTable(id) ?? throw new InvalidOperationException("The table no longer exists."); var grid = new TableGrid(table);
        if (row < 0 || endRow < 0 || column < 0 || endColumn < 0 || row >= grid.RowCount || endRow >= grid.RowCount || column >= grid.ColumnCount || endColumn >= grid.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(row));
        var top = Math.Min(row, endRow); var left = Math.Min(column, endColumn); var bottom = Math.Max(row, endRow) + 1; var right = Math.Max(column, endColumn) + 1;
        // Expand to include complete existing spans, reaching a fixed point.
        bool changed;
        do
        {
            changed = false;
            foreach (var region in grid.Regions)
            {
                if (region.Row >= bottom || region.RowEnd <= top || region.Column >= right || region.ColumnEnd <= left) continue;
                var t = Math.Min(top, region.Row); var l = Math.Min(left, region.Column); var b = Math.Max(bottom, region.RowEnd); var r = Math.Max(right, region.ColumnEnd);
                changed |= t != top || l != left || b != bottom || r != right; top = t; left = l; bottom = b; right = r;
            }
        } while (changed);
        var first = DocumentModel.Walk(grid.At(top, left).Cell.Blocks).First(); var index = Index; SetSelection(index.StartOf(first), index.StartOf(first));
        return new(id, top, left, bottom - top, right - left);
    }
    public void MergeTableSelection(TableSelection selection)
    {
        EnsureWritable(); var table = FindTable(selection.TableId) ?? throw new InvalidOperationException("The table no longer exists.");
        var grid = new TableGrid(table); var paragraph = DocumentModel.Walk(grid.At(selection.Row, selection.Column).Cell.Blocks).First();
        var at = Index.StartOf(paragraph); SetSelection(at, at);
        MergeTableCells(selection.Row, selection.Column, selection.RowCount, selection.ColumnCount);
    }
    public void FormatTableSelection(TableSelection selection, string label, Action<TableCell> format)
    {
        ArgumentNullException.ThrowIfNull(format); EnsureWritable();
        var table = FindTable(selection.TableId) ?? throw new InvalidOperationException("The table no longer exists."); var grid = new TableGrid(table);
        if (selection.Row < 0 || selection.Column < 0 || selection.RowCount < 1 || selection.ColumnCount < 1 || selection.RowEnd > grid.RowCount || selection.ColumnEnd > grid.ColumnCount)
            throw new InvalidOperationException("The cell selection is no longer valid.");
        Execute(label, () => { foreach (var region in grid.Regions.Where(r => selection.Intersects(r.Row, r.Column, r.RowSpan, r.ColumnSpan))) format(region.Cell); });
    }
    public void DistributeTableColumns(string id)
    {
        EnsureWritable(); var table = FindTable(id) ?? throw new InvalidOperationException("The table no longer exists.");
        Execute("Distribute columns", () => table.ColumnWidths = Enumerable.Repeat(1d, new TableGrid(table).ColumnCount).ToList());
    }
    public void DistributeTableRows(string id)
    {
        EnsureWritable(); var table = FindTable(id) ?? throw new InvalidOperationException("The table no longer exists.");
        var height = Math.Max(24, table.Rows.Average(r => r.MinimumHeight));
        Execute("Distribute row minimums", () => { foreach (var row in table.Rows) row.MinimumHeight = height; });
    }
}
