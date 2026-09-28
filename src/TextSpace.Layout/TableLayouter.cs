using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Measures table trees at their real cell widths instead of flattening nested content.</summary>
internal sealed class TableLayouter(ParagraphLayoutCache paragraphs, TextIndex index, double defaultTabStop, double maximumImageHeight)
{
    private sealed class Content
    {
        public double Height;
        public List<LayoutLine> Lines = [];
        public List<LayoutCell> Cells = [];
        public List<LayoutImage> Images = [];
    }
    public TableFlowLayout Measure(TableBlock table, double width, int depth = 0)
    {
        if (depth > 8) throw new InvalidDataException("Tables are nested too deeply.");
        var grid = new TableGrid(table);
        var weights = table.ColumnWidths.Count == grid.ColumnCount && table.ColumnWidths.All(w => double.IsFinite(w) && w > 0)
            ? table.ColumnWidths.ToArray() : Enumerable.Repeat(1d, grid.ColumnCount).ToArray();
        var maximum = weights.Max(); var normalized = weights.Select(w => Math.Max(0.000001, w / maximum)).ToArray(); var total = normalized.Sum();
        var x = new double[grid.ColumnCount + 1];
        for (var c = 0; c < grid.ColumnCount; c++) x[c + 1] = x[c] + width * normalized[c] / total;
        var heights = table.Rows.Select(r => Math.Max(24, r.MinimumHeight)).ToArray();
        var measurements = new List<(TableCellRegion Region, Content Content, double Padding)>();
        foreach (var region in grid.Regions)
        {
            var cellWidth = x[region.ColumnEnd] - x[region.Column]; var padding = Math.Min(table.CellPadding, cellWidth / 4);
            var content = MeasureContent(region.Cell.Blocks, Math.Max(12, cellWidth - 2 * padding), depth);
            measurements.Add((region, content, padding));
        }
        // Span constraints only increase row heights, so later constraints cannot undo earlier ones.
        foreach (var item in measurements.OrderBy(m => m.Region.RowSpan))
        {
            var height = 0d; for (var row = item.Region.Row; row < item.Region.RowEnd; row++) height += heights[row];
            var deficit = item.Content.Height + 2 * item.Padding - height;
            if (deficit > 0) heights[item.Region.RowEnd - 1] += deficit;
        }
        var y = new double[grid.RowCount + 1];
        for (var row = 0; row < grid.RowCount; row++) y[row + 1] = y[row] + heights[row];
        var forbidden = new bool[grid.RowCount + 1];
        foreach (var region in grid.Regions) for (var row = region.Row + 1; row < region.RowEnd; row++) forbidden[row] = true;
        var groups = new List<(double Start, double End, bool AllowSplit)>(); var first = 0;
        for (var end = 1; end <= grid.RowCount; end++)
        {
            if (forbidden[end]) continue;
            var split = true; for (var row = first; row < end; row++) split &= table.Rows[row].AllowSplit;
            groups.Add((y[first], y[end], split)); first = end;
        }
        var result = new TableFlowLayout { Height = y[^1], Groups = groups,
            HeaderHeight = table.HeaderRow && table.RepeatHeaderRow && !forbidden[1] ? y[1] : 0 };
        foreach (var item in measurements)
        {
            var region = item.Region; var cell = region.Cell; var padding = item.Padding;
            var cellHeight = y[region.RowEnd] - y[region.Row]; var remaining = Math.Max(0, cellHeight - 2 * padding - item.Content.Height);
            var offset = cell.VerticalAlignment == CellVerticalAlignment.Center ? remaining / 2 : cell.VerticalAlignment == CellVerticalAlignment.Bottom ? remaining : 0;
            var dx = x[region.Column] + padding; var dy = y[region.Row] + padding + offset;
            var fill = cell.Shading ?? (table.HeaderRow && region.Row == 0 ? "#D9E5F5" : table.BandedRows && region.Row % 2 == 0 ? "#F3F6FA" : null);
            result.Cells.Add(new(table.Id, new(x[region.Column], y[region.Row], x[region.ColumnEnd] - x[region.Column], cellHeight), fill, table.HeaderRow && region.Row == 0));
            foreach (var nested in item.Content.Cells) result.Cells.Add(nested with { Bounds = new(nested.Bounds.X + dx, nested.Bounds.Y + dy, nested.Bounds.Width, nested.Bounds.Height) });
            foreach (var line in item.Content.Lines) result.Lines.Add(LayoutGeometry.Copy(line, dx, dy, 0));
            foreach (var image in item.Content.Images) result.Images.Add(image with { Bounds = new(image.Bounds.X + dx, image.Bounds.Y + dy, image.Bounds.Width, image.Bounds.Height) });
        }
        result.BuildIndexes(); return result;
    }
    private Content MeasureContent(IEnumerable<Block> blocks, double width, int depth)
    {
        var result = new Content(); var number = 0;
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    result.Height += Math.Max(0, paragraph.Format.SpaceBefore);
                    if (paragraph.Format.List == ListKind.Number) number++; else if (paragraph.Format.List == ListKind.None) number = 0;
                    var lines = paragraphs.Layout(paragraph, width, index.StartOf(paragraph), defaultTabStop);
                    for (var i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i]; line.Y = result.Height;
                        if (i == 0 && paragraph.Format.List != ListKind.None) line.Marker = paragraph.Format.List == ListKind.Bullet ? "•" : number + ".";
                        result.Lines.Add(line); result.Height += line.Height;
                    }
                    result.Height += Math.Max(0, paragraph.Format.SpaceAfter); break;
                case TableBlock nested:
                    var layout = Measure(nested, width, depth + 1);
                    foreach (var line in layout.Lines) result.Lines.Add(LayoutGeometry.Copy(line, 0, result.Height, 0));
                    foreach (var cell in layout.Cells) result.Cells.Add(cell with { Bounds = new(cell.Bounds.X, cell.Bounds.Y + result.Height, cell.Bounds.Width, cell.Bounds.Height) });
                    foreach (var image in layout.Images) result.Images.Add(image with { Bounds = new(image.Bounds.X, image.Bounds.Y + result.Height, image.Bounds.Width, image.Bounds.Height) });
                    result.Height += layout.Height + 8; break;
                case ImageBlock image:
                    var ratio = Math.Min(1, Math.Min(width / image.Width, maximumImageHeight / image.Height));
                    var w = image.Width * ratio; var h = image.Height * ratio;
                    var left = image.Alignment == TextAlignment.Center ? (width - w) / 2 : image.Alignment == TextAlignment.Right ? width - w : 0;
                    result.Images.Add(new(image, new(left, result.Height, w, h))); result.Height += h + 8; break;
            }
        }
        return result;
    }
}
