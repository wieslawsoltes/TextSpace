using System.Text;

namespace TextSpace.Storage;

/// <summary>Atomic replacement with a bounded journal. A failed write never removes the previous successful snapshot.</summary>
public sealed class FileRecoveryStore : IRecoveryStore, IDisposable
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastVersion;
    public FileRecoveryStore(string directory) { _directory = Path.GetFullPath(directory); Directory.CreateDirectory(_directory); }
    public async Task<string?> ReadLatestAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_directory, "recovery.textspace"); return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }
    public async Task WriteLatestAsync(string title, string document, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var path = Path.Combine(_directory, "recovery.textspace"); var temporary = Path.Combine(_directory, "recovery." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllTextAsync(temporary, document, new UTF8Encoding(false), cancellationToken);
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            if (DateTimeOffset.UtcNow - _lastVersion > TimeSpan.FromMinutes(2))
            {
                var id = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..6];
                await File.WriteAllTextAsync(Path.Combine(_directory, id + ".textspace"), document, cancellationToken);
                await File.WriteAllTextAsync(Path.Combine(_directory, id + ".title"), title, cancellationToken); _lastVersion = DateTimeOffset.UtcNow;
                foreach (var file in Directory.EnumerateFiles(_directory, "*.textspace").Where(p => Path.GetFileName(p) != "recovery.textspace").OrderDescending().Skip(12)) { File.Delete(file); var titlePath = Path.ChangeExtension(file, ".title"); if (File.Exists(titlePath)) File.Delete(titlePath); }
            }
        }
        finally { _gate.Release(); }
    }
    public Task<IReadOnlyList<RecoveryVersion>> ListVersionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RecoveryVersion> result = Directory.EnumerateFiles(_directory, "*.textspace").Where(p => Path.GetFileName(p) != "recovery.textspace").OrderDescending().Select(p => new FileInfo(p)).Select(f => new RecoveryVersion(Path.GetFileNameWithoutExtension(f.Name), File.Exists(Path.ChangeExtension(f.FullName, ".title")) ? File.ReadAllText(Path.ChangeExtension(f.FullName, ".title")) : "Recovered document", f.LastWriteTimeUtc, (int)Math.Min(int.MaxValue, f.Length))).ToArray();
        return Task.FromResult(result);
    }
    public async Task<string?> ReadVersionAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id.Length > 100 || id.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new ArgumentException("Invalid recovery version identifier.", nameof(id));
        var path = Path.Combine(_directory, id + ".textspace"); return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }
    public void Dispose() => _gate.Dispose();
}
