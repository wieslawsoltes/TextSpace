using TextSpace.Core;

namespace TextSpace.Layout;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;
}

public sealed class LayoutChunk
{
    public string Text { get; init; } = "";
    public TextStyle Style { get; init; } = new();
    public int Start { get; init; }
    public int End => Start + Text.Length;
    public double X { get; set; }
    public double Width { get; set; }
    public double[] Carets { get; init; } = [0];
    public TabLeader TabLeader { get; init; }
    public string? DisplayText { get; init; }
    private int[]? _boundaries;
    internal ReadOnlySpan<int> CaretOffsets
    {
        get
        {
            if (_boundaries is not null) return _boundaries;
            var offsets = System.Globalization.StringInfo.ParseCombiningCharacters(Text);
            _boundaries = new int[offsets.Length + 1];
            offsets.CopyTo(_boundaries, 0); _boundaries[^1] = Text.Length;
            return _boundaries;
        }
    }
    public double Position(int offset) => X + Carets[Math.Clamp(offset, 0, Text.Length)] * (Carets[^1] > 0 ? Width / Carets[^1] : 1);
}

public sealed class LayoutLine
{
    public string ParagraphId { get; init; } = "";
    public int Start { get; init; }
    public int End { get; init; }
    public int PageIndex { get; set; }
    public bool IsReplica { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; init; }
    public double Ascent { get; init; }
    public double Baseline => Y + Ascent;
    public bool LastInParagraph { get; set; }
    public string? Marker { get; set; }
    public ParagraphFormat Format { get; init; } = new();
    public TextStyle DefaultStyle { get; init; } = new();
    public List<LayoutChunk> Chunks { get; init; } = [];
    public double[] BarTabs { get; init; } = [];
    public double CaretX(int position)
    {
        if (Chunks.Count == 0) return X;
        for (var i = Chunks.Count - 1; i >= 0; i--)
        {
            var candidate = Chunks[i];
            if (position >= candidate.Start && position <= candidate.End) return candidate.Position(position - candidate.Start);
        }
        var chunk = position < Start ? Chunks[0] : Chunks[^1];
        return chunk.Position(position - chunk.Start);
    }
    public int HitTest(double x)
    {
        if (Chunks.Count == 0) return Start;
        var best = Start; var distance = double.MaxValue;
        foreach (var chunk in Chunks)
        {
            foreach (var offset in chunk.CaretOffsets)
            {
                var d = Math.Abs(chunk.Position(offset) - x);
                if (d < distance) { distance = d; best = chunk.Start + offset; }
            }
        }
        return best;
    }
}

public sealed record LayoutCell(string TableId, RectD Bounds, string? Fill, bool Header)
{
    public bool DrawTop { get; init; } = true;
    public bool DrawBottom { get; init; } = true;
    public bool IsReplica { get; init; }
}
public sealed record LayoutImage(ImageBlock Image, RectD Bounds);

public sealed record LayoutBreakMarker(string Label, double X, double Y, double Width);

public sealed class LayoutPage(int index, PageSettings settings)
{
    public int Index { get; } = index;
    public PageSettings Settings { get; } = settings;
    public int SectionIndex { get; init; }
    public int SectionPageIndex { get; init; }
    public int SectionPageCount { get; set; }
    public int PageNumber { get; init; } = index + 1;
    public bool IsParityBlank { get; init; }
    public SectionDefinition Section { get; init; } = new() { Page = settings };
    public string Header => IsParityBlank ? "" : Section.Options.DifferentFirstPage && SectionPageIndex == 0 ? Section.Options.FirstHeader ?? ""
        : Section.Options.DifferentOddAndEven && PageNumber % 2 == 0 ? Section.Options.EvenHeader ?? "" : Section.Header ?? "";
    public string Footer => IsParityBlank ? "" : Section.Options.DifferentFirstPage && SectionPageIndex == 0 ? Section.Options.FirstFooter ?? ""
        : Section.Options.DifferentOddAndEven && PageNumber % 2 == 0 ? Section.Options.EvenFooter ?? "" : Section.Footer ?? "";
    public List<LayoutLine> Lines { get; } = [];
    public List<LayoutCell> Cells { get; } = [];
    public List<LayoutImage> Images { get; } = [];
    public List<LayoutBreakMarker> Breaks { get; } = [];
}

public readonly record struct CaretGeometry(int PageIndex, double X, double Y, double Height);

/// <summary>Immutable page geometry index supporting mixed paper sizes and logarithmic page lookup.</summary>
public sealed partial class DocumentLayout
{
    public const double PageGap = 24;
    private readonly double[] _pageTops;
    private readonly LayoutLine[] _lines;
    public PageSettings Settings { get; }
    public IReadOnlyList<LayoutPage> Pages { get; }
    public double Width { get; }
    public double Height { get; }
    public IEnumerable<LayoutLine> Lines => _lines;

    public DocumentLayout(PageSettings settings, IReadOnlyList<LayoutPage> pages)
    {
        if (pages.Count == 0) throw new ArgumentException("At least one page is required.", nameof(pages));
        Settings = settings; Pages = pages; Width = pages.Max(p => p.Settings.Width);
        _pageTops = new double[pages.Count]; var top = 0d;
        for (var i = 0; i < pages.Count; i++) { _pageTops[i] = top; top += pages[i].Settings.Height + PageGap; }
        Height = top - PageGap; _lines = pages.SelectMany(p => p.Lines).Where(line => !line.IsReplica).ToArray();
        BuildTextLookup();
    }
    public double PageTop(int index) => _pageTops[Math.Clamp(index, 0, _pageTops.Length - 1)];
    public double PageLeft(int index) => (Width - Pages[Math.Clamp(index, 0, Pages.Count - 1)].Settings.Width) / 2;
    public int PageAtY(double documentY)
    {
        if (!double.IsFinite(documentY)) return 0;
        var at = Array.BinarySearch(_pageTops, documentY);
        return at >= 0 ? at : Math.Clamp(~at - 1, 0, Pages.Count - 1);
    }
    public CaretGeometry Caret(int position)
    {
        var line = FindCaretLine(position);
        return line is null ? new(0, Settings.MarginLeft, Settings.MarginTop, 14) : new(line.PageIndex, line.CaretX(position), line.Y, line.Height);
    }
    public FieldPageInfo FieldPageAt(int position)
    {
        var page = Pages[Caret(position).PageIndex];
        return new(page.PageNumber, Pages.Count, page.SectionIndex + 1, page.SectionPageCount, page.Section.Options.NumberStyle);
    }
    public int HitTest(double x, double documentY)
    {
        var pageIndex = PageAtY(documentY); var y = documentY - PageTop(pageIndex); var page = Pages[pageIndex];
        x -= PageLeft(pageIndex);
        var line = NearestLine(page.Lines, x, y);
        if (line is not null) return line.HitTest(x);
        // A deliberately blank parity page should not jump to the document's end.
        return _lines.LastOrDefault(l => l.PageIndex < pageIndex)?.End ?? _lines.FirstOrDefault()?.Start ?? 0;
    }
    public int VerticalMove(int position, double deltaY, double? desiredX = null)
    {
        var caret = Caret(position);
        return HitTest(desiredX ?? caret.X + PageLeft(caret.PageIndex), PageTop(caret.PageIndex) + caret.Y + deltaY + caret.Height / 2);
    }
}
