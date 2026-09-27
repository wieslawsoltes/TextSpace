using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxReader
{
    private void LoadStyles()
    {
        var xml = RelatedXml("styles"); if (xml?.Root is null) return;
        _defaultStyle = ReadTextStyle(xml.Root.Element(W + "docDefaults")?.Element(W + "rPrDefault")?.Element(W + "rPr"), new());
        var definitions = xml.Root.Elements(W + "style").Where(e => e.Attribute(W + "styleId") is not null).GroupBy(e => (string)e.Attribute(W + "styleId")!).ToDictionary(g => g.Key, g => g.First());
        (TextStyle, ParagraphFormat) ResolveStyle(string id, HashSet<string> visiting)
        {
            if (_styles.TryGetValue(id, out var value)) return value;
            if (visiting.Count > 32 || !visiting.Add(id) || !definitions.TryGetValue(id, out var element)) return (_defaultStyle, new());
            var parent = Val(element.Element(W + "basedOn")); var basis = parent is null ? (_defaultStyle, new ParagraphFormat()) : ResolveStyle(parent, visiting);
            var character = ReadTextStyle(element.Element(W + "rPr"), basis.Item1); var paragraph = ReadParagraphFormat(element.Element(W + "pPr"), basis.Item2);
            paragraph = paragraph with { StyleName = Val(element.Element(W + "name")) ?? id };
            _styles[id] = (character, paragraph); visiting.Remove(id); return (character, paragraph);
        }
        foreach (var id in definitions.Keys) ResolveStyle(id, []);
    }
    private void LoadNumbering()
    {
        var root = RelatedXml("numbering")?.Root; if (root is null) return;
        var abstracts = root.Elements(W + "abstractNum").Where(e => e.Attribute(W + "abstractNumId") is not null).ToDictionary(e => (string)e.Attribute(W + "abstractNumId")!, e => Val(e.Element(W + "lvl")?.Element(W + "numFmt")) == "bullet" ? ListKind.Bullet : ListKind.Number);
        foreach (var num in root.Elements(W + "num")) { var id = (string?)num.Attribute(W + "numId"); var abstractId = Val(num.Element(W + "abstractNumId")); if (id is not null && abstractId is not null) _numbering[id] = abstracts.GetValueOrDefault(abstractId, ListKind.Number); }
    }
    private static TextStyle ReadTextStyle(XElement? properties, TextStyle basis)
    {
        if (properties is null) return basis;
        var font = properties.Element(W + "rFonts"); var color = Val(properties.Element(W + "color")); var highlight = Val(properties.Element(W + "highlight")); var shading = (string?)properties.Element(W + "shd")?.Attribute(W + "fill"); var vertical = Val(properties.Element(W + "vertAlign"));
        var highlights = new Dictionary<string, string> { ["yellow"] = "#FFFF00", ["green"] = "#00FF00", ["cyan"] = "#00FFFF", ["magenta"] = "#FF00FF", ["blue"] = "#0000FF", ["red"] = "#FF0000", ["darkYellow"] = "#808000", ["lightGray"] = "#D3D3D3" };
        return basis with
        {
            FontFamily = (string?)font?.Attribute(W + "ascii") ?? (string?)font?.Attribute(W + "hAnsi") ?? basis.FontFamily,
            FontSize = Math.Clamp(Number(Val(properties.Element(W + "sz")), basis.FontSize * 2) / 2, 1, 400),
            Bold = Flag(properties.Element(W + "b"), basis.Bold), Italic = Flag(properties.Element(W + "i"), basis.Italic), Underline = Flag(properties.Element(W + "u"), basis.Underline), StrikeThrough = Flag(properties.Element(W + "strike"), basis.StrikeThrough),
            Color = color is { Length: 6 } && color.All(Uri.IsHexDigit) ? "#" + color : basis.Color,
            Highlight = shading is { Length: 6 } && shading.All(Uri.IsHexDigit) ? "#" + shading : highlight is not null && highlights.TryGetValue(highlight, out var h) ? h : basis.Highlight,
            Superscript = vertical is null ? basis.Superscript : vertical == "superscript", Subscript = vertical is null ? basis.Subscript : vertical == "subscript"
        };
    }
    private static ParagraphFormat ReadParagraphFormat(XElement? properties, ParagraphFormat basis)
    {
        if (properties is null) return basis;
        var spacing = properties.Element(W + "spacing"); var indent = properties.Element(W + "ind"); var alignment = Val(properties.Element(W + "jc")); var outline = Val(properties.Element(W + "outlineLvl")); var shade = (string?)properties.Element(W + "shd")?.Attribute(W + "fill");
        var firstLine = indent?.Attribute(W + "firstLine"); var hanging = indent?.Attribute(W + "hanging");
        return basis with
        {
            Alignment = alignment switch { "center" => TextAlignment.Center, "right" or "end" => TextAlignment.Right, "both" or "distribute" => TextAlignment.Justify, "left" or "start" => TextAlignment.Left, _ => basis.Alignment },
            LeftIndent = Number((string?)indent?.Attribute(W + "left"), basis.LeftIndent * 20) / 20, RightIndent = Number((string?)indent?.Attribute(W + "right"), basis.RightIndent * 20) / 20,
            FirstLineIndent = firstLine is not null ? Number((string?)firstLine) / 20 : hanging is not null ? -Number((string?)hanging) / 20 : basis.FirstLineIndent,
            SpaceBefore = Math.Max(0, Number((string?)spacing?.Attribute(W + "before"), basis.SpaceBefore * 20) / 20), SpaceAfter = Math.Max(0, Number((string?)spacing?.Attribute(W + "after"), basis.SpaceAfter * 20) / 20),
            LineSpacing = (string?)spacing?.Attribute(W + "lineRule") is null or "auto" ? Math.Clamp(Number((string?)spacing?.Attribute(W + "line"), basis.LineSpacing * 240) / 240, 0.5, 10) : basis.LineSpacing,
            KeepWithNext = Flag(properties.Element(W + "keepNext"), basis.KeepWithNext), PageBreakBefore = Flag(properties.Element(W + "pageBreakBefore"), basis.PageBreakBefore),
            OutlineLevel = outline is not null ? Math.Clamp((int)Number(outline) + 1, 0, 9) : basis.OutlineLevel,
            ListLevel = (int)Number(Val(properties.Element(W + "numPr")?.Element(W + "ilvl")), basis.ListLevel), BorderBottom = properties.Element(W + "pBdr")?.Element(W + "bottom") is not null || basis.BorderBottom,
            Shading = shade is { Length: 6 } && shade.All(Uri.IsHexDigit) ? "#" + shade : basis.Shading
        };
    }
    private static PageSettings ReadPage(XElement? section)
    {
        var size = section?.Element(W + "pgSz"); var margins = section?.Element(W + "pgMar"); var columns = section?.Element(W + "cols");
        return new()
        {
            Width = Number((string?)size?.Attribute(W + "w"), 12240) / 20, Height = Number((string?)size?.Attribute(W + "h"), 15840) / 20,
            MarginLeft = Number((string?)margins?.Attribute(W + "left"), 1440) / 20, MarginRight = Number((string?)margins?.Attribute(W + "right"), 1440) / 20,
            MarginTop = Number((string?)margins?.Attribute(W + "top"), 1440) / 20, MarginBottom = Number((string?)margins?.Attribute(W + "bottom"), 1440) / 20,
            HeaderDistance = Number((string?)margins?.Attribute(W + "header"), 600) / 20, FooterDistance = Number((string?)margins?.Attribute(W + "footer"), 600) / 20,
            Columns = Math.Clamp((int)Number((string?)columns?.Attribute(W + "num"), 1), 1, 3), ColumnGap = Math.Max(0, Number((string?)columns?.Attribute(W + "space"), 480) / 20)
        };
    }
}
