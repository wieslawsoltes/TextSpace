using System.Xml.Linq;
using TextSpace.Core;

namespace TextSpace.OpenXml;

/// <summary>
/// Bounded, non-executing conversion between editable presentation math and OMML.
/// Unsupported structures retain their visible text with a warning, never execute fields.
/// </summary>
public static class OfficeMathCodec
{
    public static readonly XNamespace Namespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    private static readonly XNamespace W = Ooxml.W;
    private static XElement E(string name, params object?[] value) => new(Namespace + name, value);
    private static XElement Property(string name, object value) => E(name, new XAttribute(Namespace + "val", value));
    private static string? Value(XElement? element) => (string?)element?.Attribute(Namespace + "val");

    public static XElement Write(EquationNode root, TextStyle? style = null)
    {
        EquationRules.Validate(root);
        style ??= new TextStyle { FontFamily = "Cambria Math", FontSize = 18 };
        var format = style;
        IEnumerable<XElement> Children(EquationNode node) => node.Children.SelectMany(WriteNode);
        IEnumerable<XElement> WriteNode(EquationNode node)
        {
            XElement Child(string name, int index) => E(name, WriteNode(node.Children[index]));
            XElement result;
            switch (node.Kind)
            {
                case EquationKind.Text:
                    yield return E("r", E("rPr", Property("sty", "p")), DocxWriter.RunProperties(format),
                        E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), node.Text));
                    yield break;
                case EquationKind.Row:
                    foreach (var child in Children(node)) yield return child;
                    yield break;
                case EquationKind.Fraction:
                    result = E("f", E("fPr", Property("type", "bar")), Child("num", 0), Child("den", 1)); break;
                case EquationKind.Radical:
                    result = E("rad", E("radPr", Property("degHide", node.Children[1].ToLinearText().Length == 0 ? "1" : "0")), Child("deg", 1), Child("e", 0)); break;
                case EquationKind.Superscript:
                    result = E("sSup", Child("e", 0), Child("sup", 1)); break;
                case EquationKind.Subscript:
                    result = E("sSub", Child("e", 0), Child("sub", 1)); break;
                case EquationKind.SubSuperscript:
                    result = E("sSubSup", Child("e", 0), Child("sub", 1), Child("sup", 2)); break;
                case EquationKind.Delimited:
                    result = E("d", E("dPr", Property("begChr", node.Text[..1]), Property("endChr", node.Text[1..]), Property("grow", "1")), Child("e", 0)); break;
                case EquationKind.Matrix:
                    result = E("m");
                    for (var row = 0; row < node.Children.Count; row += node.Columns)
                        result.Add(E("mr", node.Children.Skip(row).Take(node.Columns).Select(n => E("e", WriteNode(n)))));
                    break;
                case EquationKind.Nary:
                    result = E("nary", E("naryPr", Property("chr", node.Text), Property("limLoc", "undOvr"), Property("grow", "1"),
                        Property("subHide", node.Children[1].ToLinearText().Length == 0 ? "1" : "0"), Property("supHide", node.Children[2].ToLinearText().Length == 0 ? "1" : "0")),
                        Child("sub", 1), Child("sup", 2), Child("e", 0)); break;
                case EquationKind.Accent:
                    result = E("acc", E("accPr", Property("chr", ToCombiningAccent(node.Text))), Child("e", 0)); break;
                default: throw new InvalidDataException("Unsupported equation structure.");
            }
            yield return result;
        }
        return E("oMath", WriteNode(root));
    }

    public static EquationNode Read(XElement math, Action<string>? warning = null)
    {
        ArgumentNullException.ThrowIfNull(math);
        CheckXmlBudget(math);
        if (math.Name != Namespace + "oMath" && math.Name != Namespace + "oMathPara")
            throw new InvalidDataException("Expected an Office Math expression.");
        var count = 0;
        EquationNode ReadRow(XElement container, int depth)
        {
            var nodes = container.Elements().Where(e => !e.Name.LocalName.EndsWith("Pr", StringComparison.Ordinal))
                .Select(e => ReadNode(e, depth + 1)).ToArray();
            return nodes.Length == 1 ? nodes[0] : EquationNode.Row(nodes);
        }
        EquationNode ReadNode(XElement element, int depth)
        {
            if (++count > EquationRules.MaximumNodes || depth > EquationRules.MaximumDepth)
                throw new InvalidDataException("Office Math exceeds the editable structure budget.");
            EquationNode Child(string name) => element.Element(Namespace + name) is { } child ? ReadRow(child, depth) : EquationNode.Leaf();
            var kind = element.Name.Namespace == Namespace ? element.Name.LocalName : "unsupported";
            switch (kind)
            {
                case "r": return EquationNode.Leaf(string.Concat(element.Elements(Namespace + "t").Select(e => e.Value)));
                case "oMath": case "oMathPara": case "e": case "num": case "den": case "deg": case "sub": case "sup": return ReadRow(element, depth);
                case "f": return EquationNode.Structure(EquationKind.Fraction, Child("num"), Child("den"));
                case "rad": return EquationNode.Structure(EquationKind.Radical, Child("e"), Child("deg"));
                case "sSup": return EquationNode.Structure(EquationKind.Superscript, Child("e"), Child("sup"));
                case "sSub": return EquationNode.Structure(EquationKind.Subscript, Child("e"), Child("sub"));
                case "sSubSup": return EquationNode.Structure(EquationKind.SubSuperscript, Child("e"), Child("sub"), Child("sup"));
                case "d":
                    var properties = element.Element(Namespace + "dPr");
                    var begin = Value(properties?.Element(Namespace + "begChr")) ?? "(";
                    var end = Value(properties?.Element(Namespace + "endChr")) ?? ")";
                    if (begin.Length != 1 || end.Length != 1 || !"([{⌈⌊|".Contains(begin[0]) || !")]}⌉⌋|".Contains(end[0])) break;
                    var bodies = element.Elements(Namespace + "e").Select(e => ReadRow(e, depth)).ToArray();
                    if (bodies.Length != 1) break;
                    return new() { Kind = EquationKind.Delimited, Text = begin + end, Children = [bodies[0]] };
                case "m":
                    var rows = element.Elements(Namespace + "mr").ToArray();
                    var columns = rows.FirstOrDefault()?.Elements(Namespace + "e").Count() ?? 0;
                    if (columns is < 1 or > 10 || rows.Length > 10 || rows.Any(r => r.Elements(Namespace + "e").Count() != columns)) break;
                    return new() { Kind = EquationKind.Matrix, Columns = columns,
                        Children = rows.SelectMany(r => r.Elements(Namespace + "e")).Select(e => ReadRow(e, depth)).ToList() };
                case "nary":
                    var symbol = Value(element.Element(Namespace + "naryPr")?.Element(Namespace + "chr")) ?? "∫";
                    if (symbol is not ("∑" or "∏" or "∫" or "∮" or "⋃" or "⋂")) break;
                    return new() { Kind = EquationKind.Nary, Text = symbol, Children = [Child("e"), Child("sub"), Child("sup")] };
                case "acc":
                    var accent = FromCombiningAccent(Value(element.Element(Namespace + "accPr")?.Element(Namespace + "chr")) ?? "\u0302");
                    if (accent is null) break;
                    return new() { Kind = EquationKind.Accent, Text = accent, Children = [Child("e")] };
                case "bar":
                    if (Value(element.Element(Namespace + "barPr")?.Element(Namespace + "pos")) == "bot") break;
                    return new() { Kind = EquationKind.Accent, Text = "¯", Children = [Child("e")] };
            }
            warning?.Invoke("Unsupported Office Math structure '" + element.Name.LocalName + "' was imported as its visible text; its layout semantics are not preserved.");
            return EquationNode.Leaf(string.Concat(element.DescendantsAndSelf().Where(e => e.Name == Namespace + "t").Select(e => e.Value)));
        }
        var root = ReadRow(math, 0);
        EquationRules.Validate(root); return root;
    }

    private static void CheckXmlBudget(XElement root)
    {
        var pending = new Stack<(XElement Node, int Depth)>(); pending.Push((root, 0));
        var count = 0; long characters = 0;
        while (pending.TryPop(out var item))
        {
            if (++count > EquationRules.MaximumNodes * 16 || item.Depth > EquationRules.MaximumDepth * 4)
                throw new InvalidDataException("Office Math XML exceeds the import budget.");
            foreach (var attribute in item.Node.Attributes()) characters += attribute.Value.Length;
            foreach (var text in item.Node.Nodes().OfType<XText>()) characters += text.Value.Length;
            if (characters > EquationRules.MaximumCharacters * 16L) throw new InvalidDataException("Office Math XML text exceeds the import budget.");
            foreach (var child in item.Node.Elements()) pending.Push((child, item.Depth + 1));
        }
    }
    private static string ToCombiningAccent(string text) => text switch { "¯" => "\u0305", "^" => "\u0302", "~" => "\u0303", "→" => "\u20d7", "˙" => "\u0307", "¨" => "\u0308", _ => text };
    private static string? FromCombiningAccent(string text) => text switch { "\u0305" or "¯" => "¯", "\u0302" or "^" => "^", "\u0303" or "~" => "~", "\u20d7" or "→" => "→", "\u0307" or "˙" => "˙", "\u0308" or "¨" => "¨", _ => null };
}
