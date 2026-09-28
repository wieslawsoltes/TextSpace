using System.Text.Json;
using TextSpace.Editing;
using TextSpace.Editor;

namespace TextSpace.App;

internal static partial class BrowserDiagnostics
{
    private static void WriteTypography(Utf8JsonWriter json, EditorSession session, DocumentSurface surface)
    {
        var paragraph = session.CurrentParagraph; var format = paragraph.Format;
        json.WriteStartObject("typography"); json.WriteString("paragraphId", paragraph.Id);
        json.WriteNumber("defaultTabStop", session.Document.DefaultTabStop);
        json.WriteBoolean("keepWithNext", format.KeepWithNext); json.WriteBoolean("keepLinesTogether", format.KeepLinesTogether);
        json.WriteBoolean("widowControl", format.WidowControl); json.WriteBoolean("pageBreakBefore", format.PageBreakBefore);
        json.WriteStartArray("tabStops");
        foreach (var stop in format.TabStops)
        {
            json.WriteStartObject(); json.WriteNumber("position", stop.Position); json.WriteBoolean("relative", stop.RelativeToRightEdge);
            json.WriteString("alignment", stop.Alignment.ToString()); json.WriteString("leader", stop.Leader.ToString()); json.WriteEndObject();
        }
        json.WriteEndArray(); json.WriteStartArray("lines");
        foreach (var line in surface.Layout.Lines.Where(l => l.ParagraphId == paragraph.Id).Take(100))
        {
            json.WriteStartObject(); json.WriteNumber("page", line.PageIndex); json.WriteNumber("x", line.X); json.WriteNumber("y", line.Y);
            json.WriteNumber("width", line.Width); json.WriteNumber("start", line.Start); json.WriteNumber("end", line.End);
            json.WriteStartArray("chunks");
            foreach (var chunk in line.Chunks)
            {
                json.WriteStartObject(); json.WriteString("text", chunk.Text); json.WriteString("display", chunk.DisplayText ?? chunk.Text);
                json.WriteNumber("x", chunk.X); json.WriteNumber("width", chunk.Width); json.WriteString("leader", chunk.TabLeader.ToString()); json.WriteEndObject();
            }
            json.WriteEndArray(); json.WriteEndObject();
        }
        json.WriteEndArray(); json.WriteNumber("cachedGlyphRuns", surface.Renderer.Metrics.CachedShapedRuns);
        json.WriteNumber("glyphCacheHits", surface.Renderer.Metrics.ShapedCacheHits); json.WriteEndObject();
    }
}
