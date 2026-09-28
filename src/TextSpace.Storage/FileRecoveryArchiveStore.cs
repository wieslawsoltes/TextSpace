using System.Security.Cryptography;
using System.Text;

namespace TextSpace.Storage;

/// <summary>
/// Originals are atomically published under SHA-256 filenames and never evicted.
/// A cross-process lock serializes the budget check with publication. Failed writes
/// leave existing originals and the caller's active recovery untouched.
/// </summary>
public sealed class FileRecoveryArchiveStore : IRecoveryArchiveStore
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly string _directory;
    public FileRecoveryArchiveStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }

    public async Task<RecoveryArchive> ProtectAsync(string original, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Length > RecoveryArchiveLimits.MaximumOriginalBytes)
            throw new InvalidDataException("The original exceeds the 32 MB recovery archive limit.");
        var bytes = Utf8.GetBytes(original);
        if (bytes.Length > RecoveryArchiveLimits.MaximumOriginalBytes)
            throw new InvalidDataException("The original exceeds the 32 MB recovery archive limit.");
        var id = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var path = Path.Combine(_directory, id + ".textspace");
        await using var fileLock = await AcquireLockAsync(cancellationToken);
        if (File.Exists(path))
        {
            var existing = await File.ReadAllBytesAsync(path, cancellationToken);
            if (!existing.AsSpan().SequenceEqual(bytes))
                throw new InvalidDataException("The protected original failed its content-address check.");
            return Describe(path);
        }
        var current = Entries();
        if (current.Count >= RecoveryArchiveLimits.MaximumEntries || current.Sum(x => x.Bytes) + bytes.Length > RecoveryArchiveLimits.MaximumTotalBytes)
            throw new IOException("Protected recovery storage is full. Download your original; no existing backup has been removed.");
        var temporary = Path.Combine(_directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: false);
            return Describe(path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public Task<IReadOnlyList<RecoveryArchive>> ListProtectedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RecoveryArchive>>(Entries());
    }

    public async Task<string?> ReadProtectedAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!RecoveryArchiveLimits.IsValidId(id)) throw new ArgumentException("Invalid protected recovery identifier.", nameof(id));
        var path = Path.Combine(_directory, id + ".textspace");
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > RecoveryArchiveLimits.MaximumOriginalBytes)
            throw new InvalidDataException("Protected recovery is oversized.");
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != id)
            throw new InvalidDataException("The protected original failed its checksum verification.");
        return Utf8.GetString(bytes);
    }

    private List<RecoveryArchive> Entries() => Directory.EnumerateFiles(_directory, "*.textspace")
        .Where(path => RecoveryArchiveLimits.IsValidId(Path.GetFileNameWithoutExtension(path)))
        .Select(Describe).OrderByDescending(x => x.SavedAt).ToList();

    private static RecoveryArchive Describe(string path)
    {
        var file = new FileInfo(path);
        return new(Path.GetFileNameWithoutExtension(path), file.CreationTimeUtc, file.Length);
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_directory, ".archive.lock");
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 200) { await Task.Delay(25, cancellationToken); }
        }
    }
}
