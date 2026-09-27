using TextSpace.Storage;

namespace TextSpace.Workbench;

public sealed record OpenedFile(string Name, byte[] Bytes);

/// <summary>All platform-dependent capabilities are explicit and replaceable by the embedding application.</summary>
public interface IWorkspaceHost : IRecoveryStore
{
    string StorageDescription { get; }
    Task<OpenedFile?> OpenFileAsync(string acceptedExtensions, CancellationToken cancellationToken = default);
    Task SaveFileAsync(string name, byte[] bytes, string contentType, CancellationToken cancellationToken = default);
    Task<string?> ReadClipboardAsync(CancellationToken cancellationToken = default);
    Task WriteClipboardAsync(string text, CancellationToken cancellationToken = default);
    Task PrintHtmlAsync(string html, CancellationToken cancellationToken = default);
    Task OpenUriAsync(string uri, CancellationToken cancellationToken = default);
}
