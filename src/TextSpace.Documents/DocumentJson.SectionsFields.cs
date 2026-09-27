using TextSpace.Core;

namespace TextSpace.Documents;

public static partial class DocumentJson
{
    public static void ValidatePage(PageSettings page)
    {
        if (page is null || !double.IsFinite(page.Width + page.Height + page.MarginTop + page.MarginBottom + page.MarginLeft + page.MarginRight + page.ColumnGap + page.HeaderDistance + page.FooterDistance)
            || page.Width is < 144 or > 4000 || page.Height is < 144 or > 4000 || page.MarginLeft < 0 || page.MarginRight < 0 || page.MarginTop < 0 || page.MarginBottom < 0
            || page.ContentHeight < 36 || page.ContentWidth < 36 || page.Columns is < 1 or > 3 || page.ColumnWidth < 24 || page.ColumnGap < 0
            || page.HeaderDistance < 0 || page.FooterDistance < 0 || page.HeaderDistance >= page.Height || page.FooterDistance >= page.Height)
            throw new InvalidDataException("Invalid page geometry.");
    }

    private static void ValidateSections(DocumentModel document)
    {
        if (document.SectionOptions is null || document.Fields is null) throw new InvalidDataException("Missing section or field structure.");
        if (document.Blocks.OfType<SectionBreakBlock>().Count() > 1000) throw new InvalidDataException("Too many document sections.");
        foreach (var boundary in document.Blocks.OfType<SectionBreakBlock>())
            if (!Enum.IsDefined(boundary.Kind) || boundary.Section is null) throw new InvalidDataException("Invalid section boundary.");
        foreach (var section in DocumentSections.Definitions(document))
        {
            ValidatePage(section.Page);
            var options = section.Options;
            if (options is null || !Enum.IsDefined(options.NumberStyle) || options.PageNumberStart is < 1 or > 1_000_000) throw new InvalidDataException("Invalid section numbering.");
            foreach (var story in new[] { section.Header, section.Footer, options.FirstHeader, options.FirstFooter, options.EvenHeader, options.EvenFooter })
                if (story?.Length > 8000) throw new InvalidDataException("Header or footer exceeds 8,000 characters.");
        }
    }

    private static void ValidateFields(DocumentModel document)
    {
        if (document.Fields.Count > 10_000) throw new InvalidDataException("Too many fields.");
        if (document.Fields.Count == 0) return;
        var index = new TextIndex(document); var ids = new HashSet<string>(StringComparer.Ordinal); var end = -1;
        foreach (var field in document.Fields.OrderBy(f => f?.Start ?? -1))
        {
            if (field is null || string.IsNullOrWhiteSpace(field.Id) || !ids.Add(field.Id) || string.IsNullOrWhiteSpace(field.Instruction) || field.Instruction.Length > 1024 || field.Instruction.Any(char.IsControl))
                throw new InvalidDataException("Fields require unique identifiers and bounded instructions.");
            if (field.Start < 0 || field.End <= field.Start || field.End > index.Length || field.Start < end
                || index.Snap(field.Start) != field.Start || index.Snap(field.End) != field.End || !ReferenceEquals(index.At(field.Start).Paragraph, index.At(field.End).Paragraph))
                throw new InvalidDataException("A field result must occupy a non-overlapping, Unicode-safe range in one paragraph.");
            end = field.End;
        }
    }
}
