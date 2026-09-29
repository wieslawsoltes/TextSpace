using System.Globalization;
using TextSpace.Editor;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private OfficeComboField? _objectWidth, _objectHeight, _objectRotation, _objectX, _objectY;
    private void InitializeVisualEditing()
    {
        Ribbon.AddTab("Shape Format", ShapeFormatRibbon, true); Ribbon.SetTabVisible("Shape Format", false);
        Ribbon.AddTab("Equation", EquationRibbon, true); Ribbon.SetTabVisible("Equation", false);
        Ribbon.InsertGroup("Picture Format", 0, ObjectTransformGroup);
        Ribbon.InsertGroup("Table Layout", 0, () => Group("Selection",
            MenuButton("select", "Select", new OfficeMenu().Add("Select Cell", () => Surface.SelectTableCells("cell"))
                .Add("Select Row", () => Surface.SelectTableCells("row")).Add("Select Column", () => Surface.SelectTableCells("column"))
                .Add("Select Table", () => Surface.SelectTableCells("table")))));
        Ribbon.InsertGroup("Table Layout", 4, () => Group("Cell Size",
            Tool("columns", "Distribute Columns", "distribute-columns", true), Tool("row", "Distribute Rows", "distribute-rows", true)));
        Surface.ObjectSelectionChanged += RefreshVisualRibbon;
    }
    private RibbonButton ShapeGallery()
    {
        var menu = new OfficeMenu();
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var name = kind.ToString(); menu.Add(kind == ShapeKind.TextBox ? "Text Box" : name, () => _ = ExecuteCommandAsync("insert-shape:" + name), "borders");
        }
        return MenuButton("borders", "Shapes", menu);
    }
    private RibbonGroup ObjectTransformGroup()
    {
        OfficeComboField Number(string name, double value, Action<VisualBlock, double> update, double[] presets, double width = 60)
        {
            var field = new OfficeComboField(name, presets.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray(), width) { Value = value.ToString("0.##", CultureInfo.InvariantCulture) };
            field.ValueCommitted += text => RunEdit(name, () => EditSelectedObject(name, block => update(block, ParseNumber(text)))); return field;
        }
        var selected = Surface.SelectedObject;
        _objectWidth = Number("Object width (pt)", selected?.Width ?? 180, (b, v) => b.Width = v, [72, 144, 180, 240, 360]);
        _objectHeight = Number("Object height (pt)", selected?.Height ?? 100, (b, v) => b.Height = v, [48, 72, 100, 144, 240]);
        _objectRotation = Number("Object rotation", selected?.Placement.Rotation ?? 0, (b, v) => b.Placement = b.Placement with { Rotation = v }, [0, 45, 90, 180, 270]);
        var size = OfficeTheme.Column(OfficeTheme.Row(OfficeTheme.Text("Width", 10), _objectWidth), OfficeTheme.Row(OfficeTheme.Text("Height", 10), _objectHeight));
        var position = new OfficeMenu().Add("In Line with Text", () => SetObjectFloating(false)).Add("In Front of Text", () => SetObjectFloating(true));
        var rotate = new OfficeMenu().Add("Rotate Right 90°", () => RotateObject(90)).Add("Rotate Left 90°", () => RotateObject(-90))
            .Separator().Add("Flip Horizontal", () => RunEdit("Flip horizontal", () => EditSelectedObject("Flip horizontal", b => b.Placement = b.Placement with { FlipHorizontal = !b.Placement.FlipHorizontal })))
            .Add("Flip Vertical", () => RunEdit("Flip vertical", () => EditSelectedObject("Flip vertical", b => b.Placement = b.Placement with { FlipVertical = !b.Placement.FlipVertical })));
        return Group("Size & Arrange", size, OfficeTheme.Column(_objectRotation, MenuButton("orientation", "Rotate", rotate, false, true)), MenuButton("layout", "Wrap Text", position),
            OfficeTheme.Column(Tool("copy", "Duplicate", "duplicate-object", showLabel: true), Tool("close", "Delete", "delete-object", showLabel: true)));
    }
    private IEnumerable<RibbonGroup> ShapeFormatRibbon()
    {
        yield return Group("Insert Shapes", ShapeGallery(), Tool("font", "Edit Text", "edit-object", true));
        yield return Group("Shape Styles", ColorButton("shade", "Shape Fill", color => EditSelectedObject("Shape fill", b => ((ShapeBlock)b).Fill = color), true, true),
            ColorButton("borders", "Shape Outline", color => EditSelectedObject("Shape outline", b => ((ShapeBlock)b).Stroke = color ?? "#202020"), large: true),
            MenuButton("line", "Weight", new OfficeMenu().Add("0.5 pt", () => ShapeWeight(0.5)).Add("1 pt", () => ShapeWeight(1)).Add("1.5 pt", () => ShapeWeight(1.5)).Add("3 pt", () => ShapeWeight(3)).Add("6 pt", () => ShapeWeight(6))));
        yield return Group("Text", ColorButton("font", "Text Fill", color => EditSelectedObject("Shape text color", b => { var shape = (ShapeBlock)b; shape.TextStyle = shape.TextStyle with { Color = color ?? "#202020" }; }), large: true),
            MenuButton("align-center", "Align Text", new OfficeMenu().Add("Top", () => ShapeTextAlign(CellVerticalAlignment.Top)).Add("Middle", () => ShapeTextAlign(CellVerticalAlignment.Center)).Add("Bottom", () => ShapeTextAlign(CellVerticalAlignment.Bottom))),
            Tool("bold", "Bold Text", "shape-bold"), Tool("italic", "Italic Text", "shape-italic"));
        yield return ObjectTransformGroup();
        yield return ObjectPositionGroup();
    }
    private RibbonGroup ObjectPositionGroup()
    {
        OfficeComboField Offset(string label, bool horizontal)
        {
            var field = new OfficeComboField(label, ["0", "12", "24", "48", "72"], 58);
            field.ValueCommitted += text => RunEdit(label, () => EditSelectedObject(label, b => b.Placement = horizontal ? b.Placement with { X = ParseNumber(text) } : b.Placement with { Y = ParseNumber(text) })); return field;
        }
        _objectX = Offset("Object horizontal offset (pt)", true); _objectY = Offset("Object vertical offset (pt)", false);
        return Group("Position", OfficeTheme.Column(OfficeTheme.Row(OfficeTheme.Text("X", 10), _objectX), OfficeTheme.Row(OfficeTheme.Text("Y", 10), _objectY)),
            MenuButton("align-center", "Align", new OfficeMenu().Add("Align Left", () => AlignObject(TextSpace.Core.TextAlignment.Left)).Add("Align Center", () => AlignObject(TextSpace.Core.TextAlignment.Center)).Add("Align Right", () => AlignObject(TextSpace.Core.TextAlignment.Right))),
            OfficeTheme.Column(Tool("up", "Move Before", "object-before", showLabel: true), Tool("down", "Move After", "object-after", showLabel: true)));
    }
    private IEnumerable<RibbonGroup> EquationRibbon()
    {
        var presets = new OfficeMenu();
        foreach (var (name, label) in new[] { ("blank", "Insert New Equation"), ("quadratic", "Quadratic Formula"), ("pythagoras", "Pythagorean Theorem"), ("identity", "Mass–Energy Equivalence") })
        { var key = name; presets.Add(label, () => _ = ExecuteCommandAsync("insert-equation:" + key)); }
        yield return Group("Tools", MenuButton("symbol", "Equation", presets), Tool("font", "Edit Equation", "edit-object", true));
        var structures = new[] { ("fraction", "Fraction"), ("superscript", "Script"), ("radical", "Radical"), ("integral", "Integral"), ("sum", "Large Operator"), ("parentheses", "Bracket"), ("vector", "Accent"), ("matrix", "Matrix") };
        var buttons = structures.Select(s => (UIElement)new RibbonButton("symbol", s.Item2, () => EquationStructure(s.Item1), large: true)).ToArray();
        yield return Group("Structures", buttons);
        yield return ObjectTransformGroup();
    }
    private void EquationStructure(string template)
    {
        Surface.BeginObjectEditor(); Surface.ActiveEquationEditor?.InsertStructure(template);
    }
    private void EditSelectedObject(string label, Action<VisualBlock> edit)
    {
        if (Surface.SelectedObjectId is not { } id) throw new InvalidOperationException("Select an object first.");
        Session.EditVisual(id, label, edit);
    }
    private void AlignObject(TextSpace.Core.TextAlignment alignment) => RunEdit("Align object", () => EditSelectedObject("Align object", b => { b.Alignment = alignment; b.Placement = b.Placement with { X = 0 }; }));
    private void RotateObject(double angle) => RunEdit("Rotate object", () => EditSelectedObject("Rotate object", b => b.Placement = b.Placement with { Rotation = (b.Placement.Rotation + angle) % 360 }));
    private void ShapeWeight(double weight) => RunEdit("Shape outline weight", () => EditSelectedObject("Shape outline weight", b => ((ShapeBlock)b).StrokeWidth = weight));
    private void ShapeTextAlign(CellVerticalAlignment alignment) => RunEdit("Shape text alignment", () => EditSelectedObject("Shape text alignment", b => ((ShapeBlock)b).VerticalAlignment = alignment));
    private void SetObjectFloating(bool value) => RunEdit(value ? "In front of text" : "In line with text", () =>
    {
        if (value && Surface.SelectedObjectId is { } id && Surface.IsInTable(id)) throw new InvalidOperationException("Objects inside table cells remain in flow. Move the object to the main story before using floating placement.");
        EditSelectedObject("Object text placement", b => b.Placement = b.Placement with { Floating = value, X = 0, Y = 0 });
    });
    private void RefreshVisualRibbon()
    {
        var block = Surface.SelectedObject;
        Ribbon.SetTabVisible("Shape Format", block is ShapeBlock); Ribbon.SetTabVisible("Equation", block is EquationBlock);
        Ribbon.SetTabVisible("Picture Format", block is ImageBlock);
        if (_objectWidth is not null) _objectWidth.Value = (block?.Width ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        if (_objectHeight is not null) _objectHeight.Value = (block?.Height ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        if (_objectRotation is not null) _objectRotation.Value = (block?.Placement.Rotation ?? 0).ToString("0.#", CultureInfo.InvariantCulture);
        if (_objectX is not null) _objectX.Value = (block?.Placement.X ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        if (_objectY is not null) _objectY.Value = (block?.Placement.Y ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        foreach (var id in new[] { "duplicate-object", "delete-object", "edit-object", "object-before", "object-after", "shape-bold", "shape-italic", "crop-picture", "reset-crop" })
            if (_buttons.TryGetValue(id, out var button)) button.IsEnabled = block is not null && !Session.IsReadOnly;
        StateChanged?.Invoke();
    }
    private bool ExecuteVisualCommand(string id)
    {
        if (id.StartsWith("insert-shape:", StringComparison.Ordinal))
        {
            var kind = Enum.Parse<ShapeKind>(id[13..]); var selected = Session.InsertShape(kind);
            Surface.SelectObject(selected, true); Ribbon.SelectTab("Shape Format"); Surface.FocusEditor(); return true;
        }
        if (id == "insert-equation" || id.StartsWith("insert-equation:", StringComparison.Ordinal))
        {
            var template = id == "insert-equation" ? "blank" : id[16..]; string? selected = null;
            Session.Execute("Insert equation", () =>
            {
                selected = Session.InsertEquation(template); var equation = (EquationBlock)Session.FindVisual(selected)!;
                var geometry = Surface.Renderer.MeasureEquation(equation.Root, equation.FontSize); equation.Width = Math.Max(6, geometry.Width); equation.Height = Math.Max(6, geometry.Height);
            });
            Surface.SelectObject(selected, true); Ribbon.SelectTab("Equation"); Surface.BeginObjectEditor(); return true;
        }
        switch (id)
        {
            case "edit-object": Surface.BeginObjectEditor(); return true;
            case "duplicate-object": if (Surface.SelectedObjectId is { } copied) Surface.SelectObject(Session.DuplicateVisual(copied), true); break;
            case "delete-object": if (Surface.SelectedObjectId is { } removed) { Session.DeleteVisual(removed); Surface.SelectObject(null); } break;
            case "object-before": if (Surface.SelectedObjectId is { } before) Session.MoveVisualInFlow(before, -1); break;
            case "object-after": if (Surface.SelectedObjectId is { } after) Session.MoveVisualInFlow(after, 1); break;
            case "shape-bold": EditSelectedObject("Shape bold", b => { var shape = (ShapeBlock)b; shape.TextStyle = shape.TextStyle with { Bold = !shape.TextStyle.Bold }; }); break;
            case "shape-italic": EditSelectedObject("Shape italic", b => { var shape = (ShapeBlock)b; shape.TextStyle = shape.TextStyle with { Italic = !shape.TextStyle.Italic }; }); break;
            case "crop-picture": Surface.ToggleCrop(); break;
            case "reset-crop": EditSelectedObject("Reset picture crop", b => ((ImageBlock)b).Crop = new()); break;
            case "distribute-columns": Session.DistributeTableColumns(Session.CurrentTable?.Id ?? throw new InvalidOperationException("Select a table first.")); break;
            case "distribute-rows": Session.DistributeTableRows(Session.CurrentTable?.Id ?? throw new InvalidOperationException("Select a table first.")); break;
            case "merge-cells" when Surface.SelectedCells is not null: Surface.MergeSelectedCells(); break;
            default: return false;
        }
        RefreshVisualRibbon(); Surface.FocusEditor(); return true;
    }
    private bool ShowVisualContextMenu()
    {
        if (Surface.SelectedObject is not { } block) return false;
        var menu = new OfficeMenu();
        if (block is ShapeBlock or EquationBlock) menu.Add(block is EquationBlock ? "Edit Equation" : "Edit Text", () => Surface.BeginObjectEditor(), "font", "Enter");
        if (block is ImageBlock) menu.Add("Crop", () => Surface.ToggleCrop(), "image").Add("Reset Crop", () => _ = ExecuteCommandAsync("reset-crop"));
        menu.Add("Duplicate", () => _ = ExecuteCommandAsync("duplicate-object"), "copy", "Ctrl+D").Add("Delete", () => _ = ExecuteCommandAsync("delete-object"), "close", "Delete")
            .Separator().Add("Rotate Right 90°", () => RotateObject(90)).Add("In Line with Text", () => SetObjectFloating(false)).Add("In Front of Text", () => SetObjectFloating(true));
        menu.AsFlyout().ShowAt(Surface); return true;
    }
}
