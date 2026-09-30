using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    public VisualBlock? FindVisual(string? id) => id is null ? null : BlockTree.Find(Document.Blocks, id)?.Block as VisualBlock;

    public string InsertShape(ShapeKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var shape = new ShapeBlock { Kind = kind, Width = 180, Height = kind is ShapeKind.Line or ShapeKind.Arrow ? 48 : 100,
            Text = kind == ShapeKind.TextBox ? "Type your text" : "", Fill = kind == ShapeKind.TextBox ? "#FFFFFF" : "#D9E5F5" };
        InsertBlock(shape, "Insert " + kind); return shape.Id;
    }
    public string InsertEquation(string template = "blank")
    {
        var equation = new EquationBlock { Root = EquationTemplates.Create(template) };
        InsertBlock(equation, "Insert equation"); return equation.Id;
    }
    public void ReplaceVisual(string id, VisualBlock replacement, string label = "Edit object")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentNullException.ThrowIfNull(replacement);
        EnsureWritable();
        var location = BlockTree.Find(Document.Blocks, id) ?? throw new InvalidOperationException("The object no longer exists.");
        if (location.Block is not VisualBlock original || replacement.GetType() != original.GetType())
            throw new InvalidOperationException("Object replacement must preserve its type.");
        var owned = BlockTree.CloneVisual(replacement); owned.Id = id;
        Execute(label, () => location.Container[location.Index] = owned);
    }
    public void EditVisual(string id, string label, Action<VisualBlock> edit)
    {
        ArgumentNullException.ThrowIfNull(edit); EnsureWritable();
        var source = FindVisual(id) ?? throw new InvalidOperationException("Select an object first.");
        var draft = BlockTree.CloneVisual(source); edit(draft); ReplaceVisual(id, draft, label);
    }
    public void DeleteVisual(string id)
    {
        EnsureWritable(); var location = BlockTree.Find(Document.Blocks, id) ?? throw new InvalidOperationException("The object no longer exists.");
        if (location.Block is not VisualBlock) throw new InvalidOperationException("The selected block is not a visual object.");
        Execute("Delete object", () => location.Container.RemoveAt(location.Index));
    }
    public string DuplicateVisual(string id)
    {
        EnsureWritable(); var location = BlockTree.Find(Document.Blocks, id) ?? throw new InvalidOperationException("The object no longer exists.");
        if (location.Block is not VisualBlock visual) throw new InvalidOperationException("Select an object first.");
        var copy = BlockTree.CloneVisual(visual, newIds: true);
        if (copy.Placement.Floating) copy.Placement = copy.Placement with { X = Math.Clamp(copy.Placement.X + 12, -4000, 4000), Y = Math.Clamp(copy.Placement.Y + 12, -4000, 4000) };
        Execute("Duplicate object", () => location.Container.Insert(location.Index + 1, copy)); return copy.Id;
    }
    public void MoveVisualInFlow(string id, int direction)
    {
        EnsureWritable(); if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var location = BlockTree.Find(Document.Blocks, id) ?? throw new InvalidOperationException("The object no longer exists.");
        if (location.Block is not VisualBlock) throw new InvalidOperationException("Select an object first.");
        var target = location.Index + direction;
        if (target < 0 || target >= location.Container.Count) return;
        // Object blocks have no main-story UTF-16 positions. Paragraph order stays unchanged.
        Execute(direction < 0 ? "Move object before" : "Move object after", () =>
        {
            var block = location.Block; location.Container.RemoveAt(location.Index); location.Container.Insert(target, block);
        });
    }
    public void SetEquation(string id, EquationNode root, double width, double height)
    {
        EquationRules.Validate(root); var owned = root.Clone();
        EditVisual(id, "Edit equation", block =>
        {
            if (block is not EquationBlock equation) throw new InvalidOperationException("The object is not an equation.");
            equation.Root = owned; equation.Width = width; equation.Height = height;
        });
    }
}
