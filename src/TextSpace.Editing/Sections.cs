using TextSpace.Core;
using TextSpace.Documents;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    public int CurrentSectionIndex => DocumentSections.AtParagraph(Document, CurrentParagraph);
    public SectionDefinition CurrentSection => DocumentSections.Definitions(Document)[CurrentSectionIndex];

    public void SetSection(Func<SectionDefinition, SectionDefinition> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var number = CurrentSectionIndex;
        Execute("Section properties", () =>
        {
            var definition = update(DocumentSections.Definitions(Document)[number]) ?? throw new InvalidOperationException("A section is required.");
            DocumentJson.ValidatePage(definition.Page);
            if (number == 0)
            {
                Document.Page = definition.Page; Document.Header = definition.Header ?? ""; Document.Footer = definition.Footer ?? ""; Document.SectionOptions = definition.Options;
            }
            else Document.Blocks.OfType<SectionBreakBlock>().ElementAt(number - 1).Section = definition;
        });
    }

    public void InsertSectionBreak(SectionBreakKind kind = SectionBreakKind.NextPage)
    {
        EnsureWritable();
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (CurrentTable is not null) throw new InvalidOperationException("Insert a section break outside a table.");
        var current = CurrentSection;
        InsertBlock(new SectionBreakBlock
        {
            Kind = kind,
            Section = current with
            {
                Header = null, Footer = null,
                Options = current.Options with { FirstHeader = null, FirstFooter = null, EvenHeader = null, EvenFooter = null, PageNumberStart = null }
            }
        }, "Insert section break");
    }

    public void RemoveCurrentSectionBreak()
    {
        EnsureWritable(); var number = CurrentSectionIndex;
        if (number == 0) throw new InvalidOperationException("The first section has no preceding section break.");
        var boundary = Document.Blocks.OfType<SectionBreakBlock>().ElementAt(number - 1);
        StructuralEdit("Remove section break", () => Document.Blocks.Remove(boundary));
    }

    public void InsertColumnBreak()
    {
        EnsureWritable();
        if (CurrentTable is not null) throw new InvalidOperationException("Insert a column break outside a table.");
        InsertBlock(new ColumnBreakBlock(), "Insert column break");
    }
}
