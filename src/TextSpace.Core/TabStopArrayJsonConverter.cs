using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TextSpace.Core;

/// <summary>
/// Native compatibility for null/empty tab arrays. Actual stops remain subject
/// to strict model validation; oversized arrays fail before allocating their tail.
/// TabStop metadata must be present in a source-generated serializer context.
/// </summary>
public sealed class TabStopArrayJsonConverter : JsonConverter<ImmutableArray<TabStop>>
{
    public override bool HandleNull => true;

    public override ImmutableArray<TabStop> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return [];
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Tab stops must be an array or null.");
        ImmutableArray<TabStop>.Builder? result = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return result?.ToImmutable() ?? [];
            result ??= ImmutableArray.CreateBuilder<TabStop>();
            if (result.Count == TabStopRules.MaximumCount)
                throw new InvalidDataException($"A paragraph supports at most {TabStopRules.MaximumCount} tab stops. Open and Repair can create a separate copy; the original must be preserved.");
            var stop = JsonSerializer.Deserialize<TabStop>(ref reader, options);
            if (stop is null) throw new InvalidDataException("A tab stop cannot be null.");
            result.Add(stop);
        }
        throw new JsonException("The tab-stop array is not complete.");
    }

    public override void Write(Utf8JsonWriter writer, ImmutableArray<TabStop> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        // A default immutable collection has the same native representation as
        // no custom stops. Direct model validation still diagnoses uninitialized state.
        if (!value.IsDefault)
            foreach (var stop in value) JsonSerializer.Serialize(writer, stop, options);
        writer.WriteEndArray();
    }
}
