from pathlib import Path

def replace(path, old, new, count=1):
    p = Path(path); text = p.read_text()
    assert text.count(old) == count, (path, text.count(old), old[:110])
    p.write_text(text.replace(old, new))

replace('src/TextSpace.Layout/TableLayouter.cs', 'if (visual is ImageBlock image) result.Images.Add(new(image, bounds));', 'if (visual is ImageBlock visualImage) result.Images.Add(new(visualImage, bounds));')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '    public void FocusSlot() => _input.Focus(FocusState.Programmatic);', '''    public IReadOnlyList<EquationSlotDiagnostic> CaptureSlotDiagnostics(UIElement relativeTo)
    {
        var transform = _canvas.TransformToVisual(relativeTo);
        return _layout.Slots.Select(slot =>
        {
            var p = transform.TransformPoint(new Point((slot.Bounds.X + PaddingPoints) * DisplayScale, (slot.Bounds.Y + PaddingPoints) * DisplayScale));
            return new EquationSlotDiagnostic(slot.Id, slot.Role, slot.Text, p.X, p.Y, slot.Bounds.Width * DisplayScale, slot.Bounds.Height * DisplayScale, slot.Id == _active);
        }).ToArray();
    }
    public void FocusSlot() => _input.Focus(FocusState.Programmatic);''')
# Both TryVisualPressed(PointerRoutedEventArgs e) and TryVisualKey(KeyRoutedEventArgs e)
# must stop body editing while the detached object editor owns input.
replace('src/TextSpace.Editor/DocumentSurface.Visuals.cs', '        if (_objectEditor is not null) return false;', '        if (_objectEditor is not null) { e.Handled = true; return true; }', count=2)
replace('src/TextSpace.Editor/DocumentSurface.Visuals.cs', 'Renderer.ClearVisualLayouts(); Renderer.DrawVisualBlock', 'Renderer.InvalidateVisualLayout(block.Id); Renderer.DrawVisualBlock')
replace('src/TextSpace.Skia/DocumentRenderer.Visuals.cs', '    public void ClearVisualLayouts() { _equationLayouts.Clear(); _shapeLayouts.Clear(); }', '    public void ClearVisualLayouts() { _equationLayouts.Clear(); _shapeLayouts.Clear(); }\n    public void InvalidateVisualLayout(string id) { _equationLayouts.Remove(id); _shapeLayouts.Remove(id); }')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        if (_disposed || DeferFocusUntilPopupsClose()) return;', '        if (_disposed || IsObjectEditorOpen || DeferFocusUntilPopupsClose()) return;')
replace('src/TextSpace.Editor/DocumentSurface.cs', '_input.IsReadOnly = Session.IsReadOnly;', '_input.IsReadOnly = Session.IsReadOnly || _selectedObjectId is not null;', count=2)
replace('src/TextSpace.Editor/DocumentSurface.cs', '            if (_ownsNativeInput && !_syncing) e.Cancel = true;', '            if ((_ownsNativeInput || _selectedObjectId is not null) && !_syncing) e.Cancel = true;')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        _canvas.PointerReleased += (_, e) => { _dragging = false; _canvas.ReleasePointerCaptures(); e.Handled = true; };', '        _canvas.PointerReleased += (_, e) => { _dragging = false; if (!CompleteVisualGesture()) CompleteTableGesture(); _canvas.ReleasePointerCaptures(); e.Handled = true; };')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        _canvas.PointerCaptureLost += (_, _) => _dragging = false;', '        _canvas.PointerCaptureLost += (_, _) => { _dragging = false; CancelVisualGesture(); CancelTableGesture(); };')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        _canvas.DoubleTapped += (_, e) => { Try(() => Session.SelectWord(HitTest(e.GetPosition(_canvas)))); FocusEditor(); e.Handled = true; };', '        _canvas.DoubleTapped += (_, e) => { if (!TryVisualDoubleTap(e.GetPosition(_canvas))) { Try(() => Session.SelectWord(HitTest(e.GetPosition(_canvas)))); FocusEditor(); } e.Handled = true; };')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        if (_nativeEdit) { QueueNativeViewRefresh(); return; }', '        if (_nativeEdit) { QueueNativeViewRefresh(); return; }\n        RefreshVisualState(e);')
replace('src/TextSpace.Editor/DocumentSurface.cs', 'if (e.Kind is EditorChangeKind.Document or EditorChangeKind.Selection) { _caretVisible = true; EnsureCaretVisible(); UpdateRuler(); }', 'if (e.Kind is EditorChangeKind.Document or EditorChangeKind.Selection) { _caretVisible = true; if (_selectedObjectId is null && _tableGesture is null) EnsureCaretVisible(); UpdateRuler(); }')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        if (_syncing || _nativeEdit || _ownsNativeInput || _disposed) return;', '        if (_syncing || _nativeEdit || _ownsNativeInput || _disposed || _selectedObjectId is not null) return;')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        if (_syncing || _nativeEdit || _ownsNativeInput || _disposed || _input.Text != _inputText.Text) return;', '        if (_syncing || _nativeEdit || _ownsNativeInput || _disposed || _selectedObjectId is not null || _input.Text != _inputText.Text) return;')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        var point = e.GetCurrentPoint(_canvas); _lastPointer = point.Position;', '        if (TryVisualPressed(e) || TryTablePressed(e)) return;\n        var point = e.GetCurrentPoint(_canvas); _lastPointer = point.Position;')
p = Path('src/TextSpace.Editor/DocumentSurface.cs'); text = p.read_text(); a = text.index('        SelectedImageId = null; var documentY ='); b = text.index('        var position = HitTest(point.Position);', a)
text = text[:a] + '        SelectObject(null); ClearCellSelection();\n' + text[b:]; p.write_text(text)
replace('src/TextSpace.Editor/DocumentSurface.cs', '        _lastPointer = e.GetCurrentPoint(_canvas).Position; if (!_dragging) return;', '        if (TryVisualMoved(e) || TryTableMoved(e)) return;\n        _lastPointer = e.GetCurrentPoint(_canvas).Position; if (!_dragging) return;')
replace('src/TextSpace.Editor/DocumentSurface.cs', 'DrawCaret = _caretVisible && !Session.IsReadOnly && SelectedImageId is null', 'DrawCaret = _caretVisible && !Session.IsReadOnly && _selectedObjectId is null && SelectedCells is null')
replace('src/TextSpace.Editor/DocumentSurface.cs', 'SelectedImageId = SelectedImageId }); canvas.Restore();', 'SelectedImageId = SelectedImageId, HiddenObjectId = _visualGesture?.Item.Object.Id });\n            DrawVisualInteraction(canvas, i); canvas.Restore();')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        zoom = Math.Clamp(zoom, 0.25, 5);', '        CancelVisualGesture(); CancelTableGesture();\n        zoom = Math.Clamp(zoom, 0.25, 5);')
replace('src/TextSpace.Editor/DocumentSurface.cs', '        if (_disposed) return; _disposed = true; _caretTimer.Stop();', '        if (_disposed) return; CancelObjectEditor(); CancelVisualGesture(); CancelTableGesture(); _disposed = true; _caretTimer.Stop();')
replace('src/TextSpace.Editor/DocumentSurface.Keyboard.cs', '        var control = ControlDown();\n        if (!IsDocumentKey(e.Key, control)) return;', '''        var control = ControlDown();
        if (TryVisualKey(e)) return;
        if (KeyDown(VirtualKey.Menu) && e.Key == (VirtualKey)187) { CommandRequested?.Invoke("insert-equation"); e.Handled = true; return; }
        if (!IsDocumentKey(e.Key, control)) return;''')
