using System.Text;
using System.Xml.Linq;
using TextSpace.Core;
using TextSpace.Documents;
using static TextSpace.OpenXml.Ooxml;

namespace TextSpace.OpenXml;

public sealed partial class DocxReader
{
    private sealed class PendingField(int start, bool locked)
    {
        public int Start { get; } = start;
        public bool Locked { get; } = locked;
        public StringBuilder Code { get; } = new();
        public bool InResult { get; set; }
        public bool Nested { get; set; }
    }
    private readonly List<DocumentField> _importedFields = [];
    private SectionDefinition[] _sections = [];
    private SectionBreakKind[] _sectionKinds = [];
    private int _sectionCursor;
    private static bool FlagAttribute(XElement element, string name) => (string?)element.Attribute(W + name) is "true" or "1" or "on";

    private bool ReadFieldToken(XElement token, Paragraph paragraph, Stack<PendingField> stack)
    {
        if (token.Name == W + "fldChar")
        {
            switch ((string?)token.Attribute(W + "fldCharType"))
            {
                case "begin":
                    if (stack.Count >= 64) throw new InvalidDataException("Fields are nested too deeply.");
                    foreach (var enclosing in stack) enclosing.Nested = true;
                    stack.Push(new(_textPosition + paragraph.Length, FlagAttribute(token, "fldLock")) { Nested = stack.Count > 0 });
                    break;
                case "separate": if (stack.TryPeek(out var current)) current.InResult = true; break;
                case "end":
                    if (stack.TryPop(out var field))
                    {
                        if (!field.Nested && field.InResult) AddImportedField(field.Code.ToString(), field.Start, paragraph, field.Locked);
                        else Warn("Nested or incomplete fields retain cached text without overlapping field behavior.");
                    }
                    break;
            }
            return true;
        }
        if (token.Name == W + "instrText")
        {
            if (stack.TryPeek(out var field) && !field.InResult)
            {
                if (field.Code.Length + token.Value.Length > 1024) throw new InvalidDataException("A field instruction exceeds the safety limit.");
                field.Code.Append(token.Value);
            }
            return true;
        }
        return false;
    }

    private void AddImportedField(string instruction, int start, Paragraph paragraph, bool locked)
    {
        instruction = instruction.Trim();
        if (instruction.Length is 0 or > 1024 || instruction.Any(char.IsControl)) { Warn("An invalid field instruction was omitted; cached text was retained."); return; }
        if (_importedFields.Count >= 10_000) throw new InvalidDataException("Too many fields.");
        if (start == _textPosition + paragraph.Length) paragraph.Runs.Add(new("\u200B", paragraph.DefaultStyle));
        _importedFields.Add(new() { Instruction = instruction, Start = start, End = _textPosition + paragraph.Length, Locked = locked });
        try { if (!FieldInstruction.Parse(instruction).Supported) Warn("Unsupported fields are retained as inert instructions and cached results; no external content is executed or fetched."); }
        catch (FormatException) { Warn("An unsupported field instruction was retained without execution."); }
    }

    private void ReadSectionDefinitions(XElement body)
    {
        var properties = body.Elements(W + "p").Select(p => p.Element(W + "pPr")?.Element(W + "sectPr")).Where(p => p is not null)
            .Append(body.Element(W + "sectPr")).ToArray();
        if (properties.Length > 1001) throw new InvalidDataException("Too many sections.");
        var evenOdd = Flag(RelatedXml("settings")?.Root?.Element(W + "evenAndOddHeaders"), false);
        _sections = properties.Select(p =>
        {
            string? Story(string kind, string variant)
            {
                var reference = p?.Elements(W + kind + "Reference").FirstOrDefault(e => (string?)e.Attribute(W + "type") == variant);
                return reference is null ? null : ReadHeaderFooter(reference);
            }
            var number = p?.Element(W + "pgNumType");
            var restart = (string?)number?.Attribute(W + "start");
            return new SectionDefinition
            {
                Page = ReadPage(p), Header = Story("header", "default"), Footer = Story("footer", "default"),
                Options = new()
                {
                    DifferentFirstPage = Flag(p?.Element(W + "titlePg"), false), DifferentOddAndEven = evenOdd,
                    FirstHeader = Story("header", "first"), FirstFooter = Story("footer", "first"), EvenHeader = Story("header", "even"), EvenFooter = Story("footer", "even"),
                    PageNumberStart = restart is null ? null : Math.Clamp((int)Number(restart, 1), 1, 1_000_000),
                    NumberStyle = (string?)number?.Attribute(W + "fmt") switch
                    {
                        "upperRoman" => PageNumberStyle.UpperRoman, "lowerRoman" => PageNumberStyle.LowerRoman,
                        "upperLetter" => PageNumberStyle.UpperLetter, "lowerLetter" => PageNumberStyle.LowerLetter, _ => PageNumberStyle.Decimal
                    }
                }
            };
        }).ToArray();
        _sectionKinds = properties.Select(p =>
        {
            var type = Val(p?.Element(W + "type"));
            return type switch
            {
                "continuous" => SectionBreakKind.Continuous, "nextColumn" => SectionBreakKind.NextColumn,
                "oddPage" => SectionBreakKind.OddPage, "evenPage" => SectionBreakKind.EvenPage, _ => SectionBreakKind.NextPage
            };
        }).ToArray();
    }
}
