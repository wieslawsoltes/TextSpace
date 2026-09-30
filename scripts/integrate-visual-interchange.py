"""One-time, guarded source integration. Removed from the resulting source tree."""
from pathlib import Path
changes = {}
def replace(path, old, new, count=1):
    text = changes.get(path, Path(path).read_text())
    if text.count(old) != count:
        raise RuntimeError((path, text.count(old), old[:120]))
    changes[path] = text.replace(old, new)

replace('src/TextSpace.OpenXml/DocxWriter.Content.cs',
    'case ImageBlock image: yield return Picture(image); break;',
    'case VisualBlock visual: yield return VisualObject(visual); break;')
replace('src/TextSpace.OpenXml/DocxReader.cs',
    '                var hasPageBreak = element.Descendants(W + "br").Any',
    '                if (HasVisualContent(element))\n                {\n                    foreach (var visualBlock in ReadVisualParagraph(element)) yield return visualBlock;\n                }\n                else\n                {\n                var hasPageBreak = element.Descendants(W + "br").Any')
replace('src/TextSpace.OpenXml/DocxReader.cs',
    '                if (parent.Name == W + "body" && element.Element(W + "pPr")?.Element(W + "sectPr") is not null)',
    '                }\n                if (parent.Name == W + "body" && element.Element(W + "pPr")?.Element(W + "sectPr") is not null)')
replace('src/TextSpace.OpenXml/DocxReader.cs',
    '        if (drawing.Name == Wp + "anchor") Warn("Floating pictures are imported as in-flow picture blocks.");\n', '')
replace('src/TextSpace.OpenXml/DocxWriter.Visuals.cs',
    'new XAttribute("alignment", block.Alignment), new XAttribute("kind",',
    'new XAttribute("standardX", Invariant(block.Placement.Floating ? AlignedLeft(block) + block.Placement.X : Twips(block.Placement.X) / 20d)),\n                new XAttribute("standardY", Invariant(block.Placement.Floating ? block.Placement.Y : Twips(Math.Max(0, block.Placement.Y)) / 20d)),\n                new XAttribute("alignment", block.Alignment), new XAttribute("kind",')
for path, text in changes.items():
    Path(path).write_text(text)
Path('scripts/integrate-visual-interchange.py').unlink()
Path('.github/workflows/visual-interchange-integration.yml').unlink()
print('Integrated editable OMML and DrawingML sources. Temporary files removed.')
