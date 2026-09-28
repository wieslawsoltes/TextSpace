using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using TextSpace.Storage;
using Xunit;

namespace TextSpace.Tests;

public sealed class RecoveryTransportTests
{
    [Theory]
    [InlineData("")]
    [InlineData("\uFEFF{\"title\":\"żółć 😀\"}")]
    [InlineData("\uFEFF\uFEFFRepeated markers are data")]
    [InlineData("Quotes: \" \\ \r\n\t \u0000 \uFEFF")]
    public void EnvelopePreservesEveryOriginalUtf8Byte(string original)
    {
        var bytes = Encoding.UTF8.GetBytes(original);
        var id = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var envelope = RecoveryArchiveTransport.Encode(original);
        Assert.StartsWith("{", envelope);
        var decoded = RecoveryArchiveTransport.Decode(envelope, id);
        Assert.Equal(original, decoded);
        Assert.Equal(bytes, Encoding.UTF8.GetBytes(decoded));
    }

    [Fact]
    public void ChangedPayloadIsRejectedWithoutRecomputingItsClaimedChecksum()
    {
        var node = JsonNode.Parse(RecoveryArchiveTransport.Encode("\uFEFFOriginal"))!;
        node["original"] = "Original";
        Assert.Throws<InvalidDataException>(() => RecoveryArchiveTransport.Decode(node.ToJsonString()));
    }

    [Fact]
    public void WrongArchiveIdentityIsRejected()
    {
        var payload = RecoveryArchiveTransport.Encode("Original");
        Assert.Throws<InvalidDataException>(() => RecoveryArchiveTransport.Decode(payload, new string('0', 64)));
    }

    [Fact]
    public void IncorrectByteCountIsRejected()
    {
        var node = JsonNode.Parse(RecoveryArchiveTransport.Encode("żółć 😀"))!;
        node["utf8Bytes"] = 1;
        Assert.Throws<InvalidDataException>(() => RecoveryArchiveTransport.Decode(node.ToJsonString()));
    }

    [Fact]
    public void InvalidUnicodeCannotBeSilentlyTranscoded()
    {
        var original = new string(['\uD800']);
        Assert.Throws<InvalidDataException>(() => RecoveryArchiveTransport.Encode(original));
    }
}
