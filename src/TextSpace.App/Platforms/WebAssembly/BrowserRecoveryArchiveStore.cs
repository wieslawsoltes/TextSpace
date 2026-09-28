using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextSpace.Storage;

namespace TextSpace.App;

internal sealed partial class BrowserRecoveryArchiveStore : IRecoveryArchiveStore
{
    [JSImport("globalThis.TextSpaceRecovery.protect")] private static partial Task<string> Protect(string original);
    [JSImport("globalThis.TextSpaceRecovery.list")] private static partial Task<string> List();
    [JSImport("globalThis.TextSpaceRecovery.read")] private static partial Task<string?> Read(string id);
    [JSImport("globalThis.TextSpaceRecovery.complete")] internal static partial void Complete();
    [JSImport("globalThis.TextSpaceRecovery.publishState")] internal static partial void PublishState(string json);

    public async Task<RecoveryArchive> ProtectAsync(string original, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return JsonSerializer.Deserialize(await Protect(original), RecoveryWireContext.Default.RecoveryArchive)
            ?? throw new InvalidDataException("The browser did not confirm original protection.");
    }
    public async Task<IReadOnlyList<RecoveryArchive>> ListProtectedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return JsonSerializer.Deserialize(await List(), RecoveryWireContext.Default.ListRecoveryArchive) ?? [];
    }
    public async Task<string?> ReadProtectedAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); return await Read(id);
    }
}

// Closed diagnostic DTOs survive trimming without retaining unrelated application
// metadata. They contain only UI state and geometry, never the original payload.
internal sealed record RecoveryControlSnapshot(string Name, double X, double Y, double Width, double Height, bool Enabled);
internal sealed record RecoveryUiSnapshot(bool Active, bool CanDownload, bool RepairPreviewed, string Message, List<RecoveryControlSnapshot> Controls);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RecoveryArchive))]
[JsonSerializable(typeof(List<RecoveryArchive>))]
[JsonSerializable(typeof(RecoveryUiSnapshot))]
internal partial class RecoveryWireContext : JsonSerializerContext;
