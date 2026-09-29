namespace TextSpace.Core;

public static class EquationTemplates
{
    public static EquationNode Create(string name, EquationNode? selected = null)
    {
        var value = selected?.Clone() ?? EquationNode.Leaf();
        EquationNode Empty() => EquationNode.Leaf();
        return name switch
        {
            "fraction" => EquationNode.Structure(EquationKind.Fraction, value, Empty()),
            "radical" => EquationNode.Structure(EquationKind.Radical, value, Empty()),
            "superscript" => EquationNode.Structure(EquationKind.Superscript, value, Empty()),
            "subscript" => EquationNode.Structure(EquationKind.Subscript, value, Empty()),
            "subsuperscript" => EquationNode.Structure(EquationKind.SubSuperscript, value, Empty(), Empty()),
            "parentheses" => new() { Kind = EquationKind.Delimited, Text = "()", Children = [value] },
            "brackets" => new() { Kind = EquationKind.Delimited, Text = "[]", Children = [value] },
            "matrix" => new() { Kind = EquationKind.Matrix, Columns = 2, Children = [value, Empty(), Empty(), Empty()] },
            "sum" => new() { Kind = EquationKind.Nary, Text = "∑", Children = [value, Empty(), Empty()] },
            "integral" => new() { Kind = EquationKind.Nary, Text = "∫", Children = [value, Empty(), Empty()] },
            "bar" => new() { Kind = EquationKind.Accent, Text = "¯", Children = [value] },
            "vector" => new() { Kind = EquationKind.Accent, Text = "→", Children = [value] },
            "quadratic" => EquationNode.Row(EquationNode.Leaf("x = "), EquationNode.Structure(EquationKind.Fraction,
                EquationNode.Row(EquationNode.Leaf("−b ± "), EquationNode.Structure(EquationKind.Radical,
                    EquationNode.Row(EquationNode.Structure(EquationKind.Superscript, EquationNode.Leaf("b"), EquationNode.Leaf("2")), EquationNode.Leaf(" − 4ac")), Empty())),
                EquationNode.Leaf("2a"))),
            "pythagoras" => EquationNode.Row(EquationNode.Structure(EquationKind.Superscript, EquationNode.Leaf("a"), EquationNode.Leaf("2")), EquationNode.Leaf(" + "),
                EquationNode.Structure(EquationKind.Superscript, EquationNode.Leaf("b"), EquationNode.Leaf("2")), EquationNode.Leaf(" = "),
                EquationNode.Structure(EquationKind.Superscript, EquationNode.Leaf("c"), EquationNode.Leaf("2"))),
            "identity" => EquationNode.Row(EquationNode.Leaf("E = "), EquationNode.Structure(EquationKind.Superscript, EquationNode.Leaf("mc"), EquationNode.Leaf("2"))),
            "blank" => EquationNode.Row(Empty()),
            _ => throw new ArgumentException("Unknown equation template.", nameof(name))
        };
    }

    /// <summary>Replace a node without retaining aliases to the caller's replacement tree.</summary>
    public static EquationNode Replace(EquationNode root, string id, EquationNode replacement)
    {
        EquationRules.Validate(root); EquationRules.Validate(replacement);
        var found = false;
        EquationNode Copy(EquationNode node)
        {
            if (node.Id == id) { found = true; return replacement.Clone(); }
            return new() { Id = node.Id, Kind = node.Kind, Text = node.Text, Columns = node.Columns, Children = node.Children.Select(Copy).ToList() };
        }
        var result = Copy(root);
        if (!found) throw new InvalidOperationException("The equation slot no longer exists.");
        EquationRules.Validate(result); return result;
    }
}
