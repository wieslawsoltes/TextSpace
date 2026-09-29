using TextSpace.Core;

namespace TextSpace.Editing;

/// <summary>
/// A detached, single-writer gesture draft. Preview never mutates the document,
/// raises a document notification, creates undo entries or triggers AutoSave.
/// Commit rejects stale revisions/model replacement and publishes one transaction.
/// </summary>
public sealed class VisualEditDraft : IDisposable
{
    private readonly EditorSession _session;
    private readonly DocumentModel _document;
    private readonly VisualBlock _original;
    private readonly long _revision;
    private bool _completed;
    public VisualBlock Value { get; }
    public bool IsCurrent => !_completed && !_session.IsReadOnly && ReferenceEquals(_document, _session.Document)
        && _revision == _session.Revision && ReferenceEquals(_original, _session.FindVisual(_original.Id));
    public VisualEditDraft(EditorSession session, string id)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsReadOnly) throw new InvalidOperationException("This document is in reading mode.");
        _session = session; _document = session.Document; _revision = session.Revision;
        _original = session.FindVisual(id) ?? throw new InvalidOperationException("The object no longer exists.");
        Value = BlockTree.CloneVisual(_original);
    }
    public void Commit(string label)
    {
        if (!IsCurrent) throw new InvalidOperationException("The document changed during the gesture. The preview was not applied.");
        VisualBlockRules.Validate(Value);
        _completed = true; _session.ReplaceVisual(_original.Id, Value, label);
    }
    public void Dispose() => _completed = true;
}
