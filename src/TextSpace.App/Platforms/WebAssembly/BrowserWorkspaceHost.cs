using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextSpace.Storage;
using TextSpace.Workbench;

namespace TextSpace.App;

internal sealed partial class BrowserWorkspaceHost : IWorkspaceHost
{
    public string StorageDescription => "this browser's IndexedDB storage";
    [JSImport("globalThis.TextSpaceHost.loadRecovery")] private static partial Task<string?> LoadRecovery();
    [JSImport("globalThis.TextSpaceHost.saveRecovery")] private static partial Task SaveRecovery(string title, string document);
    [JSImport("globalThis.TextSpaceHost.listVersions")] private static partial Task<string> ListVersions();
    [JSImport("globalThis.TextSpaceHost.readVersion")] private static partial Task<string?> ReadVersion(string id);
    [JSImport("globalThis.TextSpaceHost.upload")] private static partial Task<string?> Upload(string acceptedExtensions);
    [JSImport("globalThis.TextSpaceHost.download")] private static partial void Download(string name, string base64, string contentType);
    [JSImport("globalThis.TextSpaceHost.readClipboard")] private static partial Task<string?> ReadClipboard();
    [JSImport("globalThis.TextSpaceHost.writeClipboard")] private static partial Task WriteClipboard(string text);
    [JSImport("globalThis.TextSpaceHost.printHtml")] private static partial Task PrintHtml(string html);
    [JSImport("globalThis.TextSpaceHost.openUri")] private static partial void OpenUri(string uri);
    [JSImport("globalThis.TextSpaceHost.ready")] internal static partial void Ready();
    [JSImport("globalThis.TextSpaceHost.startupError")] internal static partial void StartupError(string message);
    [JSImport("globalThis.TextSpaceHost.diagnosticsEnabled")] internal static partial bool DiagnosticsEnabled();
    public async Task<string?> ReadLatestAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return await LoadRecovery(); }
    public async Task WriteLatestAsync(string title, string document, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); await SaveRecovery(title, document); }
    public async Task<IReadOnlyList<RecoveryVersion>> ListVersionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); return JsonSerializer.Deserialize(await ListVersions(), BrowserJsonContext.Default.ListRecoveryVersion) ?? [];
    }
    public async Task<string?> ReadVersionAsync(string id, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return await ReadVersion(id); }
    public async Task<OpenedFile?> OpenFileAsync(string acceptedExtensions, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var json = await Upload(acceptedExtensions); if (json is null) return null;
        var file = JsonSerializer.Deserialize(json, BrowserJsonContext.Default.UploadedFileWire) ?? throw new InvalidDataException("The browser did not return a file."); return new(file.Name, Convert.FromBase64String(file.Base64));
    }
    public Task SaveFileAsync(string name, byte[] bytes, string contentType, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Download(name, Convert.ToBase64String(bytes), contentType); return Task.CompletedTask; }
    public async Task<string?> ReadClipboardAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return await ReadClipboard(); }
    public async Task WriteClipboardAsync(string text, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); await WriteClipboard(text); }
    public async Task PrintHtmlAsync(string html, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); await PrintHtml(html); }
    public Task OpenUriAsync(string uri, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); OpenUri(uri); return Task.CompletedTask; }
}

internal sealed record UploadedFileWire(string Name, string Base64);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UploadedFileWire))]
[JsonSerializable(typeof(List<RecoveryVersion>))]
internal partial class BrowserJsonContext : JsonSerializerContext;
