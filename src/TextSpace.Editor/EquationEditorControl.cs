using SkiaSharp;
using TextSpace.Core;
using TextSpace.Layout;
using TextSpace.Skia;
using Uno.WinUI.Graphics2DSK;

namespace TextSpace.Editor;

/// <summary>
/// Reusable WYSIWYG presentation-math editor. Native input edits the selected leaf
/// at its measured position; structure, layout and navigation use the same tree.
/// It owns a detached draft and never writes to an EditorSession by itself.
/// </summary>
public sealed class EquationEditorControl : UserControl, IDisposable
{
    private sealed class EquationCanvas : SKCanvasElement
    {
        public Action<SKCanvas>? Paint;
        protected override void RenderOverride(SKCanvas canvas, Size area) => Paint?.Invoke(canvas);
    }
    private sealed record History(EquationNode Root, string? Slot);
    private readonly DocumentRenderer _renderer;
    private readonly EquationCanvas _canvas = new();
    private readonly Canvas _inputLayer = new();
    private readonly TextBox _input = OfficeTheme.Field("Equation slot");
    private readonly TextBlock _status = OfficeTheme.Text("", 11, OfficeTheme.Muted);
    private readonly List<History> _undo = [], _redo = [];
    private readonly Grid _surface = new();
    private EquationNode _root;
    private EquationLayout _layout;
    private string? _active;
    private bool _syncing, _pending, _disposed;
    private const double DisplayScale = 1.5;
    private const double PaddingPoints = 14;
    public double EquationFontSize { get; }
    public EquationNode Value
    {
        get
        {
            if (!CaptureInput()) throw new InvalidOperationException("Correct the equation input before applying it.");
            return _root.Clone();
        }
    }
    public EquationLayout Geometry => _layout;
    public string? ActiveSlot => _active;
    public event Action? CommitRequested;
    public event Action? CancelRequested;
    public event Action? DraftChanged;

