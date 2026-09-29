using System.Text;

namespace TextSpace.Core;

public enum EquationKind { Text, Row, Fraction, Radical, Superscript, Subscript, SubSuperscript, Delimited, Matrix, Nary, Accent }

/// <summary>
/// A bounded presentation-math tree. IDs are stable during editing and identify
/// hit-tested slots; all source characters and structure remain editable.
/// Radical children: radicand/index. Nary children: body/lower/upper.
/// SubSuperscript children: base/subscript/superscript. Matrices are row-major.
/// </summary>
public sealed class EquationNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public EquationKind Kind { get; set; }
    public string Text { get; set; } = "";
    public List<EquationNode> Children { get; set; } = [];
    public int Columns { get; set; } = 1;

    public static EquationNode Leaf(string text = "") => new() { Text = text };
    public static EquationNode Row(params EquationNode[] children) => new() { Kind = EquationKind.Row, Children = children.Length == 0 ? [Leaf()] : [.. children] };
    public static EquationNode Structure(EquationKind kind, params EquationNode[] children) => new() { Kind = kind, Children = [.. children] };

    public EquationNode Clone(bool newIds = false) => new()
    {
        Id = newIds ? Guid.NewGuid().ToString("N") : Id, Kind = Kind, Text = Text,
        Columns = Columns, Children = Children.Select(n => n.Clone(newIds)).ToList()
    };

    public IEnumerable<EquationNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var node in child.DescendantsAndSelf()) yield return node;
    }

    /// <summary>Human-readable linear form; not a general LaTeX parser or evaluator.</summary>
    public string ToLinearText()
    {
        string C(int at) => Children[at].ToLinearText();
        return Kind switch
        {
            EquationKind.Text => Text,
            EquationKind.Row => string.Concat(Children.Select(n => n.ToLinearText())),
            EquationKind.Fraction => $"({C(0)})/({C(1)})",
            EquationKind.Radical => string.IsNullOrEmpty(C(1)) ? $"√({C(0)})" : $"root[{C(1)}]({C(0)})",
            EquationKind.Superscript => $"{C(0)}^({C(1)})",
            EquationKind.Subscript => $"{C(0)}_({C(1)})",
            EquationKind.SubSuperscript => $"{C(0)}_({C(1)})^({C(2)})",
            EquationKind.Delimited => $"{Text[0]}{C(0)}{Text[1]}",
            EquationKind.Matrix => "[" + string.Join("; ", Children.Chunk(Columns).Select(row => string.Join(", ", row.Select(n => n.ToLinearText())))) + "]",
            EquationKind.Nary => $"{Text}_({C(1)})^({C(2)}) {C(0)}",
            EquationKind.Accent => $"{Text}({C(0)})",
            _ => throw new InvalidDataException("Unsupported equation structure.")
        };
    }
}

public static class EquationRules
{
    public const int MaximumNodes = 4096;
    public const int MaximumDepth = 32;
    public const int MaximumCharacters = 16384;

    public static void Validate(EquationNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var nodes = new HashSet<EquationNode>(ReferenceEqualityComparer.Instance);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var characters = 0;
        void Walk(EquationNode node, int depth)
        {
            if (node is null || depth > MaximumDepth || !nodes.Add(node) || nodes.Count > MaximumNodes)
                throw new InvalidDataException("Equation is cyclic, shared, too deep or too large.");
            if (string.IsNullOrWhiteSpace(node.Id) || node.Id.Length > 64 || !ids.Add(node.Id)
                || !Enum.IsDefined(node.Kind) || node.Text is null || node.Children is null)
                throw new InvalidDataException("Invalid equation node.");
            characters += node.Text.Length;
            if (characters > MaximumCharacters || node.Text.Any(c => char.IsControl(c)) || !WellFormed(node.Text))
                throw new InvalidDataException("Equation text is oversized or contains invalid characters.");
            var count = node.Children.Count;
            var valid = node.Kind switch
            {
                EquationKind.Text => count == 0,
                EquationKind.Row => count is >= 1 and <= 1024,
                EquationKind.Fraction or EquationKind.Radical or EquationKind.Superscript or EquationKind.Subscript => count == 2,
                EquationKind.SubSuperscript => count == 3,
                EquationKind.Delimited => count == 1 && node.Text.Length == 2 && "([{⌈⌊|".Contains(node.Text[0]) && ")]}⌉⌋|".Contains(node.Text[1]),
                EquationKind.Matrix => node.Columns is >= 1 and <= 10 && count >= node.Columns && count % node.Columns == 0 && count / node.Columns <= 10,
                EquationKind.Nary => count == 3 && node.Text is "∑" or "∏" or "∫" or "∮" or "⋃" or "⋂",
                EquationKind.Accent => count == 1 && node.Text is "¯" or "^" or "~" or "→" or "˙" or "¨",
                _ => false
            };
            if (!valid) throw new InvalidDataException("Invalid equation structure or child count.");
            foreach (var child in node.Children) Walk(child, depth + 1);
        }
        Walk(root, 0);
    }

    private static bool WellFormed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }

    /// <summary>Stable content key excluding slot IDs, suitable for typography caches.</summary>
    public static string ContentKey(EquationNode root)
    {
        Validate(root);
        var text = new StringBuilder();
        void Write(EquationNode n)
        {
            text.Append((int)n.Kind).Append(':').Append(n.Columns).Append(':').Append(n.Text.Length).Append(':').Append(n.Text).Append('[');
            foreach (var child in n.Children) Write(child);
            text.Append(']');
        }
        Write(root); return text.ToString();
    }
}
