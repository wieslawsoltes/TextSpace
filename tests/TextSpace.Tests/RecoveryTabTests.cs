using System.Collections.Immutable;
using System.Text.Json.Nodes;
using TextSpace.Core;
using TextSpace.Documents;
using Xunit;

namespace TextSpace.Tests;

public sealed class RecoveryTabTests
{
    private static JsonObject Model() => JsonNode.Parse(DocumentJson.Save(DocumentJson.FromText("Keep my words.\nSecond paragraph.")))!.AsObject();
    private static JsonObject Format(JsonObject root) => root["blocks"]![0]!["format"]!.AsObject();
    private static JsonObject Stop(double position) => new() { ["position"] = position, ["alignment"] = "Right", ["leader"] = "Dot", ["decimalCharacter"] = "." };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyMissingAndNullArraysLoadAsEmpty(bool explicitNull)
    {
        var root = Model();
        if (explicitNull) Format(root)["tabStops"] = null;
        else Format(root).Remove("tabStops");
        var document = DocumentJson.Load(root.ToJsonString());
        Assert.False(document.Paragraphs().First().Format.TabStops.IsDefault);
        Assert.Empty(document.Paragraphs().First().Format.TabStops);
        Assert.Equal("Keep my words.\nSecond paragraph.", document.PlainText);
        Assert.Contains("\"tabStops\":[]", DocumentJson.Save(document));
    }

    [Fact]
    public void ActualOverflowStillFailsStrictLoading()
    {
        var root = Model(); var stops = new JsonArray();
        for (var i = 0; i < 129; i++) stops.Add(Stop(i));
        Format(root)["tabStops"] = stops;
        var ex = Assert.Throws<InvalidDataException>(() => DocumentJson.Load(root.ToJsonString()));
        Assert.Contains("128", ex.Message);
    }

    [Fact]
    public void DefaultCollectionHasDistinctDiagnostic()
    {
        var ex = Assert.Throws<InvalidDataException>(() => TabStopRules.Validate(default));
        Assert.Contains("uninitialized", ex.Message);
        Assert.DoesNotContain("128", ex.Message);
    }

    [Fact]
    public void RepairPreservesTextAndEveryNonTabProperty()
    {
        var root = Model(); var stops = new JsonArray();
        for (var i = 0; i < 129; i++) stops.Add(Stop(i));
        Format(root)["tabStops"] = stops;
        root["subject"] = "Preserve metadata";
        var original = root.ToJsonString();
        var plan = DocumentRecovery.PrepareTabRepair(original);
        Assert.Single(plan.Issues); Assert.Equal(1, plan.RemovedStops);
        Assert.Equal(129, plan.Issues[0].OriginalCount); Assert.Equal(128, plan.Issues[0].RetainedCount);
        Assert.Equal(original, root.ToJsonString());
        var repaired = JsonNode.Parse(plan.RepairedJson)!.AsObject();
        Format(root).Remove("tabStops"); Format(repaired).Remove("tabStops");
        Assert.True(JsonNode.DeepEquals(root, repaired));
        Assert.Equal("Keep my words.\nSecond paragraph.", plan.CreateDocument().PlainText);
    }

    [Fact]
    public void RepairDeterministicallyKeepsFirstValidPosition()
    {
        var root = Model();
        Format(root)["tabStops"] = new JsonArray(Stop(12), Stop(12.001), null,
            Stop(4001), new JsonObject { ["position"] = 72, ["alignment"] = "Unsupported" }, Stop(100));
        var plan = DocumentRecovery.PrepareTabRepair(root.ToJsonString());
        var issue = Assert.Single(plan.Issues);
        Assert.Equal(3, issue.InvalidCount); Assert.Equal(1, issue.DuplicateCount);
        Assert.Equal(new[] { 12d, 100d }, plan.CreateDocument().Paragraphs().First().Format.TabStops.Select(s => s.Position));
    }

    [Fact]
    public void RepairsNestedTableParagraphWithoutFlatteningIt()
    {
        var table = TableBlock.Create(1, 1);
        var document = new DocumentModel { Blocks = [table, new Paragraph("After")] };
        var paragraph = (Paragraph)table.Rows[0].Cells[0].Blocks[0];
        paragraph.Runs.Add(new("Nested words"));
        paragraph.Format = paragraph.Format with { TabStops = Enumerable.Range(0, 129).Select(i => new TabStop { Position = i }).ToImmutableArray() };
        var plan = DocumentRecovery.PrepareTabRepair(DocumentJson.Save(document));
        Assert.Single(plan.Issues);
        Assert.IsType<TableBlock>(plan.CreateDocument().Blocks[0]);
        Assert.Equal("Nested words\nAfter", plan.CreateDocument().PlainText);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"formatVersion\":2,\"blocks\":[]}")]
    [InlineData("{\"formatVersion\":1}")]
    public void CannotRepairUnrelatedCorruption(string source)
    {
        Assert.ThrowsAny<Exception>(() => DocumentRecovery.PrepareTabRepair(source));
    }

    [Fact]
    public void InvalidPageIsNotSilentlyRepaired()
    {
        var root = Model(); root["page"]!["width"] = -1;
        Assert.Throws<InvalidDataException>(() => DocumentRecovery.PrepareTabRepair(root.ToJsonString()));
    }

    [Fact]
    public void RepairIsIdempotentAndValidDocumentsAreUnchanged()
    {
        var source = DocumentJson.Save(SampleDocument.Create());
        var plan = DocumentRecovery.PrepareTabRepair(source);
        Assert.Empty(plan.Issues); Assert.Equal(source, plan.RepairedJson);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(8)] [InlineData(9)] [InlineData(128)]
    public void SmallAndLargeValidationPathsAgree(int count)
    {
        var stops = Enumerable.Range(0, count).Select(i => new TabStop { Position = i }).ToImmutableArray();
        TabStopRules.Validate(stops);
        if (count > 0) Assert.Throws<InvalidDataException>(() => TabStopRules.Validate(stops.SetItem(count - 1, new TabStop { Position = 5000 })));
    }

    [Fact]
    public void DuplicatePositionsRespectRelativeEdge()
    {
        TabStopRules.Validate([new() { Position = 10 }, new() { Position = 10, RelativeToRightEdge = true }]);
        Assert.Throws<InvalidDataException>(() => TabStopRules.Validate([new() { Position = 10 }, new() { Position = 10.001 }]));
    }

    [Fact]
    public void EmptyAndSmallValidationHaveNoPerCallAllocationAfterWarmup()
    {
        ImmutableArray<TabStop> small = [new() { Position = 12 }, new() { Position = 24 }];
        for (var i = 0; i < 100; i++) { TabStopRules.Validate([]); TabStopRules.Validate(small); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { TabStopRules.Validate([]); TabStopRules.Validate(small); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
