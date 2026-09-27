using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    public void InsertBlock(Block block, string label)
    {
        if (!Selection.IsEmpty) InsertText("");
        var index = Index; var address = index.At(Selection.Active); var offset = Selection.Active - address.Start;
        Execute(label, () =>
        {
            var p = address.Paragraph; var suffix = p.Slice(offset, p.Length - offset); p.Runs = p.Slice(0, offset);
            var after = new Paragraph { Runs = suffix, Format = p.Format, DefaultStyle = p.DefaultStyle };
            var position = address.Container.IndexOf(p); address.Container.InsertRange(position + 1, [block, after]);
            var newIndex = Index; var target = block is TableBlock table ? DocumentModel.Walk(table.Rows[0].Cells[0].Blocks).First() : after;
            var caret = newIndex.StartOf(target); Selection = new(caret, caret);
            var added = newIndex.Length - index.Length; TransformAnchors(address.Start + offset, 0, added);
        });
    }
    public void InsertTable(int rows, int columns) => InsertBlock(TableBlock.Create(rows, columns), "Insert table");
    public void InsertPageBreak() => InsertBlock(new PageBreakBlock(), "Page break");
    public void InsertPicture(byte[] data, string contentType, double width, double height, string altText)
    {
        if (data.Length > 16 * 1024 * 1024) throw new InvalidOperationException("Pictures must be smaller than 16 MB.");
        var ratio = Math.Min(1, Document.Page.ColumnWidth / Math.Max(1, width));
        InsertBlock(new ImageBlock { Data = data, ContentType = contentType, Width = width * ratio, Height = height * ratio, AltText = altText }, "Insert picture");
    }
    public TableBlock? CurrentTable => Index.At(Selection.Active).Table;
    public void AddTableRow()
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        Execute("Insert row", () =>
        {
            var address = Index.At(Selection.Active); var row = table.Rows.FindIndex(r => r.Cells.Any(c => c.Blocks.Contains(address.Paragraph)));
            var item = new TableRow(); for (var i = 0; i < table.Rows[0].Cells.Count; i++) item.Cells.Add(new());
            table.Rows.Insert(Math.Max(0, row) + 1, item); var start = Index.StartOf((Paragraph)item.Cells[0].Blocks[0]); Selection = new(start, start);
        });
    }
    public void AddTableColumn()
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        Execute("Insert column", () => { foreach (var row in table.Rows) row.Cells.Add(new()); table.ColumnWidths = Enumerable.Repeat(1d / table.Rows[0].Cells.Count, table.Rows[0].Cells.Count).ToList(); });
    }
    public void DeleteTableRow()
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        if (table.Rows.Count == 1) { DeleteTable(); return; }
        Execute("Delete row", () =>
        {
            var p = CurrentParagraph; var row = table.Rows.FindIndex(r => r.Cells.Any(c => c.Blocks.Contains(p)));
            if (row < 0) return; table.Rows.RemoveAt(row); var next = DocumentModel.Walk(table.Rows[Math.Min(row, table.Rows.Count - 1)].Cells[0].Blocks).First();
            var caret = Index.StartOf(next); Selection = new(caret, caret);
        });
    }
    public void DeleteTable()
    {
        var table = CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        Execute("Delete table", () =>
        {
            bool Remove(List<Block> blocks)
            {
                var at = blocks.IndexOf(table); if (at >= 0) { blocks.RemoveAt(at); if (!blocks.OfType<Paragraph>().Any()) blocks.Add(new Paragraph()); return true; }
                return blocks.OfType<TableBlock>().Any(t => t.Rows.SelectMany(r => r.Cells).Any(c => Remove(c.Blocks)));
            }
            Remove(Document.Blocks); var caret = Math.Min(Selection.Active, Index.Length); Selection = new(caret, caret);
        });
    }
    public bool MoveTableCell(bool backwards)
    {
        var index = Index; var address = index.At(Selection.Active); var table = address.Table; if (table is null) return false;
        var cells = table.Rows.SelectMany(r => r.Cells).ToArray();
        var cell = Array.FindIndex(cells, c => DocumentModel.Walk(c.Blocks).Contains(address.Paragraph));
        var next = cell + (backwards ? -1 : 1);
        if (next >= cells.Length) { AddTableRow(); return true; }
        if (next < 0) return true;
        var paragraph = DocumentModel.Walk(cells[next].Blocks).First(); var start = Index.StartOf(paragraph); SetSelection(start, start); return true;
    }
    public void AddComment(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Execute("New comment", () => Document.Comments.Add(new() { Start = Selection.Start, End = Selection.End, Text = text.Trim(), Author = Document.Author }));
    }
    public void ResolveComment(string id) => Execute("Resolve comment", () => { var comment = Document.Comments.First(c => c.Id == id); comment.Resolved = !comment.Resolved; });
    public void DeleteComment(string id) => Execute("Delete comment", () => Document.Comments.RemoveAll(c => c.Id == id));
    public void ReplyToComment(string id, string text) => Execute("Reply", () => Document.Comments.First(c => c.Id == id).Replies.Add(text));
    public void AcceptChange(string id) => Execute("Accept change", () => Document.Changes.RemoveAll(c => c.Id == id));
    public void AcceptAllChanges() => Execute("Accept all changes", () => Document.Changes.Clear());
    public void RejectChange(string id)
    {
        var change = Document.Changes.First(c => c.Id == id);
        if (!change.CanReject || change.Start < 0 || change.Start + change.Inserted.Length > Index.Length || Index.Text.Substring(change.Start, change.Inserted.Length) != change.Inserted) throw new InvalidOperationException("Later edits overlap this change. Use Undo or accept this change instead.");
        Execute("Reject change", () =>
        {
            Document.Changes.Remove(change); var tracking = TrackChanges; TrackChanges = false;
            try { Replace(change.Start, change.Inserted.Length, change.Removed, "Reject change"); } finally { TrackChanges = tracking; }
        });
    }
}
