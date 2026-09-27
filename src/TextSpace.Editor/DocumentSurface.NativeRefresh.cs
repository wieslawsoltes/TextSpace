namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    private bool _nativeViewRefreshQueued;

    private void QueueNativeViewRefresh()
    {
        if (_disposed || _nativeViewRefreshQueued) return;
        _nativeViewRefreshQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _nativeViewRefreshQueued = false;
            if (_disposed) return;
            // TextChanging has now returned, and the native TextBox has rebuilt
            // its internal text/caret state. Coalesce a character burst into one
            // presentation refresh while the model remains immediately current.
            Relayout();
            SyncInput();
            _caretVisible = true;
            EnsureCaretVisible();
            UpdateRuler();
            Invalidate();
        })) _nativeViewRefreshQueued = false;
    }
}
