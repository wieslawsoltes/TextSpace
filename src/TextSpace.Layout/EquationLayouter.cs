using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>
/// Bounded presentation-math layout. Uses the host's real glyph metrics and emits
/// retained vector rules. OpenType MATH-table glyph assembly is not implemented.
/// </summary>
public sealed class EquationLayouter(ITextMetrics metrics)
{
    private sealed class Box(double width, double ascent, double descent)
    {
        public double Width = width, Ascent = ascent, Descent = descent;
        public double Height => Ascent + Descent;
        public readonly List<EquationGlyph> Glyphs = [];
        public readonly List<EquationRule> Rules = [];
        public readonly List<EquationSlot> Slots = [];
        public void Add(Box child, double x, double y)
        {
            Glyphs.AddRange(child.Glyphs.Select(g => g with { X = g.X + x, Y = g.Y + y }));
            Rules.AddRange(child.Rules.Select(r => r with { Points = r.Points.Select(p => new EquationPoint(p.X + x, p.Y + y)).ToArray() }));
            Slots.AddRange(child.Slots.Select(s => s with { Bounds = new(s.Bounds.X + x, s.Bounds.Y + y, s.Bounds.Width, s.Bounds.Height) }));
        }
        public void Rule(double thickness, params EquationPoint[] points) => Rules.Add(new(points, thickness));
    }

    public EquationLayout Layout(EquationNode root, double fontSize = 18)
    {
        EquationRules.Validate(root);
        if (!double.IsFinite(fontSize) || fontSize is < 6 or > 200) throw new ArgumentOutOfRangeException(nameof(fontSize));
        var box = Build(root, fontSize, "expression");
        if (!double.IsFinite(box.Width + box.Height) || box.Width > 100000 || box.Height > 100000)
            throw new InvalidDataException("The equation exceeds the supported layout extent.");
        return new(box.Width, box.Height, box.Ascent, box.Glyphs.ToArray(), box.Rules.ToArray(), box.Slots.ToArray());
    }

    private Box Text(string text, double size, string? id = null, string role = "symbol")
    {
        var style = new TextStyle { FontFamily = "Times New Roman", FontSize = size };
        var placeholder = text.Length == 0; var shown = placeholder ? "□" : text;
        var m = metrics.Measure(shown, style);
        if (!double.IsFinite(m.Width + m.Ascent + m.Descent) || m.Width < 0 || m.Ascent < 0 || m.Descent < 0)
            throw new InvalidOperationException("Equation glyph metrics must be finite and nonnegative.");
        var box = new Box(Math.Max(size * 0.4, m.Width), Math.Max(size * 0.7, m.Ascent), Math.Max(size * 0.2, m.Descent));
        box.Glyphs.Add(new(shown, style, 0, 0, box.Ascent, Placeholder: placeholder));
        if (id is not null) box.Slots.Add(new(id, new(0, 0, box.Width, box.Height), size, text, role));
        return box;
    }

