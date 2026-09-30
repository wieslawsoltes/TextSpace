from pathlib import Path
changes = {}
def read(path):
    return changes.get(path, Path(path).read_text())
def replace(path, old, new, count=1):
    text = read(path)
    assert text.count(old) == count, (path, text.count(old), old[:100])
    changes[path] = text.replace(old, new)
def region(path, start, end, replacement):
    text = read(path)
    assert text.count(start) == text.count(end) == 1, path
    a = text.index(start); b = text.index(end, a)
    changes[path] = text[:a] + replacement + text[b:]

region('src/TextSpace.Skia/DocumentRenderer.Visuals.cs', '    private readonly Dictionary<string, (EquationNode', '    private void DrawVisualObjects', '''    private VisualLayoutCache? _visualLayouts;
    public VisualLayoutCache VisualLayouts => _visualLayouts ??= new(Metrics);
    public void ClearVisualLayouts() => _visualLayouts?.Clear();
    public void InvalidateVisualLayout(string id) => _visualLayouts?.Invalidate(id);
    public EquationLayout MeasureEquation(EquationNode root, double fontSize = 18) => new EquationLayouter(Metrics).Layout(root, fontSize);
    public EquationLayout MeasureEquation(EquationBlock equation) => VisualLayouts.GetEquation(equation);

''')
region('src/TextSpace.Skia/DocumentRenderer.Visuals.cs', '            if (!_shapeLayouts.TryGetValue(shape.Id, out var cached)', '            canvas.Save(); canvas.ClipRect(SKRect.Create((float)shape.Padding', '            var shapeLines = VisualLayouts.GetShape(shape);\n')
replace('src/TextSpace.Skia/DocumentRenderer.Visuals.cs', 'foreach (var line in cached.Lines)', 'foreach (var line in shapeLines)')
replace('src/TextSpace.Editor/DocumentSurface.Visuals.cs', 'Renderer.InvalidateVisualLayout(block.Id); Renderer.DrawVisualBlock', 'Renderer.DrawVisualBlock')
region('src/TextSpace.Editor/DocumentSurface.Visuals.cs', '    private (int Page, LayoutObject Item)? SelectedPlacement()', '    private Point PagePoint', '''    private DocumentLayout? _indexedVisualLayout;
    private readonly Dictionary<string, (int Page, LayoutObject Item)> _visualLocations = new(StringComparer.Ordinal);
    private (int Page, LayoutObject Item)? SelectedPlacement()
    {
        if (_selectedObjectId is null) return null;
        if (!ReferenceEquals(_indexedVisualLayout, Layout))
        {
            _visualLocations.Clear();
            foreach (var item in VisualObjects()) if (!item.Item.IsReplica) _visualLocations.TryAdd(item.Item.Object.Id, item);
            _indexedVisualLayout = Layout;
        }
        return _visualLocations.TryGetValue(_selectedObjectId, out var value) ? value : null;
    }
    private IEnumerable<(int Page, LayoutObject Item)> PageVisualObjects(int index)
    {
        var page = Layout.Pages[index];
        if (page.Objects.Count > 0)
        {
            foreach (var item in page.Objects) yield return (index, item);
        }
        else foreach (var image in page.Images) yield return (index, new LayoutObject(image.Image, image.Bounds) { IsReplica = image.IsReplica });
    }
''')
replace('src/TextSpace.Editor/DocumentSurface.Visuals.cs', 'VisualObjects().Where(i => i.Page == page && !i.Item.IsReplica)', 'PageVisualObjects(page).Where(i => !i.Item.IsReplica)')
replace('src/TextSpace.Editor/DocumentSurface.Visuals.cs', 'VisualObjects().Reverse().FirstOrDefault(i => i.Page == page && !i.Item.IsReplica && ContainsVisual(i.Item, p))', 'PageVisualObjects(page).Reverse().FirstOrDefault(i => !i.Item.IsReplica && ContainsVisual(i.Item, p))')
replace('src/TextSpace.Documents/HtmlExporter.cs', 'public static class HtmlExporter', 'public static partial class HtmlExporter')
replace('src/TextSpace.Documents/HtmlExporter.cs', '                    case ImageBlock image when image.ContentType', '                    case ShapeBlock shape: body.Append(ExportVisual(shape)); break;\n                    case EquationBlock equation: body.Append(ExportVisual(equation)); break;\n                    case ImageBlock image when image.ContentType')
region('src/TextSpace.Documents/HtmlExporter.cs', '                        body.Append("<img alt=', '                    case PageBreakBlock:', '                        body.Append(ExportVisual(image)); break;\n')
replace('src/TextSpace.Documents/HtmlExporter.cs', '.Append("pt;margin:0 auto;column-count:")', '.Append("pt;margin:0 auto;position:relative;column-count:")')

