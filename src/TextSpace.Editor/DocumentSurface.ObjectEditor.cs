using TextSpace.Core;
using TextSpace.Editing;
using TextSpace.Layout;

namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    private FrameworkElement? _objectEditor;
    private VisualEditDraft? _objectDraft;
    private EquationEditorControl? _equationEditor;
    public bool IsObjectEditorOpen => _objectEditor is not null;
    public EquationEditorControl? ActiveEquationEditor => _equationEditor;

    public void BeginObjectEditor()
    {
        if (_objectEditor is not null) { _equationEditor?.FocusSlot(); return; }
        if (Session.IsReadOnly || SelectedObject is not (EquationBlock or ShapeBlock) || _selectedObjectId is null) return;
        CancelVisualGesture(); CancelTableGesture(); _objectDraft = new(Session, _selectedObjectId);
        FrameworkElement editor;
        if (_objectDraft.Value is EquationBlock equation)
        {
            var control = new EquationEditorControl(Renderer, equation.Root, equation.FontSize);
            _equationEditor = control;
            control.CommitRequested += ApplyObjectEditor;
            control.CancelRequested += () => { CancelObjectEditor(); FocusEditor(); };
            editor = control;
        }
        else
        {
            var shape = (ShapeBlock)_objectDraft.Value;
            var text = OfficeTheme.Field("Shape text"); text.Text = shape.Text; text.AcceptsReturn = true; text.TextWrapping = TextWrapping.Wrap;
            text.MaxLength = 100000;
            text.FontFamily = new FontFamily(shape.TextStyle.FontFamily);
            text.FontWeight = new() { Weight = (ushort)(shape.TextStyle.Bold ? 700 : 400) };
            text.FontStyle = shape.TextStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
            text.MinHeight = 100; text.MaxHeight = 300; text.FontSize = Math.Clamp(shape.TextStyle.FontSize * Scale, 10, 60);
            text.TextChanged += (_, _) => { if (_objectDraft?.Value is ShapeBlock draft) draft.Text = text.Text; };
            var apply = new OfficeButton { Content = "Done", IsPrimary = true, Padding = new(12, 5) }; AutomationProperties.SetName(apply, "Apply shape text"); apply.Click += (_, _) => ApplyObjectEditor();
            var cancel = new OfficeButton { Content = "Cancel", Padding = new(12, 5) }; cancel.Click += (_, _) => { CancelObjectEditor(); FocusEditor(); };
            text.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == VirtualKey.Escape) { CancelObjectEditor(); FocusEditor(); e.Handled = true; }
                else if (e.Key == VirtualKey.Enter && ControlDown()) { ApplyObjectEditor(); e.Handled = true; }
            };
            var panel = OfficeTheme.Column(OfficeTheme.Text("Edit text in " + shape.Kind, 14, OfficeTheme.Ink, true), text,
                OfficeTheme.Columns((OfficeTheme.Text("Ctrl+Enter applies · Escape cancels", 11, OfficeTheme.Muted), -1), (OfficeTheme.Row(cancel, apply), 0)));
            editor = new Border { Background = OfficeTheme.Brush("#FFFFFF"), BorderBrush = OfficeTheme.Brush(OfficeTheme.Accent), BorderThickness = new(1), CornerRadius = new(5), Padding = new(12), Child = panel };
            editor.Loaded += (_, _) => { text.Focus(FocusState.Programmatic); text.SelectAll(); };
        }
        _objectEditor = editor;
        editor.HorizontalAlignment = HorizontalAlignment.Left; editor.VerticalAlignment = VerticalAlignment.Top;
        editor.Width = Math.Max(240, Math.Min(680, _viewport.ActualWidth - 24));
        var top = 12d; var left = 12d;
        if (SelectedPlacement() is { } selected)
        {
            left = PaperLeft + (Layout.PageLeft(selected.Page) + selected.Item.Bounds.X) * Scale;
            top = 18 + (Layout.PageTop(selected.Page) + selected.Item.Bounds.Y) * Scale - _scrollY;
        }
        editor.Margin = new(Math.Clamp(left, 12, Math.Max(12, _viewport.ActualWidth - editor.Width - 12)), Math.Clamp(top, 12, Math.Max(12, _viewport.ActualHeight - 340)), 0, 0);
        _viewport.Children.Add(editor); Invalidate(); ObjectSelectionChanged?.Invoke();
    }
    private void ApplyObjectEditor()
    {
        if (_objectDraft is not { } draft) return;
        try
        {
            if (_equationEditor is { } editor && draft.Value is EquationBlock equation)
            {
                equation.Root = editor.Value; var measured = Renderer.MeasureEquation(equation.Root, equation.FontSize);
                var ratio = Math.Min(1, Math.Min(4000 / measured.Width, 4000 / measured.Height));
                equation.Width = Math.Max(6, measured.Width * ratio); equation.Height = Math.Max(6, measured.Height * ratio);
            }
            if (!draft.IsCurrent || Session.IsReadOnly) throw new InvalidOperationException("The document changed while the object editor was open. Copy your text before closing the editor.");
            VisualBlockRules.Validate(draft.Value);
            // Remove the editor before the single document notification is published.
            _objectDraft = null; RemoveObjectEditor(); try { draft.Commit(draft.Value is EquationBlock ? "Edit equation" : "Edit shape text"); } finally { draft.Dispose(); }
            FocusEditor();
        }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
    }
    public void CancelObjectEditor()
    {
        var draft = _objectDraft; _objectDraft = null; draft?.Dispose(); RemoveObjectEditor();
    }
    private void RemoveObjectEditor()
    {
        var editor = _objectEditor; _objectEditor = null; _equationEditor?.Dispose(); _equationEditor = null;
        if (editor is not null) { _viewport.Children.Remove(editor); ObjectSelectionChanged?.Invoke(); Invalidate(); }
    }
}
