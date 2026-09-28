using TextSpace.Core;

namespace TextSpace.Editing;

/// <summary>
/// Single-threaded, disposable presentation cache of a session's committed text.
/// It invalidates on document notifications, revision changes and document swaps.
/// Raw model edits must be followed by Notify(Document) or Invalidate before UI use.
/// The editor's live Index semantics and transactional mutation paths are unchanged.
/// </summary>
public sealed class SessionTextProjection : IDisposable
{
    private readonly EditorSession _session;
    private DocumentModel? _document;
    private string? _text;
    private long _revision = -1;
    private bool _disposed;
    public long Captures { get; private set; }

    public SessionTextProjection(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _session.Changed += OnChanged;
    }

    public string Text
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_text is not null && ReferenceEquals(_document, _session.Document) && _revision == _session.Revision)
                return _text;
            _document = _session.Document;
            _revision = _session.Revision;
            _text = new TextIndex(_document).Text;
            Captures++;
            return _text;
        }
    }

    public void Invalidate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _text = null;
    }
    private void OnChanged(object? sender, EditorChangedEventArgs e)
    {
        if (e.Kind == EditorChangeKind.Document) _text = null;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Changed -= OnChanged;
        _text = null; _document = null;
    }
}
