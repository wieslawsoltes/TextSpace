using Microsoft.UI.Input;
using SkiaSharp;
using TextSpace.Core;
using TextSpace.Editing;
using TextSpace.Layout;

namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    private sealed class VisualGesture(VisualEditDraft draft, LayoutObject item, int page, VisualHandle handle, Point start)
    {
        public VisualEditDraft Draft = draft;
        public LayoutObject Item = item;
        public int Page = page;
        public VisualHandle Handle = handle;
        public Point Start = start;
        public RectD Bounds = item.Bounds;
        public bool Moved;
        public double? GuideX, GuideY;
    }
    private VisualGesture? _visualGesture;
    private bool _cropMode;
    private string? _selectedObjectId;
    public string? SelectedObjectId => _selectedObjectId;
    public VisualBlock? SelectedObject => Session.FindVisual(_selectedObjectId);
    public bool IsCropping => _cropMode && SelectedObject is ImageBlock;
    public bool IsVisualGestureActive => _visualGesture is not null || _tableGesture is not null;
    public event Action? ObjectSelectionChanged;

    public IEnumerable<(int Page, LayoutObject Item)> VisualObjects()
    {
        foreach (var page in Layout.Pages)
        {
            IEnumerable<LayoutObject> objects = page.Objects.Count > 0 ? page.Objects : page.Images.Select(i => new LayoutObject(i.Image, i.Bounds) { IsReplica = i.IsReplica });
            foreach (var item in objects) yield return (page.Index, item);
        }
    }
    public void SelectObject(string? id, bool reveal = false)
    {
        if (id is not null && Session.FindVisual(id) is null) throw new InvalidOperationException("The object no longer exists.");
        if (_selectedObjectId != id) { CancelObjectEditor(); CancelVisualGesture(); _cropMode = false; }
        _selectedObjectId = id; SelectedImageId = SelectedObject is ImageBlock ? id : null;
        if (id is not null) SelectedCells = null;
        if (reveal && SelectedPlacement() is { } placement)
        {
            var top = 18 + (Layout.PageTop(placement.Page) + placement.Item.Bounds.Y) * Scale;
            if (top < _scrollY + 12 || top + placement.Item.Bounds.Height * Scale > _scrollY + ViewportHeight - 12)
                SetScroll(_scrollX, Math.Max(0, top - 30));
        }
        SyncInput(); Invalidate(); ViewChanged?.Invoke(); ObjectSelectionChanged?.Invoke();
    }
    public void ToggleCrop()
    {
        if (Session.IsReadOnly || SelectedObject is not ImageBlock) return;
        _cropMode = !_cropMode; Invalidate(); ObjectSelectionChanged?.Invoke(); FocusEditor();
    }
    private (int Page, LayoutObject Item)? SelectedPlacement()
    {
        foreach (var item in VisualObjects()) if (!item.Item.IsReplica && item.Item.Object.Id == _selectedObjectId) return item;
        return null;
    }
    private Point PagePoint(Point point, int page) => new((point.X - PaperLeft) / Scale - Layout.PageLeft(page),
        (point.Y - 18 + _scrollY) / Scale - Layout.PageTop(page));
    private bool ContainsVisual(LayoutObject item, Point point)
    {
        var local = VisualGeometry.Local(item.Bounds, point.X, point.Y, item.Object.Placement.Rotation);
        return local.X >= 0 && local.Y >= 0 && local.X <= item.Bounds.Width && local.Y <= item.Bounds.Height;
    }
    private VisualHandle HandleAt(LayoutObject item, Point point, double tolerance)
    {
        var p = VisualGeometry.Local(item.Bounds, point.X, point.Y, item.Object.Placement.Rotation);
        if (!IsCropping && Math.Abs(p.X - item.Bounds.Width / 2) <= tolerance && Math.Abs(p.Y + 24 / Scale) <= tolerance) return VisualHandle.Rotate;
        foreach (var handle in VisualGeometry.Handles(item.Bounds.Width, item.Bounds.Height))
            if (Math.Abs(p.X - handle.X) <= tolerance && Math.Abs(p.Y - handle.Y) <= tolerance) return handle.Handle;
        return p.X >= 0 && p.Y >= 0 && p.X <= item.Bounds.Width && p.Y <= item.Bounds.Height ? VisualHandle.Move : VisualHandle.None;
    }
    private bool TryVisualPressed(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas); var position = point.Position;
        var right = point.Properties.IsRightButtonPressed;
        if (!right && !point.Properties.IsLeftButtonPressed && point.PointerDeviceType != PointerDeviceType.Touch) return false;
        if (_objectEditor is not null) { e.Handled = true; return true; }
        if (!right && !Session.IsReadOnly && SelectedPlacement() is { } selected)
        {
            var p = PagePoint(position, selected.Page); var handle = HandleAt(selected.Item, p, (point.PointerDeviceType == PointerDeviceType.Touch ? 12 : 6) / Scale);
            if (handle != VisualHandle.None)
            {
                BeginVisualGesture(selected, handle, p, e); return true;
            }
        }
        var page = Layout.PageAtY((position.Y - 18 + _scrollY) / Scale); var pagePoint = PagePoint(position, page);
        var items = VisualObjects().Where(i => i.Page == page && !i.Item.IsReplica).ToArray();
        var hit = items.Reverse().FirstOrDefault(i => i.Item.Object.Placement.Floating && ContainsVisual(i.Item, pagePoint));
        if (hit.Item is null) hit = items.Reverse().FirstOrDefault(i => ContainsVisual(i.Item, pagePoint));
        if (hit.Item is null) return false;
        SelectObject(hit.Item.Object.Id);
        if (right) ContextRequested?.Invoke(position);
        else if (!Session.IsReadOnly) BeginVisualGesture(hit, VisualHandle.Move, pagePoint, e);
        else FocusEditor();
        e.Handled = true; return true;
    }
    private void BeginVisualGesture((int Page, LayoutObject Item) selected, VisualHandle handle, Point start, PointerRoutedEventArgs e)
    {
        CancelVisualGesture(); _dragging = false;
        _visualGesture = new(new VisualEditDraft(Session, selected.Item.Object.Id), selected.Item, selected.Page, handle, start);
        _canvas.CapturePointer(e.Pointer); FocusEditor(); e.Handled = true;
    }
    private bool TryVisualMoved(PointerRoutedEventArgs e)
    {
        if (_visualGesture is not { } gesture) return false;
        if (!gesture.Draft.IsCurrent) { CancelVisualGesture(); e.Handled = true; return true; }
        var point = PagePoint(e.GetCurrentPoint(_canvas).Position, gesture.Page); var dx = point.X - gesture.Start.X; var dy = point.Y - gesture.Start.Y;
        if (!gesture.Moved && Math.Abs(dx) * Scale + Math.Abs(dy) * Scale < 4) return true;
        gesture.Moved = true; var original = gesture.Item.Object; var placement = original.Placement; var value = gesture.Draft.Value; var bounds = gesture.Item.Bounds;
        var alt = KeyDown(VirtualKey.Menu); var shift = KeyDown(VirtualKey.Shift);
        var sx = bounds.Width / original.Width; var sy = bounds.Height / original.Height;
        gesture.GuideX = gesture.GuideY = null;
        if (IsCropping && value is ImageBlock image && original is ImageBlock source && gesture.Handle is not (VisualHandle.Move or VisualHandle.Rotate))
        {
            var delta = VisualGeometry.Rotate(dx, dy, -placement.Rotation); var crop = source.Crop;
            var left = crop.Left; var top = crop.Top; var right = crop.Right; var bottom = crop.Bottom;
            var horizontal = delta.X / bounds.Width * (1 - left - right); var vertical = delta.Y / bounds.Height * (1 - top - bottom);
            if (gesture.Handle is VisualHandle.NorthWest or VisualHandle.West or VisualHandle.SouthWest) left = Math.Clamp(left + horizontal, 0, 0.98 - right);
            if (gesture.Handle is VisualHandle.NorthEast or VisualHandle.East or VisualHandle.SouthEast) right = Math.Clamp(right - horizontal, 0, 0.98 - left);
            if (gesture.Handle is VisualHandle.NorthWest or VisualHandle.North or VisualHandle.NorthEast) top = Math.Clamp(top + vertical, 0, 0.98 - bottom);
            if (gesture.Handle is VisualHandle.SouthWest or VisualHandle.South or VisualHandle.SouthEast) bottom = Math.Clamp(bottom - vertical, 0, 0.98 - top);
            image.Crop = new() { Left = left, Top = top, Right = right, Bottom = bottom };
        }
        else if (gesture.Handle == VisualHandle.Move)
        {
            if (shift) { if (Math.Abs(dx) >= Math.Abs(dy)) dy = 0; else dx = 0; }
            if (!alt)
            {
                var settings = Layout.Pages[gesture.Page].Settings;
                foreach (var guide in new[] { settings.MarginLeft, settings.Width / 2, settings.Width - settings.MarginRight })
                    foreach (var edge in new[] { bounds.X + dx, bounds.X + dx + bounds.Width / 2, bounds.Right + dx })
                        if (Math.Abs(edge - guide) < 5 / Scale) { dx += guide - edge; gesture.GuideX = guide; break; }
            }
            value.Placement = placement with { X = Math.Clamp(placement.X + dx, -4000, 4000), Y = Math.Clamp(placement.Y + dy, IsInTable(value.Id) ? 0 : -4000, 4000) };
            gesture.Bounds = bounds with { X = bounds.X + value.Placement.X - placement.X, Y = bounds.Y + value.Placement.Y - placement.Y };
        }
        else if (gesture.Handle == VisualHandle.Rotate)
        {
            var angle = Math.Atan2(point.Y - bounds.Y - bounds.Height / 2, point.X - bounds.X - bounds.Width / 2) * 180 / Math.PI + 90;
            if (shift) angle = Math.Round(angle / 15) * 15;
            value.Placement = placement with { Rotation = angle };
        }
        else
        {
            var delta = VisualGeometry.Rotate(dx, dy, -placement.Rotation);
            var resized = VisualGeometry.Resize(original.Width, original.Height, gesture.Handle, delta.X / sx, delta.Y / sy, shift || original is ImageBlock && !alt, centered: ControlDown());
            value.Width = resized.Width; value.Height = resized.Height;
            var w = resized.Width * sx; var h = resized.Height * sy;
            var center = VisualGeometry.Rotate((w - bounds.Width) / 2 + resized.OffsetX * sx, (h - bounds.Height) / 2 + resized.OffsetY * sy, placement.Rotation);
            var x = bounds.X + bounds.Width / 2 + center.X - w / 2; var y = bounds.Y + bounds.Height / 2 + center.Y - h / 2;
            var alignmentCorrection = original.Alignment == TextSpace.Core.TextAlignment.Center ? (w - bounds.Width) / 2 : original.Alignment == TextSpace.Core.TextAlignment.Right ? w - bounds.Width : 0;
            value.Placement = placement with { X = Math.Clamp(placement.X + x - bounds.X + alignmentCorrection, -4000, 4000), Y = Math.Clamp(placement.Y + y - bounds.Y, IsInTable(value.Id) ? 0 : -4000, 4000) };
            gesture.Bounds = new(x, y, w, h);
        }
        Invalidate(); e.Handled = true; return true;
    }
    public bool IsInTable(string id)
    {
        var location = BlockTree.Find(Session.Document.Blocks, id);
        return location is { } found && !ReferenceEquals(found.Container, Session.Document.Blocks);
    }
    private bool CompleteVisualGesture()
    {
        if (_visualGesture is not { } gesture) return false; _visualGesture = null;
        try { if (gesture.Moved) gesture.Draft.Commit(IsCropping ? "Crop picture" : gesture.Handle == VisualHandle.Move ? "Move object" : gesture.Handle == VisualHandle.Rotate ? "Rotate object" : "Resize object"); }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
        finally { gesture.Draft.Dispose(); _canvas.ReleasePointerCaptures(); Invalidate(); }
        return true;
    }
    private void CancelVisualGesture()
    {
        var gesture = _visualGesture; _visualGesture = null; gesture?.Draft.Dispose();
        if (gesture is not null) { _canvas.ReleasePointerCaptures(); Invalidate(); }
    }
    private bool TryVisualDoubleTap(Point point)
    {
        var page = Layout.PageAtY((point.Y - 18 + _scrollY) / Scale); var p = PagePoint(point, page);
        var item = VisualObjects().Reverse().FirstOrDefault(i => i.Page == page && !i.Item.IsReplica && ContainsVisual(i.Item, p));
        if (item.Item is null) return false;
        CancelVisualGesture(); SelectObject(item.Item.Object.Id);
        if (item.Item.Object is ShapeBlock or EquationBlock) BeginObjectEditor(); else ToggleCrop(); return true;
    }
    private bool TryVisualKey(KeyRoutedEventArgs e)
    {
        if (_objectEditor is not null) { e.Handled = true; return true; }
        if (e.Key == VirtualKey.Escape && (_visualGesture is not null || _tableGesture is not null))
        { CancelVisualGesture(); CancelTableGesture(); e.Handled = true; return true; }
        if (_selectedObjectId is null) return false;
        var id = _selectedObjectId; var control = ControlDown(); var shift = KeyDown(VirtualKey.Shift);
        try
        {
            if (e.Key == VirtualKey.Escape) { SelectObject(null); FocusEditor(); }
            else if (e.Key is VirtualKey.Enter or VirtualKey.F2) BeginObjectEditor();
            else if (control && e.Key == VirtualKey.D) { SelectObject(Session.DuplicateVisual(id), true); }
            else if (e.Key is VirtualKey.Delete or VirtualKey.Back) { Session.DeleteVisual(id); SelectObject(null); }
            else if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)
            {
                var delta = shift ? 10d : 1d; var dx = e.Key == VirtualKey.Left ? -delta : e.Key == VirtualKey.Right ? delta : 0;
                var dy = e.Key == VirtualKey.Up ? -delta : e.Key == VirtualKey.Down ? delta : 0;
                Session.EditVisual(id, control ? "Resize object" : "Nudge object", b =>
                {
                    if (control) { b.Width = Math.Clamp(b.Width + dx, 6, 4000); b.Height = Math.Clamp(b.Height + dy, 6, 4000); }
                    else b.Placement = b.Placement with { X = Math.Clamp(b.Placement.X + dx, -4000, 4000), Y = Math.Clamp(b.Placement.Y + dy, IsInTable(id) ? 0 : -4000, 4000) };
                });
            }
            else return false;
        }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
        e.Handled = true; return true;
    }
    private void DrawVisualInteraction(SKCanvas canvas, int page)
    {
        DrawTableInteraction(canvas, page);
        var selected = SelectedPlacement(); if (selected is null || selected.Value.Page != page) return;
        var item = selected.Value.Item; var block = item.Object; var bounds = item.Bounds;
        if (_visualGesture is { } gesture)
        {
            block = gesture.Draft.Value; bounds = gesture.Bounds; Renderer.InvalidateVisualLayout(block.Id); Renderer.DrawVisualBlock(canvas, block, bounds);
            using var guide = new SKPaint { Color = SKColor.Parse("#D347B7"), StrokeWidth = (float)(1 / Scale), IsAntialias = true };
            if (gesture.GuideX is { } x) canvas.DrawLine((float)x, 0, (float)x, (float)Layout.Pages[page].Settings.Height, guide);
        }
        canvas.Save(); canvas.Translate((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y + bounds.Height / 2)); canvas.RotateDegrees((float)block.Placement.Rotation);
        canvas.Translate((float)(-bounds.Width / 2), (float)(-bounds.Height / 2));
        using var paint = new SKPaint { Color = SKColor.Parse(IsCropping ? "#202020" : "#185ABD"), Style = SKPaintStyle.Stroke, StrokeWidth = (float)(1 / Scale), IsAntialias = true };
        canvas.DrawRect(SKRect.Create((float)bounds.Width, (float)bounds.Height), paint);
        if (!Session.IsReadOnly)
        {
            var size = (float)(7 / Scale);
            foreach (var handle in VisualGeometry.Handles(bounds.Width, bounds.Height))
            {
                var rectangle = SKRect.Create((float)handle.X - size / 2, (float)handle.Y - size / 2, size, size);
                paint.Style = SKPaintStyle.Fill; paint.Color = SKColors.White; canvas.DrawRect(rectangle, paint);
                paint.Style = SKPaintStyle.Stroke; paint.Color = SKColor.Parse(IsCropping ? "#202020" : "#185ABD"); canvas.DrawRect(rectangle, paint);
            }
            if (!IsCropping)
            {
                var x = (float)bounds.Width / 2; var y = (float)(-24 / Scale); canvas.DrawLine(x, 0, x, y, paint);
                paint.Style = SKPaintStyle.Fill; paint.Color = SKColors.White; canvas.DrawCircle(x, y, (float)(4 / Scale), paint);
                paint.Style = SKPaintStyle.Stroke; paint.Color = SKColor.Parse("#185ABD"); canvas.DrawCircle(x, y, (float)(4 / Scale), paint);
            }
        }
        canvas.Restore();
    }
    private void RefreshVisualState(EditorChangedEventArgs e)
    {
        if (e.Kind != EditorChangeKind.Document) return;
        CancelVisualGesture(); CancelTableGesture();
        if (_objectEditor is not null) CancelObjectEditor();
        if (_selectedObjectId is not null && Session.FindVisual(_selectedObjectId) is null) { _selectedObjectId = null; SelectedImageId = null; _cropMode = false; }
        if (SelectedCells is { } cells && (Session.FindTable(cells.TableId) is not { } table || cells.RowEnd > table.Rows.Count || cells.ColumnEnd > table.Rows[0].Cells.Count)) SelectedCells = null;
        ObjectSelectionChanged?.Invoke();
    }
}
