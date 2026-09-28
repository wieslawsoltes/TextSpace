namespace TextSpace.Storage;

public sealed record RecoveryArchive(string Id, DateTimeOffset SavedAt, long Bytes);

/// <summary>
/// Protected, content-addressed originals, separate from rolling AutoSave history.
/// ProtectAsync succeeds only after the original is committed. No automatic eviction.
/// </summary>
public interface IRecoveryArchiveStore
{
    Task<RecoveryArchive> ProtectAsync(string original, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryArchive>> ListProtectedAsync(CancellationToken cancellationToken = default);
    Task<string?> ReadProtectedAsync(string id, CancellationToken cancellationToken = default);
}

public static class RecoveryArchiveLimits
{
    public const int MaximumEntries = 16;
    public const int MaximumOriginalBytes = 32 * 1024 * 1024;
    public const long MaximumTotalBytes = 64L * 1024 * 1024;
    public static bool IsValidId(string id) => id is { Length: 64 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
