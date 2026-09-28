using System.Text.Json.Nodes;
using TextSpace.Core;
using TextSpace.Documents;
using Xunit;

namespace TextSpace.Tests;

public sealed class LegacyParagraphDefaultsTests
{
    [Fact]
    public void SparseParagraphAndCharacterFormatsRetainConstructorDefaults()
    {
        var source = """
            {"formatVersion":1,"blocks":[{"$type":"paragraph","id":"p",
              "format":{},"runs":[{"text":"Legacy words","style":{"bold":true}}]}]}
            """;
        var document = DocumentJson.Load(source);
        var p = document.Paragraphs().Single();
        Assert.Equal("Normal", p.Format.StyleName);
        Assert.Equal(1.15, p.Format.LineSpacing);
        Assert.Equal(8, p.Format.SpaceAfter);
        Assert.True(p.Format.WidowControl);
        Assert.False(p.Format.TabStops.IsDefault);
        Assert.Equal(11, p.Runs[0].Style.FontSize);
        Assert.Equal("Aptos", p.Runs[0].Style.FontFamily);
        Assert.True(p.Runs[0].Style.Bold);
    }

    [Fact]
    public void DirectDefaultAssignmentAndWithExpressionsStayCanonical()
    {
        var p = new ParagraphFormat { TabStops = default };
        Assert.False(p.TabStops.IsDefault); Assert.Empty(p.TabStops);
        var q = p with { LeftIndent = 18, TabStops = default };
        Assert.False(q.TabStops.IsDefault); Assert.Empty(q.TabStops);
    }

    [Fact]
    public void OmittingOnlyNewTabPropertyRoundTripsOldDocumentWithoutRepair()
    {
        var document = SampleDocument.Create();
        var root = JsonNode.Parse(DocumentJson.Save(document))!;
        void RemoveTabs(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                obj.Remove("tabStops");
                foreach (var child in obj.Select(pair => pair.Value).OfType<JsonNode>().ToArray()) RemoveTabs(child);
            }
            else if (node is JsonArray array) foreach (var child in array.OfType<JsonNode>()) RemoveTabs(child);
        }
        RemoveTabs(root);
        var restored = DocumentJson.Load(root.ToJsonString());
        Assert.Equal(document.PlainText, restored.PlainText);
        Assert.All(restored.Paragraphs(), p => Assert.False(p.Format.TabStops.IsDefault));
        Assert.Equal(document.Comments.Count, restored.Comments.Count);
    }
}
