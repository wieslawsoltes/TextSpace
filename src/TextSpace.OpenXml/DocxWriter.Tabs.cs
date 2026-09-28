using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxWriter
{
    private static XElement? WriteTabs(ParagraphFormat format, double width)
    {
        if (format.TabStops.IsEmpty) return null;
        var tabs = E("tabs");
        foreach (var stop in format.TabStops.OrderBy(t => t.Resolve(width, format.RightIndent)))
        {
            var alignment = stop.Alignment.ToString().ToLowerInvariant();
            var leader = stop.Leader == TabLeader.MiddleDot ? "middleDot" : stop.Leader.ToString().ToLowerInvariant();
            tabs.Add(E("tab", V(alignment), stop.Leader == TabLeader.None ? null : new XAttribute(W + "leader", leader),
                new XAttribute(W + "pos", Twips(stop.Resolve(width, format.RightIndent)))));
        }
        return tabs;
    }
}
