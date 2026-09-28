using System.Globalization;
using System.Text;

namespace TextSpace.Core;

public enum SectionBreakKind { NextPage, OddPage, EvenPage, Continuous, NextColumn }
public enum PageNumberStyle { Decimal, UpperRoman, LowerRoman, UpperLetter, LowerLetter }

/// <summary>Null story variants inherit from the previous section; an empty string is explicitly blank.</summary>
public sealed record SectionOptions
{
    public bool DifferentFirstPage { get; init; }
    public bool DifferentOddAndEven { get; init; }
    public string? FirstHeader { get; init; }
    public string? FirstFooter { get; init; }
    public string? EvenHeader { get; init; }
    public string? EvenFooter { get; init; }
    public int? PageNumberStart { get; init; }
    public PageNumberStyle NumberStyle { get; init; }
}

public sealed record SectionDefinition
{
    public PageSettings Page { get; init; } = new();
    public string? Header { get; init; }
    public string? Footer { get; init; }
    public SectionOptions Options { get; init; } = new();
}

/// <summary>A boundary followed by a new section. It contributes no UTF-16 text coordinates.</summary>
public sealed class SectionBreakBlock : Block
{
    public SectionBreakKind Kind { get; set; }
    public SectionDefinition Section { get; set; } = new();
}

public sealed class ColumnBreakBlock : Block;

public static class DocumentSections
{
    public static IReadOnlyList<SectionDefinition> Definitions(DocumentModel document)
        => new[] { First(document) }.Concat(document.Blocks.OfType<SectionBreakBlock>().Select(b => b.Section)).ToArray();

    public static SectionDefinition First(DocumentModel document) => new()
    {
        Page = document.Page, Header = document.Header, Footer = document.Footer, Options = document.SectionOptions
    };

    public static SectionDefinition Resolve(SectionDefinition current, SectionDefinition? previous) => current with
    {
        Header = current.Header ?? previous?.Header ?? "",
        Footer = current.Footer ?? previous?.Footer ?? "",
        Options = current.Options with
        {
            FirstHeader = current.Options.FirstHeader ?? previous?.Options.FirstHeader ?? "",
            FirstFooter = current.Options.FirstFooter ?? previous?.Options.FirstFooter ?? "",
            EvenHeader = current.Options.EvenHeader ?? previous?.Options.EvenHeader ?? "",
            EvenFooter = current.Options.EvenFooter ?? previous?.Options.EvenFooter ?? ""
        }
    };

    public static int AtParagraph(DocumentModel document, Paragraph paragraph)
    {
        var section = 0;
        foreach (var block in document.Blocks)
        {
            if (block is SectionBreakBlock) section++;
            else if (ReferenceEquals(block, paragraph) || block is TableBlock table && DocumentModel.Walk([table]).Contains(paragraph)) return section;
        }
        throw new ArgumentException("The paragraph is not part of this document.", nameof(paragraph));
    }

    public static string FormatNumber(int value, PageNumberStyle style)
    {
        if (value < 1) return value.ToString(CultureInfo.InvariantCulture);
        if (style is PageNumberStyle.UpperRoman or PageNumberStyle.LowerRoman)
        {
            // Values beyond classical Roman notation are represented in decimal, never expanded without a bound.
            if (value > 3999) return value.ToString(CultureInfo.InvariantCulture);
            var text = new StringBuilder();
            foreach (var (number, numeral) in new[] { (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
                while (value >= number) { value -= number; text.Append(numeral); }
            return style == PageNumberStyle.LowerRoman ? text.ToString().ToLowerInvariant() : text.ToString();
        }
        if (style is PageNumberStyle.UpperLetter or PageNumberStyle.LowerLetter)
        {
            var text = new StringBuilder();
            while (value > 0) { value--; text.Insert(0, (char)('A' + value % 26)); value /= 26; }
            return style == PageNumberStyle.LowerLetter ? text.ToString().ToLowerInvariant() : text.ToString();
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
