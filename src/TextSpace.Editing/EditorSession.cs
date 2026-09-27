using TextSpace.Core;
using TextSpace.Documents;

namespace TextSpace.Editing;

public enum EditorChangeKind { Document, Selection, View, Saved }
public sealed class EditorChangedEventArgs(EditorChangeKind kind, string label) : EventArgs
{
    public EditorChangeKind Kind { get; } = kind;
    public string Label { get; } = label;
}

/// <summary>Single-writer editor session. Transactions are atomic and restore the previous document on failure.</summary>
public sealed partial class EditorSession
{
    private sealed record Snapshot(string Json, TextSelection Selection, TextStyle TypingStyle, string Label);
    private readonly List<Snapshot> _undo = [], _redo = [];
    private bool _transaction;
    private string _savedJson;
    public DocumentModel Document { get; private set; }
    public TextSelection Selection { get; private set; }
    public TextStyle TypingStyle { get; private set; } = new();
    public bool TrackChanges { get; set; }
    public bool IsReadOnly { get; set; }
    public long Revision { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsDirty => DocumentJson.Save(Document) != _savedJson;
    public string UndoLabel => _undo.Count > 0 ? _undo[^1].Label : "";
    public TextIndex Index => new(Document);
    public Paragraph CurrentParagraph => Index.At(Selection.Active).Paragraph;
    public event EventHandler<EditorChangedEventArgs>? Changed;

    public EditorSession(DocumentModel document)
    {
        DocumentJson.Validate(document); Document = document; _savedJson = DocumentJson.Save(document);
        TypingStyle = Index.At(0).Paragraph.StyleAt(0);
    }
    public void Notify(EditorChangeKind kind, string label = "") => Changed?.Invoke(this, new(kind, label));
    public void SetSelection(int anchor, int active)
    {
        var index = Index; Selection = new(index.Snap(anchor), index.Snap(active, active >= anchor));
        var p = index.At(Selection.Active); TypingStyle = p.Paragraph.StyleAt(Math.Max(0, Selection.Active - p.Start - (Selection.IsEmpty ? 1 : 0)));
        Notify(EditorChangeKind.Selection);
    }
    public void SelectAll() => SetSelection(0, Index.Length);
    public void MarkSaved() { _savedJson = DocumentJson.Save(Document); Notify(EditorChangeKind.Saved); }
    public void Load(DocumentModel document)
    {
        DocumentJson.Validate(document); Document = document; _undo.Clear(); _redo.Clear(); Selection = new(0, 0);
        TypingStyle = Index.At(0).Paragraph.StyleAt(0); _savedJson = DocumentJson.Save(document); Revision++; Notify(EditorChangeKind.Document, "Open document");
    }
    public void Execute(string label, Action action)
    {
        if (IsReadOnly) throw new InvalidOperationException("This document is in reading mode.");
        if (_transaction) { action(); return; }
        var before = new Snapshot(DocumentJson.Save(Document), Selection, TypingStyle, label);
        _transaction = true;
        try
        {
            action(); DocumentJson.Validate(Document);
            var after = DocumentJson.Save(Document);
            if (after == before.Json) return;
            _undo.Add(before); if (_undo.Count > 100) _undo.RemoveAt(0); _redo.Clear();
            Document.Modified = DateTimeOffset.UtcNow; Revision++;
        }
        catch { Document = DocumentJson.Load(before.Json); Selection = before.Selection; TypingStyle = before.TypingStyle; throw; }
        finally { _transaction = false; Notify(EditorChangeKind.Document, label); }
    }
    public void Undo() => Restore(_undo, _redo, "Undo");
    public void Redo() => Restore(_redo, _undo, "Redo");
    private void Restore(List<Snapshot> source, List<Snapshot> target, string verb)
    {
        if (IsReadOnly || source.Count == 0) return;
        var snapshot = source[^1]; source.RemoveAt(source.Count - 1);
        target.Add(new(DocumentJson.Save(Document), Selection, TypingStyle, snapshot.Label));
        Document = DocumentJson.Load(snapshot.Json); Selection = snapshot.Selection; TypingStyle = snapshot.TypingStyle;
        Revision++; Notify(EditorChangeKind.Document, verb + " " + snapshot.Label);
    }
    public string SelectedText() => Index.Text.Substring(Selection.Start, Selection.Length);
    public void InsertText(string text) => Replace(Selection.Start, Selection.Length, text);
    public void Replace(int start, int length, string text, string label = "Typing")
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var index = Index; start = index.Snap(start); var end = index.Snap(Math.Clamp(start + length, start, index.Length), true);
        if (index.Length - (end - start) + text.Length > DocumentJson.MaxCharacters) throw new InvalidOperationException("The document text limit has been reached.");
        var first = index.At(start); var last = index.At(end);
        if (!ReferenceEquals(first.Container, last.Container)) throw new InvalidOperationException("This selection crosses table-cell boundaries. Edit one cell at a time, or select the entire table from outside it.");
        var removed = index.Text.Substring(start, end - start); var insertionStyle = TypingStyle;
        Execute(label, () =>
        {
            var prefix = first.Paragraph.Slice(0, start - first.Start);
            var suffix = last.Paragraph.Slice(end - last.Start, last.End - end);
            var from = first.Container.IndexOf(first.Paragraph); var to = first.Container.IndexOf(last.Paragraph);
            var pieces = text.Split('\n'); var replacement = new List<Block>();
            for (var i = 0; i < pieces.Length; i++)
            {
                var p = i == 0 ? first.Paragraph : new Paragraph { DefaultStyle = insertionStyle, Format = first.Paragraph.Format };
                if (i > 0 && p.Format.OutlineLevel > 0) p.Format = new();
                p.Runs = i == 0 ? prefix : [];
                if (pieces[i].Length > 0) p.Runs.Add(new(pieces[i], insertionStyle));
                if (i == pieces.Length - 1) p.Runs.AddRange(suffix);
                p.Normalize(); replacement.Add(p);
            }
            first.Container.RemoveRange(from, to - from + 1); first.Container.InsertRange(from, replacement);
            TransformAnchors(start, end - start, text.Length);
            if (TrackChanges && (text.Length > 0 || removed.Length > 0)) Document.Changes.Add(new() { Start = start, Removed = removed, Inserted = text, Author = Document.Author });
            Selection = new(start + text.Length, start + text.Length); TypingStyle = insertionStyle;
        });
    }
    private void TransformAnchors(int start, int removed, int inserted)
    {
        var end = start + removed; var delta = inserted - removed;
        int Map(int p, bool right) => p < start ? p : p > end ? p + delta : start + (right ? inserted : 0);
        foreach (var comment in Document.Comments) { comment.Start = Map(comment.Start, false); comment.End = Math.Max(comment.Start, Map(comment.End, true)); }
        foreach (var change in Document.Changes)
        {
            var changeEnd = change.Start + change.Inserted.Length;
            if (start < changeEnd && end > change.Start || removed == 0 && start > change.Start && start < changeEnd) change.CanReject = false;
            change.Start = change.Start >= end ? change.Start + delta : Map(change.Start, false);
        }
    }
    public void DeleteBackward()
    {
        if (!Selection.IsEmpty) { InsertText(""); return; }
        var prev = Index.Previous(Selection.Active); if (prev < Selection.Active) Replace(prev, Selection.Active - prev, "", "Backspace");
    }
    public void DeleteForward()
    {
        if (!Selection.IsEmpty) { InsertText(""); return; }
        var next = Index.Next(Selection.Active); if (next > Selection.Active) Replace(Selection.Active, next - Selection.Active, "", "Delete");
    }
}
