using System.Text.Json;
using TextSpace.Workbench;

namespace TextSpace.App;

internal static partial class BrowserDiagnostics
{
    private static void WriteVisuals(Utf8JsonWriter json, WordWorkbench workbench)
    {
        var state = workbench.Surface.CaptureVisualDiagnostics(workbench);
        json.WriteStartObject("visuals"); json.WriteString("selected", state.SelectedObjectId);
        json.WriteBoolean("cropping", state.Cropping); json.WriteBoolean("gesture", state.GestureActive); json.WriteBoolean("editor", state.EditorOpen);
        json.WriteStartArray("objects");
        foreach (var item in state.Objects)
        {
            json.WriteStartObject(); json.WriteString("id", item.Id); json.WriteString("kind", item.Kind); json.WriteNumber("page", item.Page);
            json.WriteNumber("x", item.X); json.WriteNumber("y", item.Y); json.WriteNumber("width", item.Width); json.WriteNumber("height", item.Height);
            json.WriteNumber("rotation", item.Rotation); json.WriteBoolean("floating", item.Floating); json.WriteString("text", item.Text);
            json.WriteStartArray("handles"); foreach (var handle in item.Handles) { json.WriteStartObject(); json.WriteString("handle", handle.Handle); json.WriteNumber("x", handle.X); json.WriteNumber("y", handle.Y); json.WriteEndObject(); }
            json.WriteEndArray(); json.WriteEndObject();
        }
        json.WriteEndArray(); json.WriteStartArray("cells");
        foreach (var cell in state.Cells)
        {
            json.WriteStartObject(); json.WriteString("tableId", cell.TableId); json.WriteNumber("page", cell.Page); json.WriteNumber("row", cell.Row); json.WriteNumber("column", cell.Column);
            json.WriteNumber("rowSpan", cell.RowSpan); json.WriteNumber("columnSpan", cell.ColumnSpan); json.WriteNumber("x", cell.X); json.WriteNumber("y", cell.Y);
            json.WriteNumber("width", cell.Width); json.WriteNumber("height", cell.Height); json.WriteBoolean("replica", cell.Replica); json.WriteEndObject();
        }
        json.WriteEndArray(); json.WriteStartArray("slots");
        foreach (var slot in state.EquationSlots)
        {
            json.WriteStartObject(); json.WriteString("id", slot.Id); json.WriteString("role", slot.Role); json.WriteString("text", slot.Text);
            json.WriteNumber("x", slot.X); json.WriteNumber("y", slot.Y); json.WriteNumber("width", slot.Width); json.WriteNumber("height", slot.Height); json.WriteBoolean("active", slot.Active); json.WriteEndObject();
        }
        json.WriteEndArray(); json.WriteEndObject();
    }
}
