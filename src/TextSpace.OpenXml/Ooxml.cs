using System.Globalization;
using System.Xml.Linq;

namespace TextSpace.OpenXml;

internal static class Ooxml
{
    internal static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    internal static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    internal static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    internal static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    internal static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    internal static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    internal static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    internal static XElement E(string name, params object?[] children) => new(W + name, children);
    internal static XAttribute V(object value) => new(W + "val", value);
    internal static string? Val(XElement? element) => (string?)element?.Attribute(W + "val");
    internal static int Twips(double points) => (int)Math.Round(points * 20);
    internal static double Number(string? value, double fallback = 0) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : fallback;
    internal static bool Flag(XElement? element, bool fallback) => element is null ? fallback : Val(element) is not ("0" or "false" or "off" or "none");
    internal static string Hex(string? value, string fallback = "202020") => value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit) ? value[1..].ToUpperInvariant() : fallback;
    internal static string RelationshipType(string kind) => R.NamespaceName + "/" + kind;
    internal static string? SafeLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.StartsWith('#')) return value;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" ? value : null;
    }
}
