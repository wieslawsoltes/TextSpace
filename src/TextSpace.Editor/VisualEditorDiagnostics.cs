using TextSpace.Layout;

namespace TextSpace.Editor;

/// <summary>Read-only interaction geometry for accessibility adapters and acceptance tests.</summary>
public sealed record VisualEditorDiagnostics(string? SelectedObjectId, bool Cropping, bool GestureActive, bool EditorOpen,
    IReadOnlyList<VisualObjectDiagnostic> Objects, IReadOnlyList<VisualCellDiagnostic> Cells, IReadOnlyList<EquationSlotDiagnostic> EquationSlots);
public sealed record VisualObjectDiagnostic(string Id, string Kind, int Page, double X, double Y, double Width, double Height, double Rotation,
    bool Floating, string Text, IReadOnlyList<VisualHandleDiagnostic> Handles);
public sealed record VisualHandleDiagnostic(string Handle, double X, double Y);
public sealed record VisualCellDiagnostic(string TableId, int Page, int Row, int Column, int RowSpan, int ColumnSpan, double X, double Y, double Width, double Height, bool Replica);
public sealed record EquationSlotDiagnostic(string Id, string Role, string Text, double X, double Y, double Width, double Height, bool Active);
