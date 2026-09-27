namespace TextSpace.Storage;

public sealed record RecoveryVersion(string Id, string Title, DateTimeOffset SavedAt, int Bytes);

public interface IRecoveryStore
{
    Task<string?> ReadLatestAsync(CancellationToken cancellationToken = default);
    Task WriteLatestAsync(string title, string document, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryVersion>> ListVersionsAsync(CancellationToken cancellationToken = default);
    Task<string?> ReadVersionAsync(string id, CancellationToken cancellationToken = default);
}
