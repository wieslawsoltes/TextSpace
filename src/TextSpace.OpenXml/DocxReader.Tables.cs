using System.Globalization;
using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxReader
{
    private int _tableReadDepth;
    private TableBlock ReadTable(XElement element)
    {
        if (_tableReadDepth >= 8) throw new InvalidDataException("Tables are nested too deeply.");
        _tableReadDepth++;
        try { return ReadTableCore(element); }
        finally { _tableReadDepth--; }
    }
    private TableBlock ReadTableCore(XElement element)
    {
        static int Count(string? value, int fallback, int maximum)
        {
            if (value is null) return fallback;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 0 || count > maximum)
                throw new InvalidDataException("A table grid count exceeds the import limits.");
            return count;
        }
        static int Span(XElement cell)
        {
            var span = Count(Val(cell.Element(W + "tcPr")?.Element(W + "gridSpan")), 1, 20);
            return span > 0 ? span : throw new InvalidDataException("A table cell span must be positive.");
        }
        var rows = element.Elements(W + "tr").ToArray();
        if (rows.Length == 0) return TableBlock.Create(1, 1);
        if (rows.Length > 200) throw new InvalidDataException("A table exceeds 200 rows.");
        var gridWidths = element.Element(W + "tblGrid")?.Elements(W + "gridCol")
            .Select(c => Number((string?)c.Attribute(W + "w"), 1)).ToList() ?? [];
        var columns = gridWidths.Count;
        foreach (var row in rows)
        {
            var before = Count(Val(row.Element(W + "trPr")?.Element(W + "gridBefore")), 0, 20);
            var after = Count(Val(row.Element(W + "trPr")?.Element(W + "gridAfter")), 0, 20);
            columns = Math.Max(columns, before + row.Elements(W + "tc").Sum(Span) + after);
        }
        if (columns is < 1 or > 20) throw new InvalidDataException("A table exceeds the 20-column grid limit.");
        var table = new TableBlock { HeaderRow = false, RepeatHeaderRow = false, BandedRows = false,
            ColumnWidths = gridWidths.Count == columns && gridWidths.All(w => w > 0 && double.IsFinite(w))
                ? gridWidths : Enumerable.Repeat(1d, columns).ToList() };
        var cellMargins = element.Element(W + "tblPr")?.Element(W + "tblCellMar");
        if (cellMargins is not null)
        {
            var topMargin = cellMargins.Element(W + "top");
            table.CellPadding = Math.Clamp(Number((string?)topMargin?.Attribute(W + "w"), 120) / 20, 0, 72);
            if (cellMargins.Elements().Any(m => Number((string?)m.Attribute(W + "w"), 120) / 20 != table.CellPadding))
                Warn("Asymmetric table cell margins use the top margin as uniform padding.");
        }
        var active = new Dictionary<int, (TableCell Cell, int Span)>();
        foreach (var rowElement in rows)
        {
            var properties = rowElement.Element(W + "trPr"); var height = properties?.Element(W + "trHeight");
            var row = new TableRow { MinimumHeight = Math.Clamp(Number(Val(height)) / 20, 0, 4000),
                AllowSplit = !Flag(properties?.Element(W + "cantSplit"), false) };
            if ((string?)height?.Attribute(W + "hRule") == "exact") Warn("Exact table row heights are imported as minimum heights to preserve visible content.");
            if (table.Rows.Count == 0) table.HeaderRow = table.RepeatHeaderRow = Flag(properties?.Element(W + "tblHeader"), false);
            else if (Flag(properties?.Element(W + "tblHeader"), false)) Warn("Only the first table header row is repeated by TextSpace.");
            var before = Count(Val(properties?.Element(W + "gridBefore")), 0, 20);
            if (before != 0 || properties?.Element(W + "gridAfter") is not null)
                Warn("Skipped table grid slots are normalized to explicit empty cells.");
            void Empty() { row.Cells.Add(new()); _textPosition++; }
            while (row.Cells.Count < before) Empty();
            var next = new Dictionary<int, (TableCell Cell, int Span)>();
            foreach (var cellElement in rowElement.Elements(W + "tc"))
            {
                var column = row.Cells.Count; var span = Span(cellElement); var cellProperties = cellElement.Element(W + "tcPr");
                var merge = cellProperties?.Element(W + "vMerge");
                // A malformed continuation carrying real content must not silently discard it.
                var content = cellElement.Descendants().Any(n => n.Name == W + "tbl" || n.Name == W + "drawing"
                    || n.Name == W + "object" || n.Name == W + "bookmarkStart" || n.Name == W + "bookmarkEnd"
                    || n.Name == W + "commentRangeStart" || n.Name == W + "commentRangeEnd" || n.Name == W + "fldChar"
                    || n.Name == W + "instrText" || n.Name == W + "tab" || n.Name == W + "br"
                    || n.Name == W + "t" && n.Value.Length > 0);
                if (merge is not null && Val(merge) is not "restart" && active.TryGetValue(column, out var previous)
                    && previous.Span == span && !content)
                {
                    previous.Cell.RowSpan++;
                    next[column] = previous;
                    for (var i = 0; i < span; i++) row.Cells.Add(new() { Blocks = [] });
                    continue;
                }
                if (merge is not null && Val(merge) is not "restart")
                    Warn("An unmatched or nonempty vertical-merge continuation was retained as an independent cell.");
                if (cellProperties?.Element(W + "hMerge") is not null)
                    Warn("Legacy hMerge markers are normalized to independent cells; gridSpan merges are supported.");
                var blocks = ReadBlocks(cellElement).ToList();
                if (!DocumentModel.Walk(blocks).Any()) { blocks.Add(new Paragraph()); _textPosition++; }
                var fill = (string?)cellProperties?.Element(W + "shd")?.Attribute(W + "fill");
                var vertical = Val(cellProperties?.Element(W + "vAlign")) switch
                { "center" => CellVerticalAlignment.Center, "bottom" => CellVerticalAlignment.Bottom, _ => CellVerticalAlignment.Top };
                var cell = new TableCell { Blocks = blocks, ColumnSpan = span, VerticalAlignment = vertical,
                    Shading = fill is { Length: 6 } && fill.All(Uri.IsHexDigit) ? "#" + fill : null };
                row.Cells.Add(cell);
                for (var i = 1; i < span; i++) row.Cells.Add(new() { Blocks = [] });
                if (merge is not null && Val(merge) == "restart") next[column] = (cell, span);
            }
            while (row.Cells.Count < columns) Empty();
            table.Rows.Add(row); active = next;
        }
        _ = new TableGrid(table); return table;
    }
}
