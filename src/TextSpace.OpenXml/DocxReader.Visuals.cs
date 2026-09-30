using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxReader
{
    private static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    private static readonly XNamespace Ts = "urn:textspace:visual-placement:1";
    private const string PlacementExtension = "{9C83D9BA-40B7-4B29-99AC-91A70F3535B0}";
    private static bool IsMath(XElement e) => e.Name == OfficeMathCodec.Namespace + "oMath" || e.Name == OfficeMathCodec.Namespace + "oMathPara";
    private static bool HasVisualContent(XElement paragraph) => paragraph.Descendants().Any(e => (IsMath(e) || e.Name == W + "drawing") && !e.Ancestors(W + "del").Any());
    private sealed record VisualPart(XElement? Text, VisualBlock? Visual);

    private IEnumerable<Block> ReadVisualParagraph(XElement paragraph)
    {
        var pending = new List<XElement>();
        var count = 0;
        IEnumerable<VisualPart> Split(XElement element, int depth)
        {
            if (depth > 128 || ++count > 100000) throw new InvalidDataException("Drawing paragraph exceeds the structural import budget.");
            if (element.Name == W + "del") yield break;
            if (IsMath(element)) { yield return new(null, ReadEquation(element)); yield break; }
            if (element.Name == W + "drawing")
            {
                foreach (var frame in element.Elements().Where(e => e.Name == Wp + "inline" || e.Name == Wp + "anchor"))
                    if (ReadVisualFrame(frame, paragraph) is { } visual) yield return new(null, visual);
                yield break;
            }
            if (!HasVisualContent(element)) { yield return new(new XElement(element), null); yield break; }
            var properties = element.Elements().Where(e => e.Name == W + "rPr" || e.Name == W + "sdtPr").ToArray();
            var children = new List<XElement>();
            XElement Fragment() => new(element.Name, element.Attributes(), properties.Select(e => new XElement(e)), children);
            foreach (var child in element.Elements().Where(e => !properties.Contains(e)))
            {
                foreach (var part in Split(child, depth + 1))
                {
                    if (part.Visual is not null)
                    {
                        if (children.Count > 0) { yield return new(Fragment(), null); children = []; }
                        yield return part;
                    }
                    else if (part.Text is not null) children.Add(part.Text);
                }
            }
            if (children.Count > 0) yield return new(Fragment(), null);
        }
        XElement TextParagraph() => new(paragraph.Name, paragraph.Attributes(), paragraph.Element(W + "pPr") is { } properties ? new XElement(properties) : null, pending);
        bool HasParagraphText() => pending.Any(e => e.DescendantsAndSelf().Any(n => n.Name == W + "t" || n.Name == W + "fldChar" || n.Name == W + "fldSimple"
            || n.Name == W + "tab" || n.Name == W + "br" || n.Name == W + "bookmarkStart" || n.Name == W + "bookmarkEnd" || n.Name == W + "commentRangeStart" || n.Name == W + "commentRangeEnd"));
        var mixedText = paragraph.Elements().Where(e => e.Name != W + "pPr").Any(e => !HasVisualContent(e) && e.DescendantsAndSelf(W + "t").Any());
        if (mixedText) Warn("Inline drawing or equation content is represented as editable blocks; surrounding text retains its order but may occupy separate paragraphs.");
        foreach (var element in paragraph.Elements().Where(e => e.Name != W + "pPr"))
        {
            foreach (var part in Split(element, 0))
            {
                if (part.Visual is not null)
                {
                    if (HasParagraphText()) yield return ReadParagraph(TextParagraph());
                    pending.Clear();
                    if (paragraph.Ancestors(W + "tc").Any() && (part.Visual.Placement.Floating || part.Visual.Placement.Y < 0))
                    {
                        part.Visual.Placement = part.Visual.Placement with { Floating = false, Y = Math.Max(0, part.Visual.Placement.Y) };
                        Warn("Floating or negative-offset objects in table cells were normalized to in-flow objects.");
                    }
                    yield return part.Visual;
                }
                else if (part.Text is not null) pending.Add(part.Text);
            }
        }
        if (HasParagraphText()) yield return ReadParagraph(TextParagraph());
    }

    private EquationBlock ReadEquation(XElement math)
    {
        var style = ReadTextStyle(math.Descendants(W + "rPr").FirstOrDefault(), new() { FontFamily = "Cambria Math", FontSize = 18 });
        return new() { Root = OfficeMathCodec.Read(math, Warn), FontSize = Math.Clamp(style.FontSize, 6, 200), Color = style.Color };
    }

    private VisualBlock? ReadVisualFrame(XElement frame, XElement paragraph)
    {
        VisualBlock? result;
        XElement? properties;
        var shape = frame.Descendants(Wps + "wsp").FirstOrDefault();
        if (shape is null)
        {
            result = ReadPicture(frame);
            properties = frame.Descendants(Pic + "spPr").FirstOrDefault();
            if (result is ImageBlock image && frame.Descendants(A + "srcRect").FirstOrDefault() is { } crop)
                image.Crop = new() { Left = Number((string?)crop.Attribute("l")) / 100000, Top = Number((string?)crop.Attribute("t")) / 100000,
                    Right = Number((string?)crop.Attribute("r")) / 100000, Bottom = Number((string?)crop.Attribute("b")) / 100000 };
        }
        else
        {
            properties = shape.Element(Wps + "spPr");
            var body = shape.Element(Wps + "bodyPr");
            var math = shape.Element(Wps + "txbx")?.Descendants().FirstOrDefault(IsMath);
            if (math is not null) result = ReadEquation(math);
            else
            {
                var preset = (string?)properties?.Element(A + "prstGeom")?.Attribute("prst") ?? "rect";
                var kind = preset switch { "ellipse" => ShapeKind.Ellipse, "diamond" => ShapeKind.Diamond, "triangle" => ShapeKind.Triangle,
                    "roundRect" => ShapeKind.RoundedRectangle, "line" => properties?.Element(A + "ln")?.Element(A + "tailEnd") is not null ? ShapeKind.Arrow : ShapeKind.Line, _ => ShapeKind.Rectangle };
                if (preset is not ("rect" or "roundRect" or "ellipse" or "diamond" or "triangle" or "line"))
                    Warn("Unsupported shape geometry '" + preset + "' was normalized to a rectangle with editable text.");
                var text = shape.Element(Wps + "txbx")?.Element(W + "txbxContent");
                var content = text is null ? "" : string.Join("\n", text.Elements(W + "p").Select(p => string.Concat(p.Descendants().Select(e => e.Name == W + "t" ? e.Value : e.Name == W + "tab" ? "\t" : e.Name == W + "br" ? "\u2028" : ""))));
                var line = properties?.Element(A + "ln");
                result = new ShapeBlock
                {
                    Kind = kind, Text = content,
                    TextStyle = ReadTextStyle(text?.Descendants(W + "rPr").FirstOrDefault(), new()),
                    Fill = properties?.Element(A + "noFill") is not null ? null : ReadSolid(properties?.Element(A + "solidFill"), "#D9E5F5"),
                    Stroke = ReadSolid(line?.Element(A + "solidFill"), "#185ABD"),
                    StrokeWidth = line?.Element(A + "noFill") is not null ? 0 : Number((string?)line?.Attribute("w"), 19050) / 12700,
                    Padding = Number((string?)body?.Attribute("lIns"), 101600) / 12700,
                    VerticalAlignment = (string?)body?.Attribute("anchor") switch { "t" => CellVerticalAlignment.Top, "b" => CellVerticalAlignment.Bottom, _ => CellVerticalAlignment.Center }
                };
                if (text?.Descendants(W + "rPr").Skip(1).Any() == true)
                    Warn("Shape text retains a single character style in TextSpace; mixed run formatting is normalized.");
            }
        }
        if (result is null) return null;
        var extent = frame.Element(Wp + "extent");
        result.Width = Number((string?)extent?.Attribute("cx"), 3048000) / 12700;
        result.Height = Number((string?)extent?.Attribute("cy"), 1524000) / 12700;
        var transform = properties?.Element(A + "xfrm");
        var paragraphProperties = paragraph.Element(W + "pPr");
        result.Alignment = Val(paragraphProperties?.Element(W + "jc")) switch { "left" => TextAlignment.Left, "right" => TextAlignment.Right, _ => TextAlignment.Center };
        var floating = frame.Name == Wp + "anchor";
        var x = floating ? Number(frame.Element(Wp + "positionH")?.Element(Wp + "posOffset")?.Value) / 12700
            : Number((string?)paragraphProperties?.Element(W + "ind")?.Attribute(W + "left")) / 20;
        var y = floating ? Number(frame.Element(Wp + "positionV")?.Element(Wp + "posOffset")?.Value) / 12700
            : Number((string?)paragraphProperties?.Element(W + "spacing")?.Attribute(W + "before")) / 20;
        if (floating) result.Alignment = TextAlignment.Left;
        var native = properties?.Element(A + "extLst")?.Elements(A + "ext").FirstOrDefault(e => (string?)e.Attribute("uri") == PlacementExtension)?.Element(Ts + "placement");
        if (native is not null && Enum.TryParse<TextAlignment>((string?)native.Attribute("alignment"), out var alignment) && Enum.IsDefined(alignment))
        {
            var nativeX = Number((string?)native.Attribute("x"), double.NaN);
            var nativeY = Number((string?)native.Attribute("y"), double.NaN);
            // Only use offset semantics when the standard frame agrees with the
            // export snapshot. Editing the standard frame in Word takes precedence.
            var standardX = Number((string?)native.Attribute("standardX"), double.NaN);
            var standardY = Number((string?)native.Attribute("standardY"), double.NaN);
            if ((floating || result.Alignment == alignment) && double.IsFinite(nativeX + nativeY + standardX + standardY) && Math.Abs(x - standardX) < 0.051 && Math.Abs(y - standardY) < 0.051)
            { result.Alignment = alignment; x = nativeX; y = nativeY; }
            if (result is ShapeBlock rectangle && rectangle.Kind == ShapeKind.Rectangle && (string?)native.Attribute("kind") == "TextBox") rectangle.Kind = ShapeKind.TextBox;
        }
        result.Placement = new() { Floating = floating, X = x, Y = y, Rotation = Number((string?)transform?.Attribute("rot")) / 60000,
            FlipHorizontal = (string?)transform?.Attribute("flipH") is "1" or "true", FlipVertical = (string?)transform?.Attribute("flipV") is "1" or "true" };
        if (result is ShapeBlock rounded && rounded.Kind == ShapeKind.RoundedRectangle)
        {
            var formula = (string?)properties?.Element(A + "prstGeom")?.Element(A + "avLst")?.Elements(A + "gd").FirstOrDefault(e => (string?)e.Attribute("name") == "adj")?.Attribute("fmla");
            if (formula?.StartsWith("val ", StringComparison.Ordinal) == true) rounded.CornerRadius = Number(formula[4..], 16667) / 100000 * Math.Min(result.Width, result.Height);
        }
        VisualBlockRules.Validate(result); return result;
    }
    private static string ReadSolid(XElement? fill, string fallback)
    {
        var color = fill?.Element(A + "srgbClr"); var rgb = (string?)color?.Attribute("val");
        if (rgb is not { Length: 6 } || !rgb.All(Uri.IsHexDigit)) return fallback;
        var alpha = color?.Element(A + "alpha");
        if (alpha is null) return "#" + rgb;
        var value = (int)Math.Clamp(Math.Round(Number((string?)alpha.Attribute("val"), 100000) / 100000 * 255), 0, 255);
        return "#" + value.ToString("X2") + rgb;
    }
}