    public EquationEditorControl(DocumentRenderer renderer, EquationNode root, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(renderer); EquationRules.Validate(root);
        _renderer = renderer; _root = root.Clone(); EquationFontSize = fontSize;
        _layout = renderer.MeasureEquation(_root, fontSize); _active = _layout.Slots.FirstOrDefault()?.Id;
        _canvas.Paint = Paint; _surface.Children.Add(_canvas); _surface.Children.Add(_inputLayer);
        _inputLayer.Children.Add(_input); _input.Padding = new(1, 0, 1, 0); _input.MinHeight = 0; _input.BorderThickness = new(0);
        _input.Background = OfficeTheme.Brush("#DCEBFF"); _input.FontFamily = new FontFamily("Times New Roman"); _input.IsSpellCheckEnabled = false;
        _input.TextWrapping = TextWrapping.NoWrap; _input.AcceptsReturn = false;
        _input.TextChanged += (_, _) => CaptureInput();
        _input.PreviewKeyDown += OnKeyDown;
        _input.BeforeTextChanging += (_, e) => { if (!_syncing && e.NewText.Length > EquationRules.MaximumCharacters) e.Cancel = true; };
        _canvas.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(_canvas); var slot = _layout.HitTest(point.Position.X / DisplayScale - PaddingPoints, point.Position.Y / DisplayScale - PaddingPoints);
            if (slot is not null) Activate(slot.Id); e.Handled = true;
        };
        var templates = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var (name, label) in new[] { ("fraction", "Fraction"), ("radical", "Radical"), ("superscript", "Script"), ("subsuperscript", "Sub/Sup"), ("matrix", "Matrix"), ("sum", "Sum"), ("integral", "Integral"), ("parentheses", "Brackets"), ("vector", "Accent") })
        {
            var template = name; var button = new OfficeButton { Content = label, FontSize = 11, Padding = new(6, 4), Height = 28 };
            AutomationProperties.SetName(button, "Equation " + label); button.Click += (_, _) => InsertStructure(template); templates.Children.Add(button);
        }
        var done = new OfficeButton { Content = "Done", IsPrimary = true, Padding = new(12, 5) };
        AutomationProperties.SetName(done, "Apply equation"); done.Click += (_, _) => { if (CaptureInput()) CommitRequested?.Invoke(); };
        var cancel = new OfficeButton { Content = "Cancel", Padding = new(12, 5) }; AutomationProperties.SetName(cancel, "Cancel equation"); cancel.Click += (_, _) => CancelRequested?.Invoke();
        var undo = new OfficeButton { Content = "Undo", Padding = new(8, 5) }; undo.Click += (_, _) => Restore(false);
        var redo = new OfficeButton { Content = "Redo", Padding = new(8, 5) }; redo.Click += (_, _) => Restore(true);
        var header = OfficeTheme.Column(OfficeTheme.Text("Equation", 14, OfficeTheme.Ink, true),
            OfficeTheme.Text("Click a slot to type · Tab moves between slots · Ctrl+Enter applies · Escape cancels", 11, OfficeTheme.Muted),
            new ScrollViewer { Content = templates, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var scroller = new ScrollViewer { Content = _surface, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 100, MaxHeight = 340 };
        var footer = OfficeTheme.Columns((_status, -1), (OfficeTheme.Row(undo, redo, cancel, done), 0));
        Content = new Border { Background = OfficeTheme.Brush("#FFFFFF"), BorderBrush = OfficeTheme.Brush(OfficeTheme.Accent), BorderThickness = new(1), CornerRadius = new(5), Padding = new(10), Child = OfficeTheme.Column(header, scroller, footer) };
        AutomationProperties.SetName(this, "WYSIWYG equation editor");
        Loaded += (_, _) => Activate(_active); Rebuild();
    }

    private bool CaptureInput()
    {
        if (_disposed) return false;
        if (_syncing || _active is null) return true;
        var current = _root.DescendantsAndSelf().FirstOrDefault(n => n.Id == _active && n.Kind == EquationKind.Text);
        if (current is null) return false;
        if (current.Text == _input.Text) return true;
        var candidate = _root.Clone(); var node = candidate.DescendantsAndSelf().First(n => n.Id == _active); node.Text = _input.Text;
        try
        {
            EquationRules.Validate(candidate); Remember(); _root = candidate;
            // Let native TextBox finish selection/composition before moving its visual bounds.
            if (!_pending)
            {
                _pending = true; DispatcherQueue.TryEnqueue(() => { _pending = false; if (_disposed) return; Rebuild(syncText: false); DraftChanged?.Invoke(); });
            }
            return true;
        }
        catch (Exception ex) { _status.Text = ex.Message; return false; }
    }
    private void Remember()
    {
        _undo.Add(new(_root.Clone(), _active)); if (_undo.Count > 32) _undo.RemoveAt(0); _redo.Clear();
    }
    private void Restore(bool redo)
    {
        var source = redo ? _redo : _undo; var target = redo ? _undo : _redo; if (source.Count == 0) return;
        target.Add(new(_root.Clone(), _active)); var entry = source[^1]; source.RemoveAt(source.Count - 1); _root = entry.Root; _active = entry.Slot;
        Rebuild(); FocusSlot(); DraftChanged?.Invoke();
    }
    public void InsertStructure(string template)
    {
        try
        {
            if (!CaptureInput()) return; var slot = _root.DescendantsAndSelf().FirstOrDefault(n => n.Id == _active && n.Kind == EquationKind.Text); if (slot is null) return;
            var start = Math.Clamp(_input.SelectionStart, 0, slot.Text.Length); var length = Math.Clamp(_input.SelectionLength, 0, slot.Text.Length - start);
            var structure = EquationTemplates.Create(template, EquationNode.Leaf(slot.Text.Substring(start, length)));
            var before = slot.Text[..start]; var after = slot.Text[(start + length)..];
            var replacement = before.Length == 0 && after.Length == 0 ? structure
                : EquationNode.Row(EquationNode.Leaf(before), structure, EquationNode.Leaf(after));
            var candidate = EquationTemplates.Replace(_root, slot.Id, replacement); Remember(); _root = candidate;
            _active = structure.DescendantsAndSelf().FirstOrDefault(n => n.Kind == EquationKind.Text && n.Text.Length == 0)?.Id
                ?? structure.DescendantsAndSelf().First(n => n.Kind == EquationKind.Text).Id;
            Rebuild(); FocusSlot(); DraftChanged?.Invoke();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }
    public void InsertSymbol(string symbol)
    {
        _input.SelectedText = symbol; CaptureInput(); FocusSlot();
    }
    private void Rebuild(bool syncText = true)
    {
        if (_disposed) return;
        try { _layout = _renderer.MeasureEquation(_root, EquationFontSize); }
        catch (Exception ex) { _status.Text = ex.Message; return; }
        if (!_layout.Slots.Any(s => s.Id == _active)) _active = _layout.Slots.FirstOrDefault()?.Id;
        _surface.Width = Math.Max(240, (_layout.Width + PaddingPoints * 2) * DisplayScale);
        _surface.Height = Math.Max(90, (_layout.Height + PaddingPoints * 2) * DisplayScale);
        PositionInput(syncText); _canvas.Invalidate();
    }
    private void PositionInput(bool syncText)
    {
        var slot = _layout.Slots.FirstOrDefault(s => s.Id == _active); if (slot is null) return;
        _syncing = true;
        try
        {
            if (syncText && _input.Text != slot.Text) _input.Text = slot.Text;
            _input.FontSize = slot.FontSize * DisplayScale; _input.Width = Math.Max(18, slot.Bounds.Width * DisplayScale + 8);
            _input.Height = Math.Max(20, slot.Bounds.Height * DisplayScale + 4);
            Canvas.SetLeft(_input, (slot.Bounds.X + PaddingPoints) * DisplayScale); Canvas.SetTop(_input, (slot.Bounds.Y + PaddingPoints) * DisplayScale - 2);
            AutomationProperties.SetName(_input, "Equation " + slot.Role); _status.Text = slot.Role + " · " + (_layout.Slots.ToList().FindIndex(s => s.Id == slot.Id) + 1) + " / " + _layout.Slots.Count;
        }
        finally { _syncing = false; }
    }
    private void Activate(string? id)
    {
        if (!CaptureInput()) return; _active = id; Rebuild(); FocusSlot(); _input.SelectAll();
    }
    public IReadOnlyList<EquationSlotDiagnostic> CaptureSlotDiagnostics(UIElement relativeTo)
    {
        var transform = _canvas.TransformToVisual(relativeTo);
        return _layout.Slots.Select(slot =>
        {
            var p = transform.TransformPoint(new Point((slot.Bounds.X + PaddingPoints) * DisplayScale, (slot.Bounds.Y + PaddingPoints) * DisplayScale));
            return new EquationSlotDiagnostic(slot.Id, slot.Role, slot.Text, p.X, p.Y, slot.Bounds.Width * DisplayScale, slot.Bounds.Height * DisplayScale, slot.Id == _active);
        }).ToArray();
    }
    public void FocusSlot() { if (!_disposed) _input.Focus(FocusState.Programmatic); }
    public void Dispose() { _disposed = true; _canvas.Paint = null; }
    private void Paint(SKCanvas canvas)
    {
        canvas.Clear(SKColors.White); canvas.Save(); canvas.Scale((float)DisplayScale); canvas.Translate((float)PaddingPoints, (float)PaddingPoints);
        _renderer.DrawEquation(canvas, _layout, showSlots: true, activeSlot: _active, hideActiveText: true); canvas.Restore();
    }
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var control = DocumentSurface.ControlDown(); var shift = DocumentSurface.KeyDown(VirtualKey.Shift);
        if (e.Key == VirtualKey.Escape) { CancelRequested?.Invoke(); e.Handled = true; return; }
        if (control && e.Key == VirtualKey.Enter) { if (CaptureInput()) CommitRequested?.Invoke(); e.Handled = true; return; }
        if (control && e.Key is VirtualKey.Z or VirtualKey.Y) { Restore(e.Key == VirtualKey.Y || shift); e.Handled = true; return; }
        if (e.Key == VirtualKey.Tab)
        {
            var slots = _layout.Slots; var index = slots.ToList().FindIndex(s => s.Id == _active);
            Activate(slots[(index + (shift ? -1 : 1) + slots.Count) % slots.Count].Id); e.Handled = true; return;
        }
        if (e.Key is VirtualKey.Up or VirtualKey.Down && _layout.VerticalNeighbor(_active ?? "", e.Key == VirtualKey.Down) is { } neighbor)
        { Activate(neighbor.Id); e.Handled = true; return; }
        if (e.Key is VirtualKey.Left or VirtualKey.Right && _input.SelectionLength == 0
            && (e.Key == VirtualKey.Left ? _input.SelectionStart == 0 : _input.SelectionStart == _input.Text.Length))
        {
            var index = _layout.Slots.ToList().FindIndex(s => s.Id == _active) + (e.Key == VirtualKey.Left ? -1 : 1);
            if (index >= 0 && index < _layout.Slots.Count) { Activate(_layout.Slots[index].Id); e.Handled = true; }
        }
    }
}
