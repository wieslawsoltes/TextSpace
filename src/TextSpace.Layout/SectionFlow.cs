using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Single-pass section/page/column state. Owns physical-page versus section-page identity.</summary>
internal sealed class SectionFlow
{
    private const double Epsilon = 0.0001;
    private SectionDefinition _section;
    private int _sectionIndex, _sectionPage, _nextPageNumber;
    private double _y;
    public List<LayoutPage> Pages { get; } = [];
    public List<LayoutNotice> Notices { get; } = [];
    public LayoutPage Page { get; private set; } = null!;
    public LayoutRegion Region { get; private set; } = null!;
    public PageSettings Settings => _section.Page;
    public int Column { get; private set; }
    public double Top => Region.Top;
    public double Limit { get; private set; }
    public double Capacity => Limit - Top;
    public double Left => Region.ColumnLeft(Column);
    public double Y
    {
        get => _y;
        set { _y = value; Region.Bottom = Math.Max(Region.Bottom, Math.Min(Limit, value)); }
    }

    public SectionFlow(DocumentModel document)
    {
        _section = DocumentSections.Resolve(DocumentSections.First(document), null);
        _nextPageNumber = _section.Options.PageNumberStart ?? 1;
        NewPage();
    }

    private void NewPage(bool parityBlank = false)
    {
        // A break can leave no actual content in a newly attached region.
        // Do not count such an empty shared region as a page of the new section.
        if (Page is not null && Page.Regions.Count > 1 && Region.SectionIndex == _sectionIndex && Region.Bottom <= Region.Top + Epsilon)
        {
            Page.Regions.RemoveAt(Page.Regions.Count - 1); _sectionPage--;
            if (_section.Options.PageNumberStart is { } restart && _sectionPage == 0) _nextPageNumber = restart;
        }
        if (Pages.Count >= 10_000) throw new InvalidOperationException("The document exceeds the pagination limit.");
        var number = _nextPageNumber++;
        Page = new(Pages.Count, Settings)
        {
            SectionIndex = _sectionIndex, SectionPageIndex = _sectionPage,
            PageNumber = number, Section = _section, IsParityBlank = parityBlank
        };
        Pages.Add(Page);
        AddRegion(Settings.MarginTop, 0, number);
    }

    private void AddRegion(double top, int column, int number)
    {
        Region = new()
        {
            SectionIndex = _sectionIndex, SectionPageIndex = _sectionPage++,
            PageNumber = number, Section = _section, Top = top,
            FirstColumn = column, LastColumn = column, Bottom = top
        };
        Page.Regions.Add(Region);
        Column = column; _y = top; Limit = Settings.Height - Settings.MarginBottom;
    }

    public void Next(bool forcePage = false)
    {
        if (!forcePage && Column + 1 < Settings.Columns)
        {
            Column++; Region.LastColumn = Column; _y = Top;
        }
        else NewPage();
    }

    public void Ensure(double height)
    {
        if (height > Capacity + Epsilon && Top > Settings.MarginTop + Epsilon)
        {
            NewPage(); return;
        }
        if (Y + height > Limit + Epsilon && Y > Top + Epsilon) Next();
    }

    public void Place(LayoutLine line)
    {
        line.PageIndex = Page.Index; line.Region = Region; line.ColumnIndex = Column;
        line.X += Left; line.Y = Y;
        foreach (var chunk in line.Chunks) chunk.X += Left;
        for (var i = 0; i < line.BarTabs.Length; i++) line.BarTabs[i] += Left;
        Page.Lines.Add(line);
    }

    public void AttributeLines(int first)
    {
        for (var i = first; i < Page.Lines.Count; i++)
        {
            Page.Lines[i].Region = Region; Page.Lines[i].ColumnIndex = Column;
        }
    }

    public void Balance(double height)
    {
        if (height > 0 && height < Capacity - Epsilon)
        {
            Limit = Top + height; Region.Balanced = true;
        }
    }

    public static bool SamePaper(PageSettings first, PageSettings second) =>
        Math.Abs(first.Width - second.Width) < Epsilon && Math.Abs(first.Height - second.Height) < Epsilon;

    private static bool SameGrid(PageSettings first, PageSettings second) => SamePaper(first, second)
        && Math.Abs(first.MarginLeft - second.MarginLeft) < Epsilon
        && Math.Abs(first.MarginRight - second.MarginRight) < Epsilon
        && Math.Abs(first.MarginTop - second.MarginTop) < Epsilon
        && Math.Abs(first.MarginBottom - second.MarginBottom) < Epsilon
        && first.Columns == second.Columns && Math.Abs(first.ColumnGap - second.ColumnGap) < Epsilon;

    public void Switch(SectionBreakBlock boundary)
    {
        var next = DocumentSections.Resolve(boundary.Section, _section);
        var inline = boundary.Kind is SectionBreakKind.Continuous or SectionBreakKind.NextColumn;
        var compatible = SamePaper(Page.Settings, next.Page);
        var nextColumn = boundary.Kind == SectionBreakKind.NextColumn;
        var sameGrid = SameGrid(Settings, next.Page);
        var firstColumn = nextColumn ? Column + 1 : 0;
        // A continuous region starts below every occupied column, including
        // earlier sections that began with a next-column break on this page.
        var top = nextColumn ? Top : Math.Max(next.Page.MarginTop, Page.Regions.Max(r => r.Bottom));
        var sharedNumber = next.Options.PageNumberStart ?? Region.PageNumber;
        var share = inline && compatible
            && (!nextColumn || sameGrid && firstColumn < next.Page.Columns)
            && top < next.Page.Height - next.Page.MarginBottom - Epsilon;
        if (inline && !compatible)
            Notices.Add(new("section-paper-change", _sectionIndex + 1,
                "A continuous/next-column section with different paper dimensions starts on a new physical page."));
        else if (nextColumn && !sameGrid)
            Notices.Add(new("section-column-grid-change", _sectionIndex + 1,
                "A next-column section with a different column grid starts on a new physical page."));

        if (!inline)
        {
            var nextPhysical = Pages.Count + 1;
            if (boundary.Kind == SectionBreakKind.OddPage && nextPhysical % 2 == 0
                || boundary.Kind == SectionBreakKind.EvenPage && nextPhysical % 2 != 0)
                NewPage(parityBlank: true);
        }
        _section = next; _sectionIndex++; _sectionPage = 0;
        if (share)
        {
            _nextPageNumber = sharedNumber + 1;
            AddRegion(top, firstColumn, sharedNumber);
        }
        else
        {
            _nextPageNumber = next.Options.PageNumberStart ?? _nextPageNumber;
            NewPage();
        }
    }

    public DocumentLayout Finish(DocumentModel document)
    {
        var counts = new int[_sectionIndex + 1];
        foreach (var page in Pages)
            foreach (var region in page.Regions) counts[region.SectionIndex]++;
        foreach (var page in Pages)
        {
            foreach (var region in page.Regions) region.SectionPageCount = counts[region.SectionIndex];
            page.SectionPageCount = counts[page.SectionIndex];
        }
        return new(document.Page, Pages) { Notices = Notices.ToArray() };
    }
}
