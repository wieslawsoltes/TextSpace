using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxWriter
{
    private XElement Table(TableBlock table)
    {
        var grid = new TableGrid(table); var count = grid.ColumnCount;
        var weights = table.ColumnWidths.Count == count && table.ColumnWidths.All(w => w > 0 && double.IsFinite(w))
            ? table.ColumnWidths.ToArray() : Enumerable.Repeat(1d, count).ToArray();
        // Normalize by maximum first to keep finite large user weights from overflowing the sum.
        var maximum = weights.Max(); var total = weights.Sum(w => w / maximum);
        var widths = weights.Select(w => Math.Max(1, Twips(_availableTableWidth * (w / maximum) / total))).ToArray();
        var borders = E("tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }
            .Select(side => E(side, V("single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "A8B7C8"))));
        var margins = E("tblCellMar", new[] { "top", "left", "bottom", "right" }
            .Select(side => E(side, new XAttribute(W + "w", Twips(table.CellPadding)), new XAttribute(W + "type", "dxa"))));
        var result = E("tbl", E("tblPr", E("tblW", new XAttribute(W + "w", widths.Sum()), new XAttribute(W + "type", "dxa")),
            borders, E("tblLayout", new XAttribute(W + "type", "fixed")), margins),
            E("tblGrid", widths.Select(w => E("gridCol", new XAttribute(W + "w", w)))));
        for (var r = 0; r < grid.RowCount; r++)
        {
            var model = table.Rows[r];
            var row = E("tr", E("trPr", !model.AllowSplit ? E("cantSplit") : null,
                model.MinimumHeight > 0 ? E("trHeight", V(Twips(model.MinimumHeight)), new XAttribute(W + "hRule", "atLeast")) : null,
                table.HeaderRow && table.RepeatHeaderRow && r == 0 ? E("tblHeader") : null));
            for (var c = 0; c < count;)
            {
                var region = grid.At(r, c); var cell = region.Cell;
                var width = widths.Skip(c).Take(region.ColumnSpan).Sum();
                var fill = cell.Shading ?? (table.HeaderRow && region.Row == 0 ? "#D9E5F5"
                    : table.BandedRows && region.Row % 2 == 0 ? "#F3F6FA" : null);
                var properties = E("tcPr", E("tcW", new XAttribute(W + "w", width), new XAttribute(W + "type", "dxa")),
                    region.ColumnSpan > 1 ? E("gridSpan", V(region.ColumnSpan)) : null,
                    region.RowSpan > 1 ? E("vMerge", V(r == region.Row ? "restart" : "continue")) : null,
                    fill is null ? null : E("shd", V("clear"), new XAttribute(W + "fill", Hex(fill))),
                    E("vAlign", V(cell.VerticalAlignment.ToString().ToLowerInvariant())));
                var element = E("tc", properties);
                if (r == region.Row)
                {
                    var parentWidth = _availableTableWidth;
                    _availableTableWidth = Math.Max(12, width / 20d - 2 * Math.Min(table.CellPadding, width / 80d));
                    try { element.Add(Blocks(cell.Blocks)); }
                    finally { _availableTableWidth = parentWidth; }
                    if (cell.Blocks.LastOrDefault() is not Paragraph) element.Add(E("p"));
                }
                else element.Add(E("p"));
                row.Add(element); c += region.ColumnSpan;
            }
            result.Add(row);
        }
        return result;
    }
}
