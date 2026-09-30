using System.Globalization;

namespace TextSpace.Core;

public enum EquationMatrixOperation { InsertRowBelow, InsertColumnRight, DeleteRow, DeleteColumn }
public sealed record EquationEditResult(EquationNode Root, string ActiveSlotId);

/// <summary>Pure, validated structure edits; input trees are never mutated or aliased.</summary>
public static class EquationOperations
{
    public static EquationEditResult EditMatrix(EquationNode root, string activeSlotId, EquationMatrixOperation operation)
    {
        EquationRules.Validate(root);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        var candidate = root.Clone(); var path = Path(candidate, activeSlotId);
        var ancestor = path.FindLastIndex(n => n.Kind == EquationKind.Matrix);
        if (ancestor < 0 || ancestor + 1 >= path.Count) throw new InvalidOperationException("Select a slot inside a matrix first.");
        var matrix = path[ancestor]; var cell = matrix.Children.IndexOf(path[ancestor + 1]);
        var columns = matrix.Columns; var rows = matrix.Children.Count / columns;
        var row = cell / columns; var column = cell % columns; string? active = null;
        switch (operation)
        {
            case EquationMatrixOperation.InsertRowBelow:
                if (rows == 10) throw new InvalidOperationException("A matrix supports at most ten rows.");
                var added = Enumerable.Range(0, columns).Select(_ => EquationNode.Leaf()).ToArray();
                matrix.Children.InsertRange((row + 1) * columns, added); active = added[column].Id; break;
            case EquationMatrixOperation.InsertColumnRight:
                if (columns == 10) throw new InvalidOperationException("A matrix supports at most ten columns.");
                for (var r = rows - 1; r >= 0; r--)
                {
                    var next = EquationNode.Leaf(); matrix.Children.Insert(r * columns + column + 1, next);
                    if (r == row) active = next.Id;
                }
                matrix.Columns++; break;
            case EquationMatrixOperation.DeleteRow:
                if (rows == 1) throw new InvalidOperationException("The last matrix row cannot be removed. Delete the structure instead.");
                matrix.Children.RemoveRange(row * columns, columns);
                active = FirstSlot(matrix.Children[Math.Min(row, rows - 2) * columns + column]); break;
            case EquationMatrixOperation.DeleteColumn:
                if (columns == 1) throw new InvalidOperationException("The last matrix column cannot be removed. Delete the structure instead.");
                for (var r = rows - 1; r >= 0; r--) matrix.Children.RemoveAt(r * columns + column);
                matrix.Columns--;
                active = FirstSlot(matrix.Children[row * matrix.Columns + Math.Min(column, matrix.Columns - 1)]); break;
        }
        EquationRules.Validate(candidate);
        return new(candidate, active ?? throw new InvalidOperationException("Matrix edit did not select a surviving slot."));
    }
    public static EquationEditResult ConvertStructureToText(EquationNode root, string activeSlotId) => ReplaceStructure(root, activeSlotId, false);
    public static EquationEditResult DeleteStructure(EquationNode root, string activeSlotId) => ReplaceStructure(root, activeSlotId, true);
    private static EquationEditResult ReplaceStructure(EquationNode root, string activeSlotId, bool delete)
    {
        EquationRules.Validate(root); var path = Path(root, activeSlotId);
        var target = path.LastOrDefault(n => n.Kind is not (EquationKind.Text or EquationKind.Row)) ?? path[0];
        var leaf = EquationNode.Leaf(delete ? "" : target.ToLinearText());
        var candidate = EquationTemplates.Replace(root, target.Id, leaf);
        return new(candidate, leaf.Id);
    }
    public static (int Start, int Length) GraphemeRange(string text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start < 0 || start > text.Length || length < 0 || length > text.Length - start) throw new ArgumentOutOfRangeException(nameof(start));
        var boundaries = StringInfo.ParseCombiningCharacters(text);
        int Snap(int offset, bool forward)
        {
            if (offset == text.Length) return offset;
            var at = Array.BinarySearch(boundaries, offset); if (at >= 0) return offset;
            at = ~at; return forward ? at < boundaries.Length ? boundaries[at] : text.Length : at > 0 ? boundaries[at - 1] : 0;
        }
        var first = Snap(start, false); var end = length == 0 ? first : Snap(start + length, true);
        return (first, end - first);
    }
    private static string FirstSlot(EquationNode node) => node.DescendantsAndSelf().First(n => n.Kind == EquationKind.Text).Id;
    private static List<EquationNode> Path(EquationNode root, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var path = new List<EquationNode>();
        bool Find(EquationNode node)
        {
            path.Add(node); if (node.Id == id) return true;
            foreach (var child in node.Children) if (Find(child)) return true;
            path.RemoveAt(path.Count - 1); return false;
        }
        if (!Find(root) || path[^1].Kind != EquationKind.Text) throw new InvalidOperationException("The selected equation slot no longer exists.");
        return path;
    }
}
