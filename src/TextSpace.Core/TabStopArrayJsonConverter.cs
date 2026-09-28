using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        JsonTypeInfo<TabStop>? metadata = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return result?.ToImmutable() ?? [];
            result ??= ImmutableArray.CreateBuilder<TabStop>();
            if (result.Count == TabStopRules.MaximumCount)
                throw new InvalidDataException($"A paragraph supports at most {TabStopRules.MaximumCount} tab stops. Open and Repair can create a separate copy; the original must be preserved.");
            // Resolve once per nonempty collection using the caller's generated
            // contract. Do not fall back to reflection in a trimmed WASM build.
            metadata ??= (JsonTypeInfo<TabStop>)options.GetTypeInfo(typeof(TabStop));
            var stop = JsonSerializer.Deserialize(ref reader, metadata);
            if (stop is null) throw new InvalidDataException("A tab stop cannot be null.");
            result.Add(stop);
        }
        throw new JsonException("The tab-stop array is not complete.");
    }

    public override void Write(Utf8JsonWriter writer, ImmutableArray<TabStop> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (!value.IsDefaultOrEmpty)
        {
            var metadata = (JsonTypeInfo<TabStop>)options.GetTypeInfo(typeof(TabStop));
            foreach (var stop in value) JsonSerializer.Serialize(writer, stop, metadata);
        }
        writer.WriteEndArray();
    }
}
