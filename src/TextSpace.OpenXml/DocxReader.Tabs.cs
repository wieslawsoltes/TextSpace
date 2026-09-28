using System.Collections.Immutable;
using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxReader
{
    private ImmutableArray<TabStop> ReadTabs(XElement? properties, ImmutableArray<TabStop> inherited)
    {
        var elements = properties?.Element(W + "tabs")?.Elements(W + "tab").ToArray();
        if (elements is null) return inherited;
        if (elements.Length > 128) throw new InvalidDataException("Too many paragraph tab stops.");
        var stops = inherited.ToDictionary(s => (long)Math.Round(s.Position * 20), s => s);
        foreach (var element in elements)
        {
            var position = Number((string?)element.Attribute(W + "pos"), double.NaN);
            if (!double.IsFinite(position) || Math.Abs(position) > 80000) throw new InvalidDataException("Invalid paragraph tab position.");
            var key = (long)Math.Round(position); var value = Val(element);
            if (value == "clear") { stops.Remove(key); continue; }
            var alignment = value switch { "left" or "start" => TabAlignment.Left, "center" => TabAlignment.Center,
                "right" or "end" => TabAlignment.Right, "decimal" => TabAlignment.Decimal, "bar" => TabAlignment.Bar, _ => (TabAlignment?)null };
            if (alignment is null) { Warn("An unsupported paragraph tab alignment was omitted."); continue; }
            var leader = (string?)element.Attribute(W + "leader") switch
            {
                null or "none" => TabLeader.None, "dot" => TabLeader.Dot, "hyphen" => TabLeader.Hyphen,
                "underscore" => TabLeader.Underscore, "heavy" => TabLeader.Heavy, "middleDot" => TabLeader.MiddleDot, _ => (TabLeader?)null
            };
            if (leader is null) Warn("An unsupported tab leader was normalized to no leader.");
            stops[key] = new() { Position = key / 20d, Alignment = alignment.Value, Leader = leader ?? TabLeader.None };
        }
        return [.. stops.OrderBy(p => p.Key).Select(p => p.Value)];
    }
}
