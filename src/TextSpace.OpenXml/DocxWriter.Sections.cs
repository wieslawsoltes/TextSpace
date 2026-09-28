using System.Xml.Linq;
using TextSpace.Core;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxWriter
{
    private readonly List<(string Path, string Kind, XDocument Xml)> _storyParts = [];
    private double _availableTableWidth;

    private XElement WriteSectionBody(DocumentModel document)
    {
        var body = E("body"); var current = DocumentSections.First(document); SectionDefinition? previous = null;
        var kind = SectionBreakKind.NextPage;
        var evenOdd = DocumentSections.Definitions(document).Any(s => s.Options.DifferentOddAndEven);
        foreach (var block in document.Blocks)
        {
            if (block is not SectionBreakBlock boundary) { _availableTableWidth = current.Page.ColumnWidth; body.Add(Blocks([block])); continue; }
            var properties = WriteSectionProperties(current, kind, previous, evenOdd);
            var paragraph = body.Elements().LastOrDefault();
            if (paragraph?.Name != W + "p") { paragraph = E("p"); body.Add(paragraph); }
            var paragraphProperties = paragraph.Element(W + "pPr");
            if (paragraphProperties is null) { paragraphProperties = E("pPr"); paragraph.AddFirst(paragraphProperties); }
            paragraphProperties.Add(properties);
            previous = DocumentSections.Resolve(current, previous); current = boundary.Section; kind = boundary.Kind;
        }
        body.Add(WriteSectionProperties(current, kind, previous, evenOdd));
        return body;
    }

    private XElement WriteSectionProperties(SectionDefinition section, SectionBreakKind kind, SectionDefinition? previous, bool globalEvenOdd)
    {
        var page = section.Page; var options = section.Options; var effective = DocumentSections.Resolve(section, previous);
        XElement? Story(string storyKind, string variant, string? text)
        {
            if (text is null) return null; // Preserve the actual link-to-previous semantics, including explicitly blank stories.
            var path = storyKind + (_storyParts.Count + 1) + ".xml";
            _storyParts.Add((path, storyKind, new(E(storyKind == "header" ? "hdr" : "ftr", HeaderFooter(text, storyKind == "footer")))));
            return E(storyKind + "Reference", new XAttribute(W + "type", variant), new XAttribute(R + "id", Relate(storyKind, path)));
        }
        return E("sectPr",
            Story("header", "default", section.Header), Story("header", "first", options.FirstHeader),
            Story("header", "even", globalEvenOdd && !options.DifferentOddAndEven ? effective.Header : options.EvenHeader),
            Story("footer", "default", section.Footer), Story("footer", "first", options.FirstFooter),
            Story("footer", "even", globalEvenOdd && !options.DifferentOddAndEven ? effective.Footer : options.EvenFooter),
            E("type", V(kind switch { SectionBreakKind.OddPage => "oddPage", SectionBreakKind.EvenPage => "evenPage", SectionBreakKind.Continuous => "continuous", SectionBreakKind.NextColumn => "nextColumn", _ => "nextPage" })),
            E("pgSz", new XAttribute(W + "w", Twips(page.Width)), new XAttribute(W + "h", Twips(page.Height)), page.Width > page.Height ? new XAttribute(W + "orient", "landscape") : null),
            E("pgMar", new XAttribute(W + "top", Twips(page.MarginTop)), new XAttribute(W + "right", Twips(page.MarginRight)), new XAttribute(W + "bottom", Twips(page.MarginBottom)), new XAttribute(W + "left", Twips(page.MarginLeft)), new XAttribute(W + "header", Twips(page.HeaderDistance)), new XAttribute(W + "footer", Twips(page.FooterDistance)), new XAttribute(W + "gutter", 0)),
            E("pgNumType", new XAttribute(W + "fmt", options.NumberStyle switch
            {
                PageNumberStyle.UpperRoman => "upperRoman", PageNumberStyle.LowerRoman => "lowerRoman",
                PageNumberStyle.UpperLetter => "upperLetter", PageNumberStyle.LowerLetter => "lowerLetter", _ => "decimal"
            }), options.PageNumberStart is { } start ? new XAttribute(W + "start", start) : null),
            E("cols", new XAttribute(W + "num", page.Columns), new XAttribute(W + "space", Twips(page.ColumnGap))),
            options.DifferentFirstPage ? E("titlePg") : null);
    }
}
