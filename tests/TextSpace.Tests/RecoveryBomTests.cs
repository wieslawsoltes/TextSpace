using System.Text;
using System.Text.Json.Nodes;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Storage;
using Xunit;

namespace TextSpace.Tests;

public sealed class RecoveryBomTests
{
    [Fact]
    public void ValidBomPrefixedFileIsParsedAsACopyWithoutChangingItsSource()
    {
        var json = DocumentJson.Save(DocumentJson.FromText("Keep żółć 😀", "Original"));
        var original = "\uFEFF" + json;
        var plan = DocumentRecovery.PrepareTabRepair(original);
        Assert.Empty(plan.Issues);
        Assert.Equal(json, plan.RepairedJson);
        Assert.Equal("Keep żółć 😀", plan.CreateDocument().PlainText);
        Assert.Equal('\uFEFF', original[0]);
    }

    [Fact]
    public async Task BomAndAllOriginalUtf8BytesSurviveProtectionAndExplicitTabRepair()
    {
        var document = DocumentJson.FromText("Preserve all original bytes", "Before repair");
        var node = JsonNode.Parse(DocumentJson.Save(document))!;
        var tabs = new JsonArray();
        for (var i = 0; i < 129; i++) tabs.Add(new JsonObject { ["position"] = i });
        node["blocks"]![0]!["format"]!["tabStops"] = tabs;
        var original = "\uFEFF" + node.ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(original);
        var directory = Path.Combine(Path.GetTempPath(), "TextSpace-bom-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileRecoveryArchiveStore(directory);
            var plan = DocumentRecovery.PrepareTabRepair(original);
            var archive = await store.ProtectAsync(original);
            Assert.Equal(1, plan.RemovedStops);
            Assert.Equal(128, plan.CreateDocument().Paragraphs().First().Format.TabStops.Length);
            var retained = await store.ReadProtectedAsync(archive.Id);
            Assert.Equal(original, retained);
            Assert.Equal(bytes, Encoding.UTF8.GetBytes(retained!));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
