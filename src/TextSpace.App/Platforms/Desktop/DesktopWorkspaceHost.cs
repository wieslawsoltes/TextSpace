using System.Diagnostics;
using TextSpace.Storage;
using TextSpace.Workbench;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TextSpace.App;

internal sealed class DesktopWorkspaceHost : IWorkspaceHost, IDisposable
{
    private readonly FileRecoveryStore _recovery = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TextSpace"));
    public string StorageDescription => "this computer's application-data directory";
    public Task<string?> ReadLatestAsync(CancellationToken cancellationToken = default) => _recovery.ReadLatestAsync(cancellationToken);
    public Task WriteLatestAsync(string title, string document, CancellationToken cancellationToken = default) => _recovery.WriteLatestAsync(title, document, cancellationToken);
    public Task<IReadOnlyList<RecoveryVersion>> ListVersionsAsync(CancellationToken cancellationToken = default) => _recovery.ListVersionsAsync(cancellationToken);
    public Task<string?> ReadVersionAsync(string id, CancellationToken cancellationToken = default) => _recovery.ReadVersionAsync(id, cancellationToken);
    public async Task<OpenedFile?> OpenFileAsync(string acceptedExtensions, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var picker = new FileOpenPicker(); foreach (var extension in acceptedExtensions.Split(',')) picker.FileTypeFilter.Add(extension.Trim());
        var file = await picker.PickSingleFileAsync(); if (file is null) return null;
        var properties = await file.GetBasicPropertiesAsync(); if (properties.Size > 32 * 1024 * 1024) throw new InvalidDataException("Files must be smaller than 32 MB.");
        using var stream = await file.OpenStreamForReadAsync(); using var output = new MemoryStream(); await stream.CopyToAsync(output, cancellationToken); return new(file.Name, output.ToArray());
    }
    public async Task SaveFileAsync(string name, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(name) }; picker.FileTypeChoices.Add(contentType, [Path.GetExtension(name)]);
        var file = await picker.PickSaveFileAsync(); if (file is null) throw new OperationCanceledException("Save was cancelled."); await FileIO.WriteBytesAsync(file, bytes);
    }
    public async Task<string?> ReadClipboardAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var content = Clipboard.GetContent(); return content.Contains(StandardDataFormats.Text) ? await content.GetTextAsync() : null;
    }
    public Task WriteClipboardAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var package = new DataPackage(); package.SetText(text); Clipboard.SetContent(package); return Task.CompletedTask;
    }
    public async Task PrintHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TextSpace"); Directory.CreateDirectory(directory); var path = Path.Combine(directory, "print-" + Guid.NewGuid().ToString("N") + ".html");
        await File.WriteAllTextAsync(path, html.Replace("</body>", "<script>window.addEventListener('load',()=>window.print())</script></body>"), cancellationToken);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    public Task OpenUriAsync(string uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); if (!Uri.TryCreate(uri, UriKind.Absolute, out var address) || address.Scheme is not ("https" or "http" or "mailto")) throw new InvalidOperationException("Unsupported address scheme.");
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return Task.CompletedTask;
    }
    public void Dispose() => _recovery.Dispose();
}
