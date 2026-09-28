using System.Text.Json.Nodes;
using TextSpace.Core;
using TextSpace.Documents;
using Xunit;

namespace TextSpace.Tests;

public sealed class NativeDefaultContractTests
{
    private static JsonObject Native() => JsonNode.Parse(DocumentJson.Save(DocumentJson.FromText("Preserved text")))!.AsObject();

    [Fact]
    public void SparsePagePreservesDefaultsButHonorsExplicitChanges()
    {
        var root = Native(); root["page"] = new JsonObject { ["marginLeft"] = 36 };
        var document = DocumentJson.Load(root.ToJsonString());
        Assert.Equal(612, document.Page.Width); Assert.Equal(792, document.Page.Height);
        Assert.Equal(36, document.Page.MarginLeft); Assert.Equal(72, document.Page.MarginRight);
        Assert.Equal(1, document.Page.Columns); Assert.Equal("#FFFFFF", document.Page.Color);
    }

    [Fact]
    public void SparseTabRetainsDecimalCharacterAndGeometry()
    {
        var root = Native(); root["blocks"]![0]!["format"]!["tabStops"] = new JsonArray(new JsonObject { ["position"] = 72 });
        var stop = Assert.Single(DocumentJson.Load(root.ToJsonString()).Paragraphs().First().Format.TabStops);
        Assert.Equal(72, stop.Position); Assert.Equal('.', stop.DecimalCharacter);
        Assert.Equal(TabAlignment.Left, stop.Alignment); Assert.Equal(TabLeader.None, stop.Leader);
    }

    [Fact]
    public void ExplicitFalseAndZeroSpacingAreNotReplacedByDefaults()
    {
        var root = Native(); root["blocks"]![0]!["format"] = new JsonObject { ["widowControl"] = false, ["spaceAfter"] = 0 };
        var format = DocumentJson.Load(root.ToJsonString()).Paragraphs().First().Format;
        Assert.False(format.WidowControl); Assert.Equal(0, format.SpaceAfter); Assert.Equal(1.15, format.LineSpacing);
    }

    [Theory]
    [InlineData("lineSpacing")]
    [InlineData("fontSize")]
    [InlineData("width")]
    public void ExplicitInvalidZerosAreStillRejected(string property)
    {
        var root = Native();
        if (property == "width") root["page"]![property] = 0;
        else if (property == "fontSize") root["blocks"]![0]!["runs"]![0]!["style"]![property] = 0;
        else root["blocks"]![0]!["format"]![property] = 0;
        Assert.Throws<InvalidDataException>(() => DocumentJson.Load(root.ToJsonString()));
    }

    [Fact]
    public void ExplicitFormattingRoundTripPreservesImmutableRecordEquality()
    {
        var d = DocumentJson.FromText("Test"); var p = d.Paragraphs().Single();
        p.Format = p.Format with { StyleName = "Quote", LineSpacing = 2, KeepWithNext = true, SpaceAfter = 0, Shading = "#FFEEDD" };
        p.Runs[0].Style = new() { FontFamily = "Tinos", FontSize = 17, Bold = true, Italic = true, Color = "#123456", Hyperlink = "#Target", Underline = true };
        var loaded = DocumentJson.Load(DocumentJson.Save(d)).Paragraphs().Single();
        Assert.Equal(p.Format, loaded.Format); Assert.Equal(p.Runs[0].Style, loaded.Runs[0].Style);
    }
}