replace('src/TextSpace.Workbench/WordWorkbench.cs', 'InitializeTypography(); InitializeTableTools();', 'InitializeTypography(); InitializeTableTools(); InitializeVisualEditing();')
replace('src/TextSpace.Workbench/WordWorkbench.Commands.cs', '            switch (id)\n            {', '            if (ExecuteVisualCommand(id)) return;\n            switch (id)\n            {')
replace('src/TextSpace.Workbench/WordWorkbench.cs', '    private void ShowContextMenu()\n    {', '    private void ShowContextMenu()\n    {\n        if (ShowVisualContextMenu()) return;')
replace('src/TextSpace.Workbench/WordWorkbench.Ribbon.cs', 'Unavailable("borders", "Shapes", "Drawing shapes and floating text boxes are not implemented in this release.")', 'ShapeGallery()')
replace('src/TextSpace.Workbench/WordWorkbench.Ribbon.cs', 'yield return Group("Symbols", MenuButton("symbol", "Symbol", symbols));', 'yield return Group("Symbols", Tool("symbol", "Equation", "insert-equation", true), MenuButton("symbol", "Symbol", symbols));')
replace('src/TextSpace.Workbench/WordWorkbench.Visuals.cs', '    private OfficeComboField? _objectWidth, _objectHeight, _objectRotation, _objectX, _objectY;', '    private OfficeComboField? _objectWidth, _objectHeight, _objectRotation, _objectX, _objectY;\n    private string? _visualContextId;')
replace('src/TextSpace.Workbench/WordWorkbench.Visuals.cs', '        Ribbon.InsertGroup("Picture Format", 0, ObjectTransformGroup);', '        Ribbon.InsertGroup("Picture Format", 0, ObjectTransformGroup);\n        Ribbon.InsertGroup("Picture Format", 1, () => Group("Crop", Tool("image", "Crop", "crop-picture", true), Tool("undo", "Reset Crop", "reset-crop", true)));')
replace('src/TextSpace.Workbench/WordWorkbench.Visuals.cs', '        Ribbon.SetTabVisible("Picture Format", block is ImageBlock);', '''        Ribbon.SetTabVisible("Picture Format", block is ImageBlock);
        if (_visualContextId != block?.Id)
        {
            _visualContextId = block?.Id;
            if (block is not null) Ribbon.SelectTab(block is ShapeBlock ? "Shape Format" : block is EquationBlock ? "Equation" : "Picture Format");
        }''')
replace('src/TextSpace.Workbench/WordWorkbench.cs', '        RefreshStatus(); RefreshFormatting();\n        if (e.Kind == EditorChangeKind.Document)', '        RefreshStatus(); RefreshFormatting(); RefreshVisualRibbon();\n        if (e.Kind == EditorChangeKind.Document)')
replace('src/TextSpace.Workbench/WordWorkbench.cs', 'Session.Changed -= OnSessionChanged; Surface.Dispose();', 'Session.Changed -= OnSessionChanged; Surface.ObjectSelectionChanged -= RefreshVisualRibbon; Surface.Dispose();')
replace('src/TextSpace.App/Platforms/WebAssembly/BrowserDiagnostics.cs', '                    WriteTables(json, session, surface);', '                    WriteTables(json, session, surface);\n                    WriteVisuals(json, workbench);')
Path('scripts/integrate-visual-ui.py').unlink(); Path('.github/workflows/visual-ui-integration.yml').unlink()
print('Integrated native gesture handling, contextual ribbons and read-only diagnostics. Temporary integration files removed.')
