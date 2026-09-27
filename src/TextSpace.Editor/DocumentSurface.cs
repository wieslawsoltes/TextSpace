using Microsoft.UI.Input;
using SkiaSharp;
using TextSpace.Core;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.UI.Core;

namespace TextSpace.Editor;

/// <summary>Real Skia-rendered paper with a native Uno text-input bridge for typing, clipboard and IME.</summary>
public sealed partial class DocumentSurface : UserControl, IDisposable
{
    private sealed class DrawingCanvas : SKCanvasElement
    {
        public Action<SKCanvas, Size>? Draw { get; set; }
        protected override void RenderOverride(SKCanvas canvas, Size area) => Draw?.Invoke(canvas, area);
    }
    private readonly DrawingCanvas _canvas = new();
    private readonly Grid _viewport = new();
    private readonly TextBox _input;
    private readonly PageRuler _ruler = new();
    private readonly OfficeScrollBar _vertical = new(), _horizontal = new(false);
    private readonly DispatcherTimer _caretTimer = new() { Interval = TimeSpan.FromMilliseconds(520) };
    private bool _syncing, _nativeEdit, _dragging, _caretVisible = true, _disposed;
    private int _anchor;
    private double _zoom = 1, _scrollX, _scrollY;
    private double? _desiredX;
    private string _documentId;
    private Point _lastPointer;
    public bool IsProcessingNativeInput => _nativeEdit;
    public EditorSession Session { get; }
    public DocumentRenderer Renderer { get; } = new();
    public DocumentLayout Layout { get; private set; }
    public double Zoom => _zoom;
    public new double Scale => _zoom * 4d / 3;
    public double ScrollY => _scrollY;
    public double ScrollX => _scrollX;
    public double PaperLeft => Math.Max(34, (_canvas.ActualWidth - Layout.Width * Scale) / 2) - _scrollX;
    public double ViewportWidth => _canvas.ActualWidth;
    public double ViewportHeight => _canvas.ActualHeight;
    public bool ShowFormatting { get; set; }
    public bool ShowBoundaries { get; set; }
    public bool ShowComments { get; set; } = true;
    public bool ShowChanges { get; set; } = true;
    public bool ShowRuler { get => _ruler.Visibility == Visibility.Visible; set { _ruler.Visibility = value ? Visibility.Visible : Visibility.Collapsed; Invalidate(); } }
    public string? SelectedImageId { get; private set; }
    public event Action<string>? CommandRequested;
    public event Action<string>? Error;
    public event Action? ViewChanged;
    public new event Action<Point>? ContextRequested;

