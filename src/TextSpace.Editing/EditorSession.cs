using System.Runtime.ExceptionServices;
using TextSpace.Core;
using TextSpace.Documents;

namespace TextSpace.Editing;

public enum EditorChangeKind { Document, Selection, View, Saved }

public sealed class EditorChangedEventArgs(EditorChangeKind kind, string label) : EventArgs
{
    public EditorChangeKind Kind { get; } = kind;
    public string Label { get; } = label;
}

/// <summary>Single-writer editor with atomic notifications and bounded snapshot history.</summary>
public sealed partial class EditorSession
{
    private sealed record Snapshot(string Json, TextSelection Selection, TextStyle TypingStyle, string Label)
    {
        public long EstimatedBytes => (long)Json.Length * sizeof(char);
    }
    private readonly List<Snapshot> _undo = [], _redo = [];
    private bool _transaction;
    private EditorChangedEventArgs? _pendingNotification;
    private string _savedJson;
    public DocumentModel Document { get; private set; }
    public TextSelection Selection { get; private set; }
    public TextStyle TypingStyle { get; private set; } = new();
    public bool TrackChanges { get; set; }
    public bool IsReadOnly { get; set; }
    public long Revision { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public int MaximumHistoryEntries { get; }
    public long MaximumHistoryBytes { get; }
    public long HistoryBytes => _undo.Sum(s => s.EstimatedBytes) + _redo.Sum(s => s.EstimatedBytes);
    public bool IsDirty => DocumentJson.Save(Document) != _savedJson;
    public string UndoLabel => _undo.Count > 0 ? _undo[^1].Label : "";
    public TextIndex Index => new(Document);
    public Paragraph CurrentParagraph => Index.At(Selection.Active).Paragraph;
    public event EventHandler<EditorChangedEventArgs>? Changed;

    public EditorSession(DocumentModel document, int maximumHistoryEntries = 100, long maximumHistoryBytes = 64L * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumHistoryEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumHistoryBytes);
        DocumentJson.Validate(document); Document = document;
        MaximumHistoryEntries = maximumHistoryEntries; MaximumHistoryBytes = maximumHistoryBytes;
        _savedJson = DocumentJson.Save(document); TypingStyle = Index.At(0).Paragraph.StyleAt(0);
    }
    public void Notify(EditorChangeKind kind, string label = "")
    {
        var notification = new EditorChangedEventArgs(kind, label);
        if (_transaction) _pendingNotification = notification;
        else Changed?.Invoke(this, notification);
    }
    private void EnsureWritable()
    {
        if (IsReadOnly) throw new InvalidOperationException("This document is in reading mode.");
    }
    public void SetSelection(int anchor, int active)
    {
        var index = Index;
        if (anchor == active)
        {
            // Never expand a collapsed pointer position into a selected grapheme.
            var position = index.Snap(active); Selection = new(position, position);
        }
        else if (anchor < active) Selection = new(index.Snap(anchor), index.Snap(active, true));
        else Selection = new(index.Snap(anchor, true), index.Snap(active));
        var address = index.At(Selection.Active);
        TypingStyle = address.Paragraph.StyleAt(Math.Max(0, Selection.Active - address.Start - (Selection.IsEmpty ? 1 : 0)));
        Notify(EditorChangeKind.Selection);
    }
    public void SelectAll() => SetSelection(0, Index.Length);
    public void MarkSaved()
    {
        if (_transaction) throw new InvalidOperationException("Complete the edit before marking the document saved.");
        _savedJson = DocumentJson.Save(Document); Notify(EditorChangeKind.Saved);
    }
    public void Load(DocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_transaction) throw new InvalidOperationException("Cannot replace a document during an edit transaction.");
        DocumentJson.Validate(document); Document = document; _undo.Clear(); _redo.Clear(); Selection = new(0, 0);
        TypingStyle = Index.At(0).Paragraph.StyleAt(0); _savedJson = DocumentJson.Save(document);
        Revision++; Notify(EditorChangeKind.Document, "Open document");
    }
    public void Execute(string label, Action action)
    {
        ArgumentNullException.ThrowIfNull(action); EnsureWritable();
        if (_transaction) { action(); return; }
        var before = Capture(label); var changed = false;
        ExceptionDispatchInfo? failure = null;
        _transaction = true; _pendingNotification = null;
        try
        {
            action(); DocumentJson.Validate(Document);
            if (Selection.Start < 0 || Selection.End > Index.Length) throw new InvalidOperationException("The edit produced an invalid selection.");
            changed = DocumentJson.Save(Document) != before.Json;
            if (changed)
            {
                _redo.Clear(); AddHistory(_undo, before);
                Document.Modified = DateTimeOffset.UtcNow; Revision++;
            }
        }
        catch (Exception error)
        {
            Document = DocumentJson.Load(before.Json); Selection = before.Selection; TypingStyle = before.TypingStyle;
            _pendingNotification = null; failure = ExceptionDispatchInfo.Capture(error);
        }
        finally { _transaction = false; }
        var pending = _pendingNotification; _pendingNotification = null;
        // Only fully committed or fully restored state is observable by the UI.
        if (failure is not null)
        {
            try { Notify(EditorChangeKind.Document, "Rollback " + label); }
            finally { failure.Throw(); }
        }
        else if (changed) Notify(EditorChangeKind.Document, label);
        else if (pending is not null) Notify(pending.Kind, pending.Label);
    }
    private Snapshot Capture(string label) => new(DocumentJson.Save(Document), Selection, TypingStyle, label);
    private void AddHistory(List<Snapshot> destination, Snapshot snapshot)
    {
        if (MaximumHistoryEntries == 0 || snapshot.EstimatedBytes > MaximumHistoryBytes) { destination.Clear(); return; }
        destination.Add(snapshot);
        while (destination.Count > 0 && (destination.Count > MaximumHistoryEntries || HistoryBytes > MaximumHistoryBytes)) destination.RemoveAt(0);
    }
    public void Undo() => Restore(_undo, _redo, "Undo");
    public void Redo() => Restore(_redo, _undo, "Redo");
    private void Restore(List<Snapshot> source, List<Snapshot> target, string verb)
    {
        if (IsReadOnly || source.Count == 0) return;
        if (_transaction) throw new InvalidOperationException("Undo and redo cannot run inside an edit transaction.");
        var snapshot = source[^1]; var restored = DocumentJson.Load(snapshot.Json); var current = Capture(snapshot.Label);
        source.RemoveAt(source.Count - 1); Document = restored; Selection = snapshot.Selection; TypingStyle = snapshot.TypingStyle;
        AddHistory(target, current); Revision++; Notify(EditorChangeKind.Document, verb + " " + snapshot.Label);
    }
    public string SelectedText() => Index.Text.Substring(Selection.Start, Selection.Length);
    public void InsertText(string text) => Replace(Selection.Start, Selection.Length, text);
    public void Replace(int start, int length, string text, string label = "Typing")
    {
        ArgumentNullException.ThrowIfNull(text); EnsureWritable();
        var index = Index;
        ArgumentOutOfRangeException.ThrowIfNegative(start); ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (start > index.Length || length > index.Length - start) throw new ArgumentOutOfRangeException(nameof(length), "The replacement range lies outside the document.");
        var requestedEnd = start + length;
        start = index.Snap(start);
        var end = length == 0 ? start : index.Snap(requestedEnd, true);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if ((long)index.Length - (end - start) + text.Length > DocumentJson.MaxCharacters) throw new InvalidOperationException("The document text limit has been reached.");
        var first = index.At(start); var last = index.At(end);
        if (!ReferenceEquals(first.Container, last.Container)) throw new InvalidOperationException("This selection crosses table-cell boundaries. Edit one cell at a time, or select the entire table from outside it.");
        var removed = index.Text.Substring(start, end - start); var insertionStyle = TypingStyle;
        Execute(label, () =>
        {
            var prefix = first.Paragraph.Slice(0, start - first.Start); var suffix = last.Paragraph.Slice(end - last.Start, last.End - end);
            var from = first.Container.IndexOf(first.Paragraph); var to = first.Container.IndexOf(last.Paragraph);
            var pieces = text.Split('\n'); var replacement = new List<Block>();
            for (var i = 0; i < pieces.Length; i++)
            {
                var paragraph = i == 0 ? first.Paragraph : new Paragraph { DefaultStyle = insertionStyle, Format = first.Paragraph.Format };
                if (i > 0 && paragraph.Format.OutlineLevel > 0) paragraph.Format = new();
                paragraph.Runs = i == 0 ? prefix : [];
                if (pieces[i].Length > 0) paragraph.Runs.Add(new(pieces[i], insertionStyle));
                if (i == pieces.Length - 1) paragraph.Runs.AddRange(suffix);
                paragraph.Normalize(); replacement.Add(paragraph);
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
        int Map(int position, bool right) => position < start ? position : position > end ? position + delta : start + (right ? inserted : 0);
        foreach (var comment in Document.Comments) { comment.Start = Map(comment.Start, false); comment.End = Math.Max(comment.Start, Map(comment.End, true)); }
        foreach (var change in Document.Changes)
        {
            var changeEnd = change.Start + change.Inserted.Length;
            if ((start < changeEnd && end > change.Start) || (removed == 0 && start > change.Start && start < changeEnd)) change.CanReject = false;
            change.Start = change.Start >= end ? change.Start + delta : Map(change.Start, false);
        }
    }
    public void DeleteBackward()
    {
        EnsureWritable();
        if (!Selection.IsEmpty) { InsertText(""); return; }
        var previous = Index.Previous(Selection.Active); if (previous < Selection.Active) Replace(previous, Selection.Active - previous, "", "Backspace");
    }
    public void DeleteForward()
    {
        EnsureWritable();
        if (!Selection.IsEmpty) { InsertText(""); return; }
        var next = Index.Next(Selection.Active); if (next > Selection.Active) Replace(Selection.Active, next - Selection.Active, "", "Delete");
    }
}
