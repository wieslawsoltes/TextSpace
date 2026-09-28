using System.Text.Json;
using TextSpace.Core;
using TextSpace.Editor;
using TextSpace.Editing;

namespace TextSpace.App;

internal static partial class BrowserDiagnostics
{
    private static void WriteTables(Utf8JsonWriter json, EditorSession session, DocumentSurface surface)
    {
        var firstLines = surface.Layout.Lines.GroupBy(l => l.ParagraphId).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        json.WriteStartArray("tableModels");
        void Walk(IEnumerable<Block> blocks, int depth)
        {
            foreach (var table in blocks.OfType<TableBlock>())
            {
                var grid = new TableGrid(table);
                json.WriteStartObject(); json.WriteString("id", table.Id); json.WriteNumber("depth", depth);
                json.WriteNumber("rows", grid.RowCount); json.WriteNumber("columns", grid.ColumnCount);
                json.WriteBoolean("repeatHeader", table.RepeatHeaderRow); json.WriteStartArray("regions");
                foreach (var region in grid.Regions.Take(200))
                {
                    var p = DocumentModel.Walk(region.Cell.Blocks).FirstOrDefault();
                    var line = p is null ? null : firstLines.GetValueOrDefault(p.Id);
                    json.WriteStartObject(); json.WriteNumber("row", region.Row); json.WriteNumber("column", region.Column);
                    json.WriteNumber("rowSpan", region.RowSpan); json.WriteNumber("columnSpan", region.ColumnSpan);
                    json.WriteString("alignment", region.Cell.VerticalAlignment.ToString());
                    json.WriteString("text", p?.Text ?? "");
                    if (line is not null)
                    {
                        json.WriteNumber("page", line.PageIndex); json.WriteNumber("x", line.X); json.WriteNumber("y", line.Y);
                        json.WriteNumber("height", line.Height);
                    }
                    json.WriteEndObject();
                }
                json.WriteEndArray(); json.WriteEndObject();
                foreach (var region in grid.Regions) Walk(region.Cell.Blocks, depth + 1);
            }
        }
        Walk(session.Document.Blocks, 0); json.WriteEndArray();
        json.WriteNumber("repeatedHeaderLines", surface.Layout.Pages.Sum(p => p.Lines.Count(l => l.IsReplica)));
    }
}