    public DocumentSurface(EditorSession session)
    {
        Session = session; _documentId = session.Document.Id; Layout = Renderer.Layout(session.Document);
        _input = OfficeTheme.Field("Document text"); _input.AcceptsReturn = true; _input.TextWrapping = TextWrapping.NoWrap; _input.Width = 2; _input.Height = 24; _input.MinHeight = 0; _input.Padding = new(0); _input.BorderThickness = new(0); _input.Opacity = 0.01; _input.HorizontalAlignment = HorizontalAlignment.Left; _input.VerticalAlignment = VerticalAlignment.Top; _input.IsSpellCheckEnabled = false;
        // Capture the model synchronously, but never mutate the visual/input tree
        // until TextBox has finished its own text and selection update.
        _input.TextChanging += (_, _) => CommitNativeInput();
        _input.TextChanged += OnNativeTextChanged; _input.SelectionChanged += OnNativeSelectionChanged; _input.PreviewKeyDown += OnInputKeyDown; _input.PreviewKeyUp += OnInputKeyUp;
        _input.BeforeTextChanging += (_, e) =>
        {
            if (_ownsNativeInput && !_syncing) e.Cancel = true;
        };
        _input.GotFocus += (_, _) => { _caretVisible = true; _caretTimer.Start(); Invalidate(); }; _input.LostFocus += (_, _) => { _caretVisible = false; _caretTimer.Stop(); Invalidate(); };
        _viewport.Children.Add(_canvas); _viewport.Children.Add(_input);
        var paper = OfficeTheme.Columns((_viewport, -1), (_vertical, 16));
        var bottom = OfficeTheme.Columns((_horizontal, -1), (new Border { Background = OfficeTheme.Brush("#F2F2F2") }, 16));
        Content = OfficeTheme.Rows((_ruler, 0), (paper, -1), (bottom, 16)); Background = OfficeTheme.Brush("#E8E8E8");
        _canvas.Draw = Draw; _canvas.SizeChanged += (_, _) => { ClampScroll(); UpdateRuler(); UpdateInputPosition(); Invalidate(); ViewChanged?.Invoke(); };
        _canvas.PointerPressed += OnPointerPressed; _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, e) => { _dragging = false; _canvas.ReleasePointerCaptures(); e.Handled = true; };
        _canvas.PointerCaptureLost += (_, _) => _dragging = false;
        _canvas.DoubleTapped += (_, e) => { Try(() => Session.SelectWord(HitTest(e.GetPosition(_canvas)))); FocusEditor(); e.Handled = true; };
        _canvas.PointerWheelChanged += (_, e) =>
        {
            var point = e.GetCurrentPoint(_canvas); var delta = point.Properties.MouseWheelDelta;
            if (ControlDown()) SetZoom(_zoom * Math.Pow(1.12, delta / 120d), point.Position);
            else if (KeyDown(VirtualKey.Shift) || point.Properties.IsHorizontalMouseWheel) SetScroll(_scrollX - delta, _scrollY);
            else SetScroll(_scrollX, _scrollY - delta * 0.9); e.Handled = true;
        };
        _vertical.ScrollRequested += value => SetScroll(_scrollX, value); _horizontal.ScrollRequested += value => SetScroll(value, _scrollY);
        _ruler.IndentChanged += (handle, value) => Try(() => Session.FormatParagraph("Ruler indent", p => handle switch { "right" => p with { RightIndent = value }, "first" => p with { FirstLineIndent = value }, _ => p with { LeftIndent = value } }));
        _caretTimer.Tick += (_, _) => { _caretVisible = !_caretVisible; Invalidate(); };
        Session.Changed += OnSessionChanged;
        Loaded += (_, _) => { Relayout(); SyncInput(); };
        Unloaded += (_, _) => _caretTimer.Stop();
        SyncInput();
    }
    public new static bool KeyDown(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
    public static bool ControlDown() => KeyDown(VirtualKey.Control) || KeyDown(VirtualKey.LeftWindows) || KeyDown(VirtualKey.RightWindows);
    public void FocusEditor()
    {
        if (_disposed || DeferFocusUntilPopupsClose()) return;
        if (_input.FocusState != FocusState.Unfocused) CommitNativeInput();
        _input.IsReadOnly = Session.IsReadOnly;
        SyncInput();
        _input.Focus(FocusState.Programmatic);
        _caretVisible = true;
        Invalidate();
    }
    public void Invalidate() => _canvas.Invalidate();
    public void Relayout()
    {
        Layout = Renderer.Layout(Session.Document); ClampScroll(); UpdateRuler(); UpdateInputPosition(); Invalidate(); ViewChanged?.Invoke();
    }
    private void OnSessionChanged(object? sender, EditorChangedEventArgs e)
    {
        if (_disposed) return;
        if (_nativeEdit) { QueueNativeViewRefresh(); return; }
        if (e.Kind == EditorChangeKind.Document)
        {
            if (_documentId != Session.Document.Id || e.Label == "Open document") { _documentId = Session.Document.Id; Renderer.ClearImages(); _scrollX = _scrollY = 0; SelectedImageId = null; }
            Relayout();
        }
        if (!_nativeEdit) SyncInput();
        if (e.Kind is EditorChangeKind.Document or EditorChangeKind.Selection) { _caretVisible = true; EnsureCaretVisible(); UpdateRuler(); }
        Invalidate();
    }
    private void SyncInput()
    {
        if (_syncing || _nativeEdit) return; _syncing = true;
        try
        {
            var text = Session.Document.PlainText; if (_input.Text != text) _input.Text = text;
            var selection = Session.Selection;
            _input.Select(selection.Start, selection.Length);
            _input.IsReadOnly = Session.IsReadOnly; UpdateInputPosition();
        }
        finally { _syncing = false; }
    }
    private void OnNativeTextChanged(object sender, TextChangedEventArgs e) => CommitNativeInput();
    private void CommitNativeInput()
    {
        if (_syncing || _nativeEdit || _ownsNativeInput) return;
        var oldText = Session.Document.PlainText; var newText = _input.Text.Replace("\r\n", "\n").Replace('\r', '\n'); if (oldText == newText) return;
        var prefix = 0; while (prefix < oldText.Length && prefix < newText.Length && oldText[prefix] == newText[prefix]) prefix++;
        var suffix = 0; while (suffix < oldText.Length - prefix && suffix < newText.Length - prefix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix]) suffix++;
        _nativeEdit = true;
        try { Session.Replace(prefix, oldText.Length - prefix - suffix, newText.Substring(prefix, newText.Length - prefix - suffix)); }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
        finally { _nativeEdit = false; if (_input.Text != Session.Document.PlainText) QueueNativeViewRefresh(); }
    }
    private void OnNativeSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing || _nativeEdit || _ownsNativeInput || _input.Text != Session.Document.PlainText) return;
        var start = _input.SelectionStart; var end = start + _input.SelectionLength;
        if (Session.Selection.Start == start && Session.Selection.End == end) return;
        Session.SetSelection(start, end);
    }
    private int HitTest(Point point) => Layout.HitTest((point.X - PaperLeft) / Scale, (point.Y - 18 + _scrollY) / Scale);
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas); _lastPointer = point.Position;
        if (point.Properties.IsRightButtonPressed) { if (Session.Selection.IsEmpty) { var at = HitTest(point.Position); Session.SetSelection(at, at); } ContextRequested?.Invoke(point.Position); e.Handled = true; return; }
        if (!point.Properties.IsLeftButtonPressed && point.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch) return;
        SelectedImageId = null; var documentY = (point.Position.Y - 18 + _scrollY) / Scale; var pageIndex = Math.Clamp((int)(documentY / (Layout.Settings.Height + DocumentLayout.PageGap)), 0, Layout.Pages.Count - 1); var paperX = (point.Position.X - PaperLeft) / Scale; var paperY = documentY - Layout.PageTop(pageIndex);
        var image = Layout.Pages[pageIndex].Images.LastOrDefault(i => i.Bounds.Contains(paperX, paperY));
        if (image is not null) { SelectedImageId = image.Image.Id; Invalidate(); ViewChanged?.Invoke(); CommandRequested?.Invoke("picture-selected"); e.Handled = true; return; }
        var position = HitTest(point.Position); _anchor = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift) ? Session.Selection.Anchor : position;
        Try(() => Session.SetSelection(_anchor, position)); _dragging = true; _canvas.CapturePointer(e.Pointer); FocusEditor(); _desiredX = null; e.Handled = true;
    }
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _lastPointer = e.GetCurrentPoint(_canvas).Position; if (!_dragging) return;
        if (_lastPointer.Y < 0) SetScroll(_scrollX, _scrollY - 18); else if (_lastPointer.Y > _canvas.ActualHeight) SetScroll(_scrollX, _scrollY + 18);
        Try(() => Session.SetSelection(_anchor, HitTest(_lastPointer))); e.Handled = true;
    }
    private void Draw(SKCanvas canvas, Size area)
    {
        canvas.Clear(SKColor.Parse("#E8E8E8")); var left = PaperLeft; var pageHeight = Layout.Settings.Height * Scale; using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 24), IsAntialias = true }; using var border = new SKPaint { Color = SKColor.Parse("#C4C4C4"), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        for (var i = 0; i < Layout.Pages.Count; i++)
        {
            var top = 18 + Layout.PageTop(i) * Scale - _scrollY; if (top > area.Height || top + pageHeight < 0) continue;
            canvas.DrawRect((float)left + 2, (float)top + 3, (float)(Layout.Width * Scale), (float)pageHeight, shadow);
            canvas.Save(); canvas.Translate((float)left, (float)top); canvas.Scale((float)Scale);
            Renderer.DrawPage(canvas, Session.Document, Layout, i, new() { Selection = Session.Selection, DrawCaret = _caretVisible && !Session.IsReadOnly && SelectedImageId is null, ShowFormatting = ShowFormatting, ShowComments = ShowComments, ShowChanges = ShowChanges, ShowBoundaries = ShowBoundaries, SelectedImageId = SelectedImageId }); canvas.Restore();
            canvas.DrawRect((float)left, (float)top, (float)(Layout.Width * Scale), (float)pageHeight, border);
        }
    }
    private void ClampScroll()
    {
        var width = Math.Max(1, _canvas.ActualWidth); var height = Math.Max(1, _canvas.ActualHeight); var extentWidth = Layout.Width * Scale + 68; var extentHeight = Layout.Height * Scale + 36;
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, extentWidth - width)); _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, extentHeight - height));
        _vertical.SetMetrics(extentHeight, height, _scrollY); _horizontal.SetMetrics(extentWidth, width, _scrollX);
    }
    public void SetScroll(double x, double y)
    {
        _scrollX = x; _scrollY = y; ClampScroll(); UpdateRuler(); UpdateInputPosition(); Invalidate(); ViewChanged?.Invoke();
    }
    public void SetZoom(double zoom, Point? anchor = null)
    {
        zoom = Math.Clamp(zoom, 0.25, 5); if (Math.Abs(zoom - _zoom) < 0.0001) return;
        var point = anchor ?? new Point(_canvas.ActualWidth / 2, _canvas.ActualHeight / 2); var logicalY = (point.Y - 18 + _scrollY) / Scale; _zoom = zoom; _scrollY = logicalY * Scale - point.Y + 18;
        ClampScroll(); UpdateRuler(); UpdateInputPosition(); Invalidate(); ViewChanged?.Invoke();
    }
    public void FitPageWidth() => SetZoom((_canvas.ActualWidth - 68) / (Layout.Width * 4d / 3));
    public void FitWholePage() => SetZoom(Math.Min((_canvas.ActualWidth - 68) / (Layout.Width * 4d / 3), (_canvas.ActualHeight - 36) / (Layout.Settings.Height * 4d / 3)));
    public void ScrollToPage(int pageIndex) => SetScroll(_scrollX, Layout.PageTop(Math.Clamp(pageIndex, 0, Layout.Pages.Count - 1)) * Scale);
    public void EnsureCaretVisible()
    {
        if (_canvas.ActualHeight < 1) return; var caret = Layout.Caret(Session.Selection.Active); var top = 18 + (Layout.PageTop(caret.PageIndex) + caret.Y) * Scale; var bottom = top + caret.Height * Scale;
        if (top < _scrollY + 12) _scrollY = Math.Max(0, top - 24); else if (bottom > _scrollY + _canvas.ActualHeight - 12) _scrollY = bottom - _canvas.ActualHeight + 24;
        var x = PaperLeft + caret.X * Scale; if (x < 28) _scrollX = Math.Max(0, _scrollX + x - 28); else if (x > _canvas.ActualWidth - 28) _scrollX += x - _canvas.ActualWidth + 28;
        ClampScroll(); UpdateInputPosition();
    }
    private void UpdateRuler()
    {
        _ruler.PageLeft = PaperLeft; _ruler.Page = Session.Document.Page; _ruler.Paragraph = Session.CurrentParagraph.Format; _ruler.Scale = Scale; _ruler.Typeface = Renderer.Metrics.Typeface(new()); _ruler.Invalidate();
    }
    private void UpdateInputPosition()
    {
        var caret = Layout.Caret(Session.Selection.Active); var x = Math.Clamp(PaperLeft + caret.X * Scale, 0, Math.Max(0, _canvas.ActualWidth - 3)); var y = Math.Clamp(18 + (Layout.PageTop(caret.PageIndex) + caret.Y) * Scale - _scrollY, 0, Math.Max(0, _canvas.ActualHeight - 25));
        _input.RenderTransform = new TranslateTransform { X = x, Y = y };
    }
    private void Try(Action action) { try { action(); } catch (Exception ex) { Error?.Invoke(ex.Message); SyncInput(); } }
    public new void Dispose()
    {
        if (_disposed) return; _disposed = true; _caretTimer.Stop(); Session.Changed -= OnSessionChanged; _input.PreviewKeyDown -= OnInputKeyDown; _input.PreviewKeyUp -= OnInputKeyUp; _canvas.Draw = null; Renderer.Dispose();
    }
}
