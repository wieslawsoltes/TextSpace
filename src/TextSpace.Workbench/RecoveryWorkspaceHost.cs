using TextSpace.Storage;

namespace TextSpace.Workbench;

/// <summary>Adds protected originals to any platform host without changing its ordinary file or AutoSave services.</summary>
public sealed class RecoveryWorkspaceHost(IWorkspaceHost inner, IRecoveryArchiveStore originals) : IWorkspaceHost, IRecoveryArchiveStore, IDisposable
{
    public string StorageDescription => inner.StorageDescription;
    public Task<string?> ReadLatestAsync(CancellationToken token = default) => inner.ReadLatestAsync(token);
    public Task WriteLatestAsync(string title, string document, CancellationToken token = default) => inner.WriteLatestAsync(title, document, token);
    public Task<IReadOnlyList<RecoveryVersion>> ListVersionsAsync(CancellationToken token = default) => inner.ListVersionsAsync(token);
    public Task<string?> ReadVersionAsync(string id, CancellationToken token = default) => inner.ReadVersionAsync(id, token);
    public Task<OpenedFile?> OpenFileAsync(string extensions, CancellationToken token = default) => inner.OpenFileAsync(extensions, token);
    public Task SaveFileAsync(string name, byte[] bytes, string type, CancellationToken token = default) => inner.SaveFileAsync(name, bytes, type, token);
    public Task<string?> ReadClipboardAsync(CancellationToken token = default) => inner.ReadClipboardAsync(token);
    public Task WriteClipboardAsync(string text, CancellationToken token = default) => inner.WriteClipboardAsync(text, token);
    public Task PrintHtmlAsync(string html, CancellationToken token = default) => inner.PrintHtmlAsync(html, token);
    public Task OpenUriAsync(string uri, CancellationToken token = default) => inner.OpenUriAsync(uri, token);
    public Task<RecoveryArchive> ProtectAsync(string original, CancellationToken token = default) => originals.ProtectAsync(original, token);
    public Task<IReadOnlyList<RecoveryArchive>> ListProtectedAsync(CancellationToken token = default) => originals.ListProtectedAsync(token);
    public Task<string?> ReadProtectedAsync(string id, CancellationToken token = default) => originals.ReadProtectedAsync(id, token);
    public void Dispose() { if (inner is IDisposable disposable) disposable.Dispose(); if (originals is IDisposable archive) archive.Dispose(); }
}
