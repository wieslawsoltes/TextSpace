using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TextSpace.Storage;

/// <summary>
/// Checksum-verified framing for original text crossing native/browser string
/// boundaries. The envelope starts with JSON syntax, never a payload BOM.
/// Archive storage retains the unchanged original, not this transport envelope.
/// </summary>
public static class RecoveryArchiveTransport
{
    private const int MaximumBytes = 32 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string Encode(string original)
    {
        var bytes = GetBytes(original);
        var payload = new RecoveryArchivePayload(original,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length);
        return JsonSerializer.Serialize(payload, RecoveryTransportContext.Default.RecoveryArchivePayload);
    }

    public static string Decode(string envelope, string? expectedId = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Length > MaximumBytes * 6L + 512)
            throw new InvalidDataException("Protected recovery transport exceeds its size limit.");
        var payload = JsonSerializer.Deserialize(envelope, RecoveryTransportContext.Default.RecoveryArchivePayload)
            ?? throw new InvalidDataException("Protected recovery transport is empty.");
        var bytes = GetBytes(payload.Original);
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (bytes.Length != payload.Utf8Bytes || actual != payload.Sha256
            || expectedId is not null && actual != expectedId)
            throw new InvalidDataException("Protected recovery transport checksum verification failed.");
        return payload.Original;
    }

    private static byte[] GetBytes(string original)
    {
        if (original is null || original.Length > MaximumBytes)
            throw new InvalidDataException("Original recovery exceeds its size limit or is null.");
        try
        {
            if (Utf8.GetByteCount(original) > MaximumBytes)
                throw new InvalidDataException("Original recovery exceeds 32 MB.");
            return Utf8.GetBytes(original);
        }
        catch (EncoderFallbackException ex)
        {
            throw new InvalidDataException("Original recovery contains invalid Unicode; it cannot be silently transcoded.", ex);
        }
    }
}

internal sealed record RecoveryArchivePayload(string Original, string Sha256, int Utf8Bytes);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RecoveryArchivePayload))]
internal partial class RecoveryTransportContext : JsonSerializerContext;
