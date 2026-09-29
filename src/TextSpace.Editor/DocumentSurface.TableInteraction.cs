using SkiaSharp;
using TextSpace.Core;
using TextSpace.Editing;
using TextSpace.Layout;

namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    private sealed class TableGesture(EditorSession session, LayoutCell cell, int page, Point start, int boundary, bool selecting)
    {
        public readonly DocumentModel Document = session.Document;
        public readonly long Revision = session.Revision;
        public readonly LayoutCell Cell = cell;
        public readonly int Page = page, Boundary = boundary;
        public readonly Point Start = start;
        public readonly bool Selecting = selecting;
        public double[] Widths = [];
        public double Delta;
        public bool Moved;
    }
    private TableGesture? _tableGesture;
    public TableSelection? SelectedCells { get; private set; }

    public void SelectTableCells(string mode)
    {
        var table = Session.CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var current = Session.CurrentCell; var grid = new TableGrid(table); SelectObject(null);
        SelectedCells = mode switch
        {
            "row" => Session.SelectTableRectangle(table.Id, current.Row, 0, current.RowEnd - 1, grid.ColumnCount - 1),
            "column" => Session.SelectTableRectangle(table.Id, 0, current.Column, grid.RowCount - 1, current.ColumnEnd - 1),
            "table" => Session.SelectTableRectangle(table.Id, 0, 0, grid.RowCount - 1, grid.ColumnCount - 1),
            _ => Session.SelectTableRectangle(table.Id, current.Row, current.Column, current.RowEnd - 1, current.ColumnEnd - 1)
        };
        Invalidate(); ObjectSelectionChanged?.Invoke(); FocusEditor();
    }
    public void ClearCellSelection() { SelectedCells = null; Invalidate(); }
    public void MergeSelectedCells()
    {
        if (SelectedCells is not { } cells) throw new InvalidOperationException("Select a cell rectangle first.");
        Session.MergeTableSelection(cells); SelectedCells = null; Invalidate();
    }
    private bool TryTablePressed(PointerRoutedEventArgs e)
    {
        if (_objectEditor is not null) { e.Handled = true; return true; }
        var point = e.GetCurrentPoint(_canvas);
        if (!point.Properties.IsLeftButtonPressed && point.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch) return false;
        var page = Layout.PageAtY((point.Position.Y - 18 + _scrollY) / Scale); var p = PagePoint(point.Position, page);
        var tolerance = (point.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch ? 9 : 4) / Scale;
        var cells = Layout.Pages[page].Cells.Where(c => !c.IsReplica).Reverse();
        if (KeyDown(VirtualKey.Menu))
        {
            var cell = cells.FirstOrDefault(c => c.Bounds.Contains(p.X, p.Y)); if (cell is null) return false;
            SelectObject(null); SelectedCells = Session.SelectTableRectangle(cell.TableId, cell.Row, cell.Column, cell.Row, cell.Column);
            _tableGesture = new(Session, cell, page, p, -1, true); _canvas.CapturePointer(e.Pointer); FocusEditor(); Invalidate(); e.Handled = true; return true;
        }
        if (Session.IsReadOnly) return false;
        foreach (var cell in cells)
        {
            var table = Session.FindTable(cell.TableId); if (table is null) continue;
            var boundary = cell.Column + cell.ColumnSpan;
            var columnEdge = boundary < table.Rows[0].Cells.Count && Math.Abs(p.X - cell.Bounds.Right) <= tolerance && p.Y >= cell.Bounds.Y && p.Y <= cell.Bounds.Bottom;
            var rowEdge = cell.RowSpan == 1 && cell.DrawTop && cell.DrawBottom && Math.Abs(p.Y - cell.Bounds.Bottom) <= tolerance && p.X >= cell.Bounds.X && p.X <= cell.Bounds.Right;
            if (!columnEdge && !rowEdge) continue;
            SelectObject(null); SelectedCells = null;
            var gesture = new TableGesture(Session, cell, page, p, columnEdge ? boundary : -1, false);
            if (columnEdge) gesture.Widths = EditorSession.TableColumnWidths(table, cell.TableWidth);
            _tableGesture = gesture; _canvas.CapturePointer(e.Pointer); FocusEditor(); e.Handled = true; return true;
        }
        return false;
    }
    private bool TryTableMoved(PointerRoutedEventArgs e)
    {
        if (_tableGesture is not { } gesture) return false;
        if (!ReferenceEquals(gesture.Document, Session.Document) || gesture.Revision != Session.Revision || Session.IsReadOnly && !gesture.Selecting)
        { CancelTableGesture(); return true; }
        var point = e.GetCurrentPoint(_canvas).Position; var page = Layout.PageAtY((point.Y - 18 + _scrollY) / Scale); var p = PagePoint(point, page);
        if (gesture.Selecting)
        {
            var cell = Layout.Pages[page].Cells.LastOrDefault(c => !c.IsReplica && c.TableId == gesture.Cell.TableId && c.Bounds.Contains(p.X, p.Y));
            if (cell is not null) SelectedCells = Session.SelectTableRectangle(cell.TableId, gesture.Cell.Row, gesture.Cell.Column, cell.Row, cell.Column);
        }
        else
        {
            p = PagePoint(point, gesture.Page); gesture.Delta = gesture.Boundary >= 0 ? p.X - gesture.Start.X : p.Y - gesture.Start.Y;
            gesture.Moved |= Math.Abs(gesture.Delta) * Scale >= 3;
        }
        Invalidate(); e.Handled = true; return true;
    }
    private bool CompleteTableGesture()
    {
        if (_tableGesture is not { } gesture) return false; _tableGesture = null;
        try
        {
            if (gesture.Moved && !gesture.Selecting)
            {
                if (!ReferenceEquals(gesture.Document, Session.Document) || gesture.Revision != Session.Revision) throw new InvalidOperationException("The table changed during the gesture.");
                if (gesture.Boundary >= 0) Session.SetTableColumnWidths(gesture.Cell.TableId, EditorSession.ResizeTableBoundary(gesture.Widths, gesture.Boundary, gesture.Delta));
                else Session.SetTableRowHeight(gesture.Cell.TableId, gesture.Cell.Row, Math.Clamp(gesture.Cell.LogicalHeight + gesture.Delta, 0, 4000));
            }
        }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
        finally { _canvas.ReleasePointerCaptures(); Invalidate(); ObjectSelectionChanged?.Invoke(); }
        return true;
    }
    private void CancelTableGesture()
    {
        var had = _tableGesture is not null; _tableGesture = null;
        if (had) { _canvas.ReleasePointerCaptures(); Invalidate(); }
    }
    private void DrawTableInteraction(SKCanvas canvas, int page)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(24, 90, 189, 55) };
        if (SelectedCells is { } selection)
            foreach (var cell in Layout.Pages[page].Cells)
                if (!cell.IsReplica && cell.TableId == selection.TableId && selection.Intersects(cell.Row, cell.Column, cell.RowSpan, cell.ColumnSpan))
                    canvas.DrawRect(SKRect.Create((float)cell.Bounds.X, (float)cell.Bounds.Y, (float)cell.Bounds.Width, (float)cell.Bounds.Height), paint);
        if (_tableGesture is not { Selecting: false } gesture || gesture.Page != page) return;
        paint.Style = SKPaintStyle.Stroke; paint.Color = SKColor.Parse("#185ABD"); paint.StrokeWidth = (float)(1.5 / Scale);
        var settings = Layout.Pages[page].Settings;
        if (gesture.Boundary >= 0)
        {
            var widths = EditorSession.ResizeTableBoundary(gesture.Widths, gesture.Boundary, gesture.Delta);
            var x = gesture.Cell.TableLeft + widths.Take(gesture.Boundary).Sum();
            canvas.DrawLine((float)x, (float)settings.MarginTop, (float)x, (float)(settings.Height - settings.MarginBottom), paint);
        }
        else
        {
            var y = gesture.Cell.Bounds.Y + Math.Clamp(gesture.Cell.LogicalHeight + gesture.Delta, 0, 4000);
            canvas.DrawLine((float)gesture.Cell.TableLeft, (float)y, (float)(gesture.Cell.TableLeft + gesture.Cell.TableWidth), (float)y, paint);
        }
    }
}
