using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    public void InsertBlock(Block block, string label)
    {
        ArgumentNullException.ThrowIfNull(block);
        Execute(label, () =>
        {
            // Removing the selection and inserting the structure form one undo unit.
            if (!Selection.IsEmpty) InsertText("");
            var index = Index; var address = index.At(Selection.Active); var offset = Selection.Active - address.Start;
            var paragraph = address.Paragraph;
            var suffix = paragraph.Slice(offset, paragraph.Length - offset);
            paragraph.Runs = paragraph.Slice(0, offset);
            var after = new Paragraph { Runs = suffix, Format = paragraph.Format, DefaultStyle = paragraph.DefaultStyle };
            address.Container.InsertRange(address.Container.IndexOf(paragraph) + 1, [block, after]);
            var next = Index;
            var target = block is TableBlock table ? DocumentModel.Walk(table.Rows[0].Cells[0].Blocks).First() : after;
            var caret = next.StartOf(target); Selection = new(caret, caret);
            TransformAnchors(address.Start + offset, 0, next.Length - index.Length);
        });
    }
    public void InsertTable(int rows, int columns) => InsertBlock(TableBlock.Create(rows, columns), "Insert table");
    public void InsertPageBreak() => InsertBlock(new PageBreakBlock(), "Page break");
    public void InsertPicture(byte[] data, string contentType, double width, double height, string altText)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > 16 * 1024 * 1024) throw new InvalidOperationException("Pictures must be smaller than 16 MB.");
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var ratio = Math.Min(1, Document.Page.ColumnWidth / width);
        InsertBlock(new ImageBlock { Data = data, ContentType = contentType, Width = width * ratio, Height = height * ratio, AltText = altText }, "Insert picture");
    }
    public TableBlock? CurrentTable => Index.At(Selection.Active).Table;
    private (TableBlock Table, int Row, int Column) TableLocation()
    {
        var paragraph = CurrentParagraph;
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        for (var row = 0; row < table.Rows.Count; row++)
            for (var column = 0; column < table.Rows[row].Cells.Count; column++)
                if (DocumentModel.Walk(table.Rows[row].Cells[column].Blocks).Contains(paragraph)) return (table, row, column);
        throw new InvalidOperationException("The current table cell could not be found.");
    }
    public void AddTableRow() => InsertTableRow(false);
    public void AddTableRowAbove() => InsertTableRow(true);
    private void InsertTableRow(bool above)
    {
        var (table, row, _) = TableLocation();
        if (table.Rows.Count >= 200) throw new InvalidOperationException("A table may contain at most 200 rows.");
        StructuralEdit(above ? "Insert row above" : "Insert row below", () =>
        {
            var item = new TableRow();
            for (var i = 0; i < table.Rows.Max(r => r.Cells.Count); i++) item.Cells.Add(new());
            table.Rows.Insert(row + (above ? 0 : 1), item);
            var start = Index.StartOf((Paragraph)item.Cells[0].Blocks[0]); Selection = new(start, start);
        }, preserveSelection: false);
    }
    public void AddTableColumn() => InsertTableColumn(false);
    public void AddTableColumnBefore() => InsertTableColumn(true);
    private void InsertTableColumn(bool before)
    {
        var (table, _, column) = TableLocation();
        var count = table.Rows.Max(r => r.Cells.Count);
        if (count >= 20) throw new InvalidOperationException("A table may contain at most 20 columns.");
        StructuralEdit(before ? "Insert column left" : "Insert column right", () =>
        {
            var insertion = column + (before ? 0 : 1);
            foreach (var row in table.Rows)
            {
                while (row.Cells.Count < count) row.Cells.Add(new());
                row.Cells.Insert(insertion, new());
            }
            if (table.ColumnWidths.Count != count || table.ColumnWidths.Any(w => !double.IsFinite(w) || w <= 0)) table.ColumnWidths = Enumerable.Repeat(1d, count).ToList();
            table.ColumnWidths.Insert(insertion, table.ColumnWidths[Math.Min(column, count - 1)]);
        });
    }
    public void DeleteTableRow()
    {
        var (table, row, _) = TableLocation();
        if (table.Rows.Count == 1) { DeleteTable(); return; }
        StructuralEdit("Delete row", () =>
        {
            table.Rows.RemoveAt(row);
            var paragraph = DocumentModel.Walk(table.Rows[Math.Min(row, table.Rows.Count - 1)].Cells[0].Blocks).First();
            var caret = Index.StartOf(paragraph); Selection = new(caret, caret);
        }, preserveSelection: false);
    }
    public void DeleteTableColumn()
    {
        var (table, rowIndex, column) = TableLocation();
        if (table.Rows.Max(r => r.Cells.Count) == 1) { DeleteTable(); return; }
        StructuralEdit("Delete column", () =>
        {
            foreach (var row in table.Rows)
            {
                if (column < row.Cells.Count) row.Cells.RemoveAt(column);
                if (row.Cells.Count == 0) row.Cells.Add(new());
            }
            if (column < table.ColumnWidths.Count) table.ColumnWidths.RemoveAt(column);
            var cells = table.Rows[rowIndex].Cells;
            var paragraph = DocumentModel.Walk(cells[Math.Min(column, cells.Count - 1)].Blocks).First();
            var caret = Index.StartOf(paragraph); Selection = new(caret, caret);
        }, preserveSelection: false);
    }
    public void DeleteTable()
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        StructuralEdit("Delete table", () =>
        {
            bool Remove(List<Block> blocks)
            {
                var at = blocks.IndexOf(table);
                if (at >= 0)
                {
                    blocks.RemoveAt(at);
                    if (!DocumentModel.Walk(blocks).Any()) blocks.Add(new Paragraph());
                    return true;
                }
                return blocks.OfType<TableBlock>().Any(t => t.Rows.SelectMany(r => r.Cells).Any(c => Remove(c.Blocks)));
            }
            if (!Remove(Document.Blocks)) throw new InvalidOperationException("The table is no longer in this document.");
        });
    }
    public bool MoveTableCell(bool backwards)
    {
        var index = Index; var address = index.At(Selection.Active); var table = address.Table;
        if (table is null) return false;
        var cells = table.Rows.SelectMany(r => r.Cells).ToArray();
        var cell = Array.FindIndex(cells, c => DocumentModel.Walk(c.Blocks).Contains(address.Paragraph));
        var next = cell + (backwards ? -1 : 1);
        if (next >= cells.Length) { if (!IsReadOnly) AddTableRow(); return true; }
        if (next < 0) return true;
        var paragraph = DocumentModel.Walk(cells[next].Blocks).First(); var start = Index.StartOf(paragraph); SetSelection(start, start); return true;
    }
    public void AddComment(string text)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(text)) return;
        Execute("New comment", () => Document.Comments.Add(new() { Start = Selection.Start, End = Selection.End, Text = text.Trim(), Author = Document.Author }));
    }
    public void ResolveComment(string id) => Execute("Resolve comment", () => { var comment = Document.Comments.First(c => c.Id == id); comment.Resolved = !comment.Resolved; });
    public void DeleteComment(string id) => Execute("Delete comment", () => Document.Comments.RemoveAll(c => c.Id == id));
    public void ReplyToComment(string id, string text)
    {
        EnsureWritable();
        if (!string.IsNullOrWhiteSpace(text)) Execute("Reply", () => Document.Comments.First(c => c.Id == id).Replies.Add(text.Trim()));
    }
    public void AcceptChange(string id) => Execute("Accept change", () => Document.Changes.RemoveAll(c => c.Id == id));
    public void AcceptAllChanges() => Execute("Accept all changes", () => Document.Changes.Clear());
    public void RejectChange(string id)
    {
        var change = Document.Changes.First(c => c.Id == id); var index = Index;
        if (!change.CanReject || change.Start < 0 || change.Start > index.Length || change.Inserted.Length > index.Length - change.Start || index.Text.Substring(change.Start, change.Inserted.Length) != change.Inserted)
            throw new InvalidOperationException("Later edits overlap this change. Use Undo or accept this change instead.");
        Execute("Reject change", () =>
        {
            Document.Changes.Remove(change); var tracking = TrackChanges; TrackChanges = false;
            try { Replace(change.Start, change.Inserted.Length, change.Removed, "Reject change"); }
            finally { TrackChanges = tracking; }
        });
    }
}
