using TextSpace.Core;
using TextSpace.Layout;

namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    public VisualEditorDiagnostics CaptureVisualDiagnostics(UIElement relativeTo)
    {
        var origin = _canvas.TransformToVisual(relativeTo).TransformPoint(new Point());
        Point Screen(int page, double x, double y) => new(origin.X + PaperLeft + (Layout.PageLeft(page) + x) * Scale,
            origin.Y + 18 + (Layout.PageTop(page) + y) * Scale - _scrollY);
        var objects = new List<VisualObjectDiagnostic>();
        foreach (var (page, item) in VisualObjects().Where(o => !o.Item.IsReplica).Take(2000))
        {
            var block = item.Object; var bounds = item.Bounds; var point = Screen(page, bounds.X, bounds.Y);
            var handles = new List<VisualHandleDiagnostic>();
            foreach (var h in VisualGeometry.Handles(bounds.Width, bounds.Height).Append((VisualHandle.Rotate, bounds.Width / 2, -24 / Scale)))
            {
                var delta = VisualGeometry.Rotate(h.X - bounds.Width / 2, h.Y - bounds.Height / 2, block.Placement.Rotation);
                var transformed = Screen(page, bounds.X + bounds.Width / 2 + delta.X, bounds.Y + bounds.Height / 2 + delta.Y);
                handles.Add(new(h.Handle.ToString(), transformed.X, transformed.Y));
            }
            var text = block switch { ShapeBlock shape => shape.Text, EquationBlock equation => equation.Root.ToLinearText(), ImageBlock image => image.AltText, _ => "" };
            objects.Add(new(block.Id, block is ShapeBlock s ? s.Kind.ToString() : block is EquationBlock ? "Equation" : "Picture", page,
                point.X, point.Y, bounds.Width * Scale, bounds.Height * Scale, block.Placement.Rotation, block.Placement.Floating, text, handles));
        }
        var cells = new List<VisualCellDiagnostic>();
        foreach (var page in Layout.Pages)
            foreach (var cell in page.Cells.Take(2000))
            {
                var p = Screen(page.Index, cell.Bounds.X, cell.Bounds.Y);
                cells.Add(new(cell.TableId, page.Index, cell.Row, cell.Column, cell.RowSpan, cell.ColumnSpan, p.X, p.Y, cell.Bounds.Width * Scale, cell.Bounds.Height * Scale, cell.IsReplica));
            }
        return new(_selectedObjectId, IsCropping, IsVisualGestureActive, IsObjectEditorOpen, objects, cells, _equationEditor?.CaptureSlotDiagnostics(relativeTo) ?? []);
    }
}