    private Box Build(EquationNode node, double size, string role)
    {
        var small = Math.Max(6, size * 0.72); var gap = size * 0.18; var rule = Math.Max(0.6, size * 0.055);
        Box Child(int i, double? font = null, string? name = null) => Build(node.Children[i], font ?? size, name ?? role);
        switch (node.Kind)
        {
            case EquationKind.Text: return Text(node.Text, size, node.Id, role);
            case EquationKind.Row:
            {
                var children = node.Children.Select(n => Build(n, size, role)).ToArray();
                var result = new Box(children.Sum(b => b.Width), children.Max(b => b.Ascent), children.Max(b => b.Descent));
                var x = 0d; foreach (var child in children) { result.Add(child, x, result.Ascent - child.Ascent); x += child.Width; }
                return result;
            }
            case EquationKind.Fraction:
            {
                var n = Child(0, name: "numerator"); var d = Child(1, name: "denominator");
                var width = Math.Max(n.Width, d.Width) + gap * 2; var height = n.Height + d.Height + gap * 2 + rule;
                var ascent = n.Height + gap + rule / 2 + size * 0.25;
                var result = new Box(width, ascent, Math.Max(0, height - ascent));
                result.Add(n, (width - n.Width) / 2, 0); result.Add(d, (width - d.Width) / 2, n.Height + gap * 2 + rule);
                result.Rule(rule, new(0, n.Height + gap + rule / 2), new(width, n.Height + gap + rule / 2)); return result;
            }
            case EquationKind.Radical:
            {
                var body = Child(0, name: "radicand"); var index = Child(1, small * 0.8 < 6 ? 6 : small * 0.8, "root index");
                var sign = size * 0.75; var top = gap + rule; var left = Math.Max(sign, index.Width * 0.65 + size * 0.4);
                var result = new Box(left + body.Width + gap, top + body.Ascent, body.Descent);
                result.Add(body, left, top); result.Add(index, 0, 0);
                result.Rule(rule, new(left - sign, result.Height * 0.58), new(left - sign * 0.73, result.Height * 0.5),
                    new(left - sign * 0.45, result.Height - rule), new(left - sign * 0.12, rule / 2), new(result.Width, rule / 2));
                return result;
            }
            case EquationKind.Superscript:
            case EquationKind.Subscript:
            case EquationKind.SubSuperscript:
            {
                var basis = Child(0, name: "base");
                var sub = node.Kind == EquationKind.Superscript ? null : Child(1, small, "subscript");
                var sup = node.Kind == EquationKind.Subscript ? null : Child(node.Kind == EquationKind.SubSuperscript ? 2 : 1, small, "superscript");
                var baseY = sup is null ? 0 : Math.Max(0, sup.Height - basis.Ascent * 0.35);
                var subY = baseY + basis.Ascent + size * 0.05;
                var height = Math.Max(baseY + basis.Height, sub is null ? 0 : subY + sub.Height);
                var result = new Box(basis.Width + size * 0.06 + Math.Max(sub?.Width ?? 0, sup?.Width ?? 0), baseY + basis.Ascent, height - baseY - basis.Ascent);
                result.Add(basis, 0, baseY); if (sup is not null) result.Add(sup, basis.Width + size * 0.06, 0);
                if (sub is not null) result.Add(sub, basis.Width + size * 0.06, subY); return result;
            }
            case EquationKind.Delimited:
            {
                var body = Child(0); var width = size * 0.46; var height = Math.Max(size, body.Height + gap);
                var result = new Box(body.Width + width * 2 + gap, body.Ascent + gap / 2, Math.Max(0, height - body.Ascent - gap / 2));
                result.Add(body, width + gap / 2, gap / 2);
                AddDelimiter(result, node.Text[..1], 0, height, width, size);
                AddDelimiter(result, node.Text[1..], result.Width - width, height, width, size); return result;
            }
            case EquationKind.Matrix:
            {
                var cells = node.Children.Select((n, i) => Build(n, size, $"matrix row {i / node.Columns + 1}, column {i % node.Columns + 1}")).ToArray();
                var rows = cells.Length / node.Columns; var widths = new double[node.Columns]; var ascents = new double[rows]; var descents = new double[rows];
                for (var i = 0; i < cells.Length; i++) { widths[i % node.Columns] = Math.Max(widths[i % node.Columns], cells[i].Width); ascents[i / node.Columns] = Math.Max(ascents[i / node.Columns], cells[i].Ascent); descents[i / node.Columns] = Math.Max(descents[i / node.Columns], cells[i].Descent); }
                var padding = size * 0.45; var height = ascents.Sum() + descents.Sum() + gap * 2 * (rows - 1);
                var width = widths.Sum() + padding * (node.Columns - 1) + size;
                var result = new Box(width, height / 2 + size * 0.25, Math.Max(0, height / 2 - size * 0.25));
                var y = 0d;
                for (var r = 0; r < rows; r++)
                {
                    var x = size / 2;
                    for (var c = 0; c < node.Columns; c++) { var cell = cells[r * node.Columns + c]; result.Add(cell, x + (widths[c] - cell.Width) / 2, y + ascents[r] - cell.Ascent); x += widths[c] + padding; }
                    y += ascents[r] + descents[r] + gap * 2;
                }
                AddDelimiter(result, "[", 0, result.Height, size * 0.35, size);
                AddDelimiter(result, "]", result.Width - size * 0.35, result.Height, size * 0.35, size); return result;
            }
            case EquationKind.Nary:
            {
                var body = Child(0, name: "operand"); var lower = Child(1, small, "lower limit"); var upper = Child(2, small, "upper limit");
                var symbol = Text(node.Text, size * 1.7); var operatorWidth = Math.Max(symbol.Width, Math.Max(lower.Width, upper.Width));
                var operatorHeight = upper.Height + symbol.Height + lower.Height + gap;
                var ascent = Math.Max(upper.Height + gap / 2 + symbol.Ascent, body.Ascent);
                var result = new Box(operatorWidth + gap * 2 + body.Width, ascent, Math.Max(operatorHeight - ascent, body.Descent));
                result.Add(upper, (operatorWidth - upper.Width) / 2, 0); result.Add(symbol, (operatorWidth - symbol.Width) / 2, upper.Height + gap / 2);
                result.Add(lower, (operatorWidth - lower.Width) / 2, upper.Height + gap + symbol.Height);
                result.Add(body, operatorWidth + gap * 2, ascent - body.Ascent); return result;
            }
            case EquationKind.Accent:
            {
                var body = Child(0); var top = size * 0.35; var result = new Box(body.Width + gap, body.Ascent + top, body.Descent);
                result.Add(body, gap / 2, top);
                var y = top * 0.45;
                if (node.Text is "¯" or "→")
                {
                    result.Rule(rule, new(0, y), new(result.Width, y));
                    if (node.Text == "→") result.Rule(rule, new(result.Width - top * 0.6, 0), new(result.Width, y), new(result.Width - top * 0.6, top * 0.9));
                }
                else if (node.Text == "^") result.Rule(rule, new(0, top * 0.8), new(result.Width / 2, 0), new(result.Width, top * 0.8));
                else { var accent = Text(node.Text, size); result.Add(accent, (result.Width - accent.Width) / 2, -size * 0.2); }
                return result;
            }
            default: throw new InvalidDataException("Unsupported equation node.");
        }
    }

    private void AddDelimiter(Box target, string text, double x, double height, double width, double size)
    {
        var style = new TextStyle { FontFamily = "Times New Roman", FontSize = size }; var m = metrics.Measure(text, style);
        target.Glyphs.Add(new(text, style, x, 0, m.Ascent, width / Math.Max(1, m.Width), height / Math.Max(1, m.Ascent + m.Descent)));
    }
}
