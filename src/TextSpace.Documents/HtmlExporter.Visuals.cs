using System.Xml.Linq;
using TextSpace.Core;

namespace TextSpace.Documents;

public static partial class HtmlExporter
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace MathMl = "http://www.w3.org/1998/Math/MathML";
    private static readonly XNamespace Html = "http://www.w3.org/1999/xhtml";
    private static string VisualColor(string value)
    {
        if (!VisualBlockRules.IsColor(value)) return "#202020";
        if (value.Length == 7) return value;
        return "rgba(" + Convert.ToInt32(value.Substring(3, 2), 16) + "," + Convert.ToInt32(value.Substring(5, 2), 16) + "," + Convert.ToInt32(value.Substring(7, 2), 16)
            + "," + N(Convert.ToInt32(value.Substring(1, 2), 16) / 255d) + ")";
    }
    private static string ExportVisual(VisualBlock block)
    {
        var w = block.Width; var h = block.Height; XElement content;
        if (block is EquationBlock equation)
        {
            content = new(MathMl + "math", new XAttribute("display", "block"), new XAttribute("aria-label", equation.Root.ToLinearText()),
                new XAttribute("style", "font-size:" + N(equation.FontSize) + "pt;color:" + VisualColor(equation.Color)), MathContent(equation.Root));
        }
        else
        {
            content = new(Svg + "svg", new XAttribute("viewBox", $"0 0 {N(w)} {N(h)}"), new XAttribute("width", "100%"), new XAttribute("height", "100%"),
                new XAttribute("role", "img"), new XAttribute("aria-label", block is ShapeBlock named ? named.Text : ((ImageBlock)block).AltText));
            if (block is ImageBlock image)
            {
                if (image.ContentType is not ("image/png" or "image/jpeg" or "image/gif")) throw new InvalidDataException("Unsupported image MIME type for HTML export.");
                var crop = image.Crop; var iw = w / (1 - crop.Left - crop.Right); var ih = h / (1 - crop.Top - crop.Bottom);
                content.Add(new XElement(Svg + "image", new XAttribute("x", N(-crop.Left * iw)), new XAttribute("y", N(-crop.Top * ih)), new XAttribute("width", N(iw)), new XAttribute("height", N(ih)),
                    new XAttribute("preserveAspectRatio", "none"), new XAttribute("href", "data:" + image.ContentType + ";base64," + Convert.ToBase64String(image.Data))));
            }
            else if (block is ShapeBlock shape)
            {
                var geometry = shape.Kind switch
                {
                    ShapeKind.Ellipse => new XElement(Svg + "ellipse", new XAttribute("cx", N(w / 2)), new XAttribute("cy", N(h / 2)), new XAttribute("rx", N(w / 2)), new XAttribute("ry", N(h / 2))),
                    ShapeKind.Diamond => new XElement(Svg + "polygon", new XAttribute("points", $"{N(w / 2)},0 {N(w)},{N(h / 2)} {N(w / 2)},{N(h)} 0,{N(h / 2)}")),
                    ShapeKind.Triangle => new XElement(Svg + "polygon", new XAttribute("points", $"{N(w / 2)},0 {N(w)},{N(h)} 0,{N(h)}")),
                    ShapeKind.Line or ShapeKind.Arrow => new XElement(Svg + "path", new XAttribute("d", $"M0 {N(h / 2)} H{N(w)}")),
                    _ => new XElement(Svg + "rect", new XAttribute("width", N(w)), new XAttribute("height", N(h)), new XAttribute("rx", shape.Kind == ShapeKind.RoundedRectangle ? N(Math.Min(shape.CornerRadius, Math.Min(w, h) / 2)) : "0"))
                };
                geometry.Add(new XAttribute("fill", shape.Fill is not null && shape.Kind is not (ShapeKind.Line or ShapeKind.Arrow) ? VisualColor(shape.Fill) : "none"),
                    new XAttribute("stroke", VisualColor(shape.Stroke)), new XAttribute("stroke-width", N(shape.StrokeWidth)));
                content.Add(geometry);
                if (shape.Kind == ShapeKind.Arrow)
                {
                    var size = Math.Min(Math.Min(w / 3, h / 2), Math.Max(10, shape.StrokeWidth * 4));
                    content.Add(new XElement(Svg + "path", new XAttribute("d", $"M{N(w - size)} {N(h / 2 - size / 2)} L{N(w)} {N(h / 2)} L{N(w - size)} {N(h / 2 + size / 2)}"),
                        new XAttribute("fill", "none"), new XAttribute("stroke", VisualColor(shape.Stroke)), new XAttribute("stroke-width", N(shape.StrokeWidth))));
                }
                if (shape.Text.Length > 0)
                {
                    var s = shape.TextStyle;
                    var css = "height:100%;box-sizing:border-box;overflow:hidden;display:flex;flex-direction:column;justify-content:"
                        + (shape.VerticalAlignment == CellVerticalAlignment.Top ? "flex-start" : shape.VerticalAlignment == CellVerticalAlignment.Bottom ? "flex-end" : "center")
                        + ";white-space:pre-wrap;overflow-wrap:anywhere;padding:" + N(shape.Padding) + "px;font-family:" + Font(s.FontFamily)
                        + ";font-size:" + N(s.FontSize) + "px;color:" + VisualColor(s.Color) + ";font-weight:" + (s.Bold ? "bold" : "normal") + ";font-style:" + (s.Italic ? "italic" : "normal")
                        + ";text-decoration:" + (s.Underline ? "underline " : "") + (s.StrikeThrough ? "line-through" : "") + ";";
                    content.Add(new XElement(Svg + "foreignObject", new XAttribute("width", N(w)), new XAttribute("height", N(h)),
                        new XElement(Html + "div", new XAttribute("style", css), shape.Text)));
                }
            }
        }
        var alignment = block.Alignment == TextAlignment.Left ? "margin-right:auto;" : block.Alignment == TextAlignment.Right ? "margin-left:auto;" : "margin-left:auto;margin-right:auto;";
        var placement = block.Placement;
        var style = "width:" + N(w) + "pt;height:" + N(h) + "pt;overflow:hidden;display:flex;align-items:center;justify-content:center;break-inside:avoid;"
            + alignment + (placement.Floating ? "position:absolute;z-index:1;" : "position:relative;margin-bottom:8pt;")
            + "transform:translate(" + N(placement.X) + "pt," + N(placement.Y) + "pt) rotate(" + N(placement.Rotation) + "deg) scale(" + (placement.FlipHorizontal ? "-1" : "1") + "," + (placement.FlipVertical ? "-1" : "1") + ");";
        return new XElement("div", new XAttribute("data-textspace-object", block is EquationBlock ? "equation" : block is ShapeBlock ? "shape" : "picture"), new XAttribute("style", style), content).ToString(SaveOptions.DisableFormatting);
    }
    private static XElement MathContent(EquationNode node)
    {
        XElement E(string name, params object[] children) => new(MathMl + name, children);
        XElement C(int index) => MathContent(node.Children[index]);
        return node.Kind switch
        {
            EquationKind.Text => E("mtext", node.Text),
            EquationKind.Row => E("mrow", node.Children.Select(MathContent)),
            EquationKind.Fraction => E("mfrac", C(0), C(1)),
            EquationKind.Radical => node.Children[1].ToLinearText().Length == 0 ? E("msqrt", C(0)) : E("mroot", C(0), C(1)),
            EquationKind.Superscript => E("msup", C(0), C(1)),
            EquationKind.Subscript => E("msub", C(0), C(1)),
            EquationKind.SubSuperscript => E("msubsup", C(0), C(1), C(2)),
            EquationKind.Delimited => E("mrow", E("mo", new XAttribute("stretchy", "true"), node.Text[..1]), C(0), E("mo", new XAttribute("stretchy", "true"), node.Text[1..])),
            EquationKind.Matrix => E("mtable", node.Children.Chunk(node.Columns).Select(row => E("mtr", row.Select(n => E("mtd", MathContent(n)))))),
            EquationKind.Nary => E("mrow", E("munderover", E("mo", node.Text), C(1), C(2)), C(0)),
            EquationKind.Accent => E("mover", new XAttribute("accent", "true"), C(0), E("mo", node.Text)),
            _ => throw new InvalidDataException("Unsupported equation structure.")
        };
    }
}