replace('src/TextSpace.Editor/EquationEditorControl.cs', 'public sealed class EquationEditorControl : UserControl', 'public sealed class EquationEditorControl : UserControl, IDisposable')
replace('src/TextSpace.Editor/EquationEditorControl.cs', 'private bool _syncing, _pending;', 'private bool _syncing, _pending, _disposed;')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '    public EquationNode Value => _root.Clone();', '''    public EquationNode Value
    {
        get
        {
            if (!CaptureInput()) throw new InvalidOperationException("Correct the equation input before applying it.");
            return _root.Clone();
        }
    }''')
replace('src/TextSpace.Editor/EquationEditorControl.cs', 'CaptureInput(); CommitRequested?.Invoke();', 'if (CaptureInput()) CommitRequested?.Invoke();', count=2)
replace('src/TextSpace.Editor/EquationEditorControl.cs', '    private void CaptureInput()', '    private bool CaptureInput()')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '        if (_syncing || _active is null) return;', '        if (_disposed) return false;\n        if (_syncing || _active is null) return true;')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '        if (current is null || current.Text == _input.Text) return;', '        if (current is null) return false;\n        if (current.Text == _input.Text) return true;')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '_pending = false; Rebuild(syncText: false); DraftChanged?.Invoke();', '_pending = false; if (_disposed) return; Rebuild(syncText: false); DraftChanged?.Invoke();')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '''            }
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }
    private void Remember()''', '''            }
            return true;
        }
        catch (Exception ex) { _status.Text = ex.Message; return false; }
    }
    private void Remember()''')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '            CaptureInput(); var slot =', '            if (!CaptureInput()) return; var slot =')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '        CaptureInput(); _active = id;', '        if (!CaptureInput()) return; _active = id;')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '    private void Rebuild(bool syncText = true)\n    {', '    private void Rebuild(bool syncText = true)\n    {\n        if (_disposed) return;')
replace('src/TextSpace.Editor/EquationEditorControl.cs', '    public void FocusSlot() => _input.Focus(FocusState.Programmatic);', '    public void FocusSlot() { if (!_disposed) _input.Focus(FocusState.Programmatic); }\n    public void Dispose() { _disposed = true; _canvas.Paint = null; }')
replace('src/TextSpace.Editor/DocumentSurface.ObjectEditor.cs', '            text.MinHeight = 100;', '            text.MaxLength = 100000;\n            text.FontFamily = new FontFamily(shape.TextStyle.FontFamily);\n            text.FontWeight = new() { Weight = (ushort)(shape.TextStyle.Bold ? 700 : 400) };\n            text.FontStyle = shape.TextStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;\n            text.MinHeight = 100;')
replace('src/TextSpace.Editor/DocumentSurface.ObjectEditor.cs', '            VisualBlockRules.Validate(draft.Value);', '            if (!draft.IsCurrent || Session.IsReadOnly) throw new InvalidOperationException("The document changed while the object editor was open. Copy your text before closing the editor.");\n            VisualBlockRules.Validate(draft.Value);')
replace('src/TextSpace.Editor/DocumentSurface.ObjectEditor.cs', 'draft.Commit(draft.Value is EquationBlock ? "Edit equation" : "Edit shape text"); draft.Dispose();', 'try { draft.Commit(draft.Value is EquationBlock ? "Edit equation" : "Edit shape text"); } finally { draft.Dispose(); }')
replace('src/TextSpace.Editor/DocumentSurface.ObjectEditor.cs', '        var editor = _objectEditor; _objectEditor = null; _equationEditor = null;', '        var editor = _objectEditor; _objectEditor = null; _equationEditor?.Dispose(); _equationEditor = null;')
replace('src/TextSpace.OpenXml/DocxReader.Visuals.cs', 'if (double.IsFinite(nativeX + nativeY + standardX + standardY) && Math.Abs(x - standardX) < 0.051', 'if ((floating || result.Alignment == alignment) && double.IsFinite(nativeX + nativeY + standardX + standardY) && Math.Abs(x - standardX) < 0.051')
for path, text in changes.items(): Path(path).write_text(text)
Path('scripts/integrate-visual-refinements.py').unlink()
Path('.github/workflows/visual-refinements-integration.yml').unlink()
print('Integrated visual cache, safe HTML export, and object input ownership.')
