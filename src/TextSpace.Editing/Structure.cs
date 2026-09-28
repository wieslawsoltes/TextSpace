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
        var ratio = Math.Min(1, CurrentSection.Page.ColumnWidth / width);
        InsertBlock(new ImageBlock { Data = data, ContentType = contentType, Width = width * ratio, Height = height * ratio, AltText = altText }, "Insert picture");
    }
    public TableBlock? CurrentTable => Index.At(Selection.Active).Table;
    public void AddTableRow() => InsertGridRow(false);
    public void AddTableRowAbove() => InsertGridRow(true);
    public void AddTableColumn() => InsertGridColumn(false);
    public void AddTableColumnBefore() => InsertGridColumn(true);
    public void DeleteTableRow() => DeleteGridRow();
    public void DeleteTableColumn() => DeleteGridColumn();
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
        var cells = new TableGrid(table).Regions.Select(r => r.Cell).ToArray();
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
