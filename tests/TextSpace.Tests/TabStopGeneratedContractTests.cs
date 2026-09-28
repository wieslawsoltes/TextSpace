using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextSpace.Core;
using Xunit;

namespace TextSpace.Tests;

public sealed class TabStopGeneratedContractTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"tabStops\":null}")]
    [InlineData("{\"tabStops\":[]}")]
    public void MissingNullAndEmptyArraysUseAClosedGeneratedContract(string json)
    {
        var format = JsonSerializer.Deserialize(json, TabContractContext.Default.ParagraphFormat)!;
        Assert.False(format.TabStops.IsDefault);
        Assert.Empty(format.TabStops);
        Assert.Equal(1.15, format.LineSpacing);
        Assert.True(format.WidowControl);
        var encoded = JsonSerializer.Serialize(format, TabContractContext.Default.ParagraphFormat);
        Assert.Contains("\"tabStops\":[]", encoded);
    }

    [Fact]
    public void NonemptyArrayRetainsAllTabPropertiesWithGeneratedMetadataOnly()
    {
        var stop = new TabStop(72.5, TabAlignment.Decimal, TabLeader.Dot, true, ',');
        var format = new ParagraphFormat { TabStops = [stop], WidowControl = false, SpaceAfter = 0 };
        var encoded = JsonSerializer.Serialize(format, TabContractContext.Default.ParagraphFormat);
        var decoded = JsonSerializer.Deserialize(encoded, TabContractContext.Default.ParagraphFormat)!;
        Assert.Equal(stop, Assert.Single(decoded.TabStops));
        Assert.False(decoded.WidowControl);
        Assert.Equal(0, decoded.SpaceAfter);
    }

    [Fact]
    public void MaximumArrayRoundTripsWithoutReflectionFallback()
    {
        var stops = Enumerable.Range(0, TabStopRules.MaximumCount)
            .Select(i => new TabStop(i, TabAlignment.Right, TabLeader.Dot)).ToImmutableArray();
        var format = new ParagraphFormat { TabStops = stops };
        var encoded = JsonSerializer.Serialize(format, TabContractContext.Default.ParagraphFormat);
        var decoded = JsonSerializer.Deserialize(encoded, TabContractContext.Default.ParagraphFormat)!;
        Assert.Equal(stops.ToArray(), decoded.TabStops.ToArray());
    }

    [Fact]
    public void DecoderRejectsTheOversizedTailBeforeAttemptingItsTypeConversion()
    {
        var acceptedPrefix = string.Join(',', Enumerable.Repeat("{}", TabStopRules.MaximumCount));
        var json = "{\"tabStops\":[" + acceptedPrefix + ",\"not a stop\"]}";
        var error = Assert.Throws<InvalidDataException>(() =>
            JsonSerializer.Deserialize(json, TabContractContext.Default.ParagraphFormat));
        Assert.Contains("at most 128", error.Message);
    }

    [Fact]
    public void SparseStopPreservesItsConstructorDefaults()
    {
        var format = JsonSerializer.Deserialize("{\"tabStops\":[{\"position\":36}]}",
            TabContractContext.Default.ParagraphFormat)!;
        var stop = Assert.Single(format.TabStops);
        Assert.Equal(36, stop.Position);
        Assert.Equal('.', stop.DecimalCharacter);
        Assert.Equal(TabAlignment.Left, stop.Alignment);
        Assert.Equal(TabLeader.None, stop.Leader);
    }
}

// The converter receives only this source-generated resolver. No default
// reflection resolver or application-wide serializer cache is composed with it.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ParagraphFormat))]
[JsonSerializable(typeof(TabStop))]
internal partial class TabContractContext : JsonSerializerContext;
