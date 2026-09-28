using System.Security.Cryptography;
using System.Text;
using TextSpace.Storage;
using Xunit;

namespace TextSpace.Tests;

public sealed class RecoveryArchiveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TextSpace-archives-" + Guid.NewGuid().ToString("N"));
    private FileRecoveryArchiveStore Store() => new(_directory);

    [Fact]
    public async Task MalformedOriginalIsPreservedExactlyAndDeduplicated()
    {
        var source = " { deliberately broken JSON with Unicode: żółć 😀\r\n";
        var first = await Store().ProtectAsync(source);
        var second = await Store().ProtectAsync(source);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source))), first.Id);
        Assert.Equal(source, await Store().ReadProtectedAsync(first.Id));
        Assert.Single(await Store().ListProtectedAsync());
    }

    [Fact]
    public async Task BudgetNeverEvictsOlderOriginals()
    {
        var store = Store(); var first = await store.ProtectAsync("original 0");
        for (var i = 1; i < RecoveryArchiveLimits.MaximumEntries; i++) await store.ProtectAsync("original " + i);
        await Assert.ThrowsAsync<IOException>(() => store.ProtectAsync("one too many"));
        Assert.Equal("original 0", await store.ReadProtectedAsync(first.Id));
        Assert.Equal(first.Id, (await store.ProtectAsync("original 0")).Id);
        Assert.Equal(16, (await store.ListProtectedAsync()).Count);
    }

    [Fact]
    public async Task TamperingFailsChecksumVerification()
    {
        var store = Store(); var saved = await store.ProtectAsync("original");
        await File.WriteAllTextAsync(Path.Combine(_directory, saved.Id + ".textspace"), "modified");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadProtectedAsync(saved.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ProtectAsync("original"));
    }

    [Theory]
    [InlineData("../recovery")]
    [InlineData("")]
    [InlineData("/tmp/document")]
    public async Task RejectsUnsafeIdentifiers(string id)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store().ReadProtectedAsync(id));
    }

    [Fact]
    public async Task CancellationDoesNotCreateAnOriginal()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel(); var store = Store();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ProtectAsync("cancelled", cts.Token));
        Assert.Empty(await store.ListProtectedAsync());
    }

    [Fact]
    public async Task ParallelProtectionPublishesOneIdenticalOriginal()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Store().ProtectAsync("same document")));
        Assert.Single(results.Select(r => r.Id).Distinct());
        Assert.Single(await Store().ListProtectedAsync());
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
