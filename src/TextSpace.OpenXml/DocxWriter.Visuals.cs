using System.Globalization;
using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxWriter
{
    private static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    private static readonly XNamespace Ts = "urn:textspace:visual-placement:1";
    private const string PlacementExtension = "{9C83D9BA-40B7-4B29-99AC-91A70F3535B0}";
    private static long Emu(double points) => checked((long)Math.Round(points * 12700));
    private static string Invariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private XElement VisualObject(VisualBlock block)
    {
        VisualBlockRules.Validate(block);
        var id = ++_nextId;
        var cx = Emu(block.Width); var cy = Emu(block.Height);
        var transform = new XElement(A + "xfrm",
            new XAttribute("rot", (long)Math.Round(((block.Placement.Rotation % 360 + 360) % 360) * 60000)),
            new XAttribute("flipH", block.Placement.FlipHorizontal ? 1 : 0), new XAttribute("flipV", block.Placement.FlipVertical ? 1 : 0),
            new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy)));
        // Native offsets are measured from the aligned flow anchor. The extension
        // preserves that distinction; standard DrawingML remains fully editable.
        var placement = new XElement(A + "extLst", new XElement(A + "ext", new XAttribute("uri", PlacementExtension),
            new XElement(Ts + "placement", new XAttribute("x", Invariant(block.Placement.X)), new XAttribute("y", Invariant(block.Placement.Y)),
                new XAttribute("alignment", block.Alignment), new XAttribute("kind", block is ShapeBlock s ? s.Kind.ToString() : block is EquationBlock ? "Equation" : "Picture"))));
        XElement graphicData;
        string name;
        if (block is ImageBlock image)
        {
            var extension = image.ContentType == "image/jpeg" ? "jpg" : image.ContentType == "image/gif" ? "gif" : "png";
            var path = "media/image" + (_media.Count + 1) + "." + extension;
            var relationship = Relate("image", path); _media.Add((path, image.Data, image.ContentType));
            var crop = image.Crop;
            var picture = new XElement(Pic + "pic",
                new XElement(Pic + "nvPicPr", new XElement(Pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", image.AltText)), new XElement(Pic + "cNvPicPr")),
                new XElement(Pic + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", relationship)),
                    new XElement(A + "srcRect", new XAttribute("l", (int)Math.Round(crop.Left * 100000)), new XAttribute("t", (int)Math.Round(crop.Top * 100000)),
                        new XAttribute("r", (int)Math.Round(crop.Right * 100000)), new XAttribute("b", (int)Math.Round(crop.Bottom * 100000))),
                    new XElement(A + "stretch", new XElement(A + "fillRect"))),
                new XElement(Pic + "spPr", transform, new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst")), placement));
            graphicData = new(A + "graphicData", new XAttribute("uri", Pic.NamespaceName), picture); name = image.AltText;
        }
        else
        {
            var shape = block as ShapeBlock;
            var equation = block as EquationBlock;
            var preset = shape?.Kind switch { ShapeKind.Ellipse => "ellipse", ShapeKind.Diamond => "diamond", ShapeKind.Triangle => "triangle", ShapeKind.RoundedRectangle => "roundRect", ShapeKind.Line or ShapeKind.Arrow => "line", _ => "rect" };
            var adjustments = new XElement(A + "avLst");
            if (shape?.Kind == ShapeKind.RoundedRectangle)
                adjustments.Add(new XElement(A + "gd", new XAttribute("name", "adj"), new XAttribute("fmla", "val " + (int)Math.Clamp(shape.CornerRadius / Math.Min(shape.Width, shape.Height) * 100000, 0, 50000))));
            var properties = new XElement(Wps + "spPr", transform, new XElement(A + "prstGeom", new XAttribute("prst", preset), adjustments));
            properties.Add(shape?.Fill is { } fill ? Solid(fill) : new XElement(A + "noFill"));
            var line = new XElement(A + "ln", new XAttribute("w", Emu(shape?.StrokeWidth ?? 0)));
            line.Add(shape is { StrokeWidth: > 0 } ? Solid(shape.Stroke) : new XElement(A + "noFill"));
            if (shape?.Kind == ShapeKind.Arrow) line.Add(new XElement(A + "tailEnd", new XAttribute("type", "triangle"), new XAttribute("w", "med"), new XAttribute("len", "med")));
            properties.Add(line, placement);
            var content = E("txbxContent");
            if (equation is not null)
                content.Add(E("p", E("pPr", E("jc", V("center"))), OfficeMathCodec.Write(equation.Root, new() { FontFamily = "Cambria Math", FontSize = equation.FontSize, Color = equation.Color })));
            else
                foreach (var paragraph in (shape?.Text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    content.Add(E("p", E("pPr", E("spacing", new XAttribute(W + "after", 0))), Run(paragraph, shape?.TextStyle ?? new())));
            var padding = Emu(shape?.Padding ?? 0);
            var body = new XElement(Wps + "bodyPr", new XAttribute("lIns", padding), new XAttribute("tIns", padding), new XAttribute("rIns", padding), new XAttribute("bIns", padding),
                new XAttribute("anchor", shape?.VerticalAlignment switch { CellVerticalAlignment.Top => "t", CellVerticalAlignment.Bottom => "b", _ => "ctr" }),
                new XElement(A + "noAutofit"));
            var visual = new XElement(Wps + "wsp", new XElement(Wps + "cNvSpPr", new XAttribute("txBox", 1)), properties,
                new XElement(Wps + "txbx", content), body);
            graphicData = new(A + "graphicData", new XAttribute("uri", Wps.NamespaceName), visual);
            name = equation is not null ? "Equation" : shape?.Kind.ToString() ?? "Shape";
        }
        var frame = new XElement(Wp + (block.Placement.Floating ? "anchor" : "inline"),
            new XAttribute("distT", 0), new XAttribute("distB", 0), new XAttribute("distL", 0), new XAttribute("distR", 0));
        if (block.Placement.Floating)
        {
            frame.Add(new XAttribute("simplePos", 0), new XAttribute("relativeHeight", id), new XAttribute("behindDoc", 0),
                new XAttribute("locked", 0), new XAttribute("layoutInCell", 1), new XAttribute("allowOverlap", 1));
            frame.Add(new XElement(Wp + "simplePos", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Wp + "positionH", new XAttribute("relativeFrom", "column"), new XElement(Wp + "posOffset", Emu(AlignedLeft(block) + block.Placement.X))),
                new XElement(Wp + "positionV", new XAttribute("relativeFrom", "paragraph"), new XElement(Wp + "posOffset", Emu(block.Placement.Y))));
        }
        frame.Add(new XElement(Wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)));
        if (block.Placement.Floating) frame.Add(new XElement(Wp + "wrapNone"));
        frame.Add(new XElement(Wp + "docPr", new XAttribute("id", id), new XAttribute("name", name), new XAttribute("descr", name)),
            new XElement(Wp + "cNvGraphicFramePr"), new XElement(A + "graphic", graphicData));
        // Inline offsets map to the owning paragraph; negative vertical offsets
        // cannot be expressed as before-spacing and stay in the native extension.
        var pPr = E("pPr", E("spacing", new XAttribute(W + "before", Twips(Math.Max(0, block.Placement.Floating ? 0 : block.Placement.Y))), new XAttribute(W + "after", 0)),
            E("ind", new XAttribute(W + "left", Twips(block.Placement.Floating ? 0 : block.Placement.X))), E("jc", V(block.Alignment.ToString().ToLowerInvariant())));
        return E("p", pPr, E("r", E("drawing", frame)));
    }
    private double AlignedLeft(VisualBlock block) => block.Alignment switch
    {
        TextAlignment.Center => Math.Max(0, (_availableTableWidth - block.Width) / 2),
        TextAlignment.Right => Math.Max(0, _availableTableWidth - block.Width), _ => 0
    };
    private static XElement Solid(string hex)
    {
        var rgb = hex.Length == 9 ? hex[3..] : hex[1..];
        var color = new XElement(A + "srgbClr", new XAttribute("val", rgb));
        if (hex.Length == 9) color.Add(new XElement(A + "alpha", new XAttribute("val", (int)Math.Round(Convert.ToInt32(hex.Substring(1, 2), 16) / 255d * 100000))));
        return new XElement(A + "solidFill", color);
    }
}
