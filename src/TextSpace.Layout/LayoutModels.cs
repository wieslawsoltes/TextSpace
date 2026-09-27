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
    public double Position(int offset) => X + Carets[Math.Clamp(offset, 0, Text.Length)] * (Carets[^1] > 0 ? Width / Carets[^1] : 1);
}

public sealed class LayoutLine
{
    public string ParagraphId { get; init; } = "";
    public int Start { get; init; }
    public int End { get; init; }
    public int PageIndex { get; set; }
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
    public double CaretX(int position)
    {
        if (Chunks.Count == 0) return X;
        var chunk = Chunks.LastOrDefault(c => position >= c.Start && position <= c.End) ?? (position < Start ? Chunks[0] : Chunks[^1]);
        return chunk.Position(position - chunk.Start);
    }
    public int HitTest(double x)
    {
        if (Chunks.Count == 0) return Start;
        var best = Start; var distance = double.MaxValue;
        foreach (var chunk in Chunks)
        {
            var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(chunk.Text).Append(chunk.Text.Length);
            foreach (var offset in boundaries)
            {
                var d = Math.Abs(chunk.Position(offset) - x);
                if (d < distance) { distance = d; best = chunk.Start + offset; }
            }
        }
        return best;
    }
}

public sealed record LayoutCell(string TableId, RectD Bounds, string? Fill, bool Header);
public sealed record LayoutImage(ImageBlock Image, RectD Bounds);

public sealed class LayoutPage(int index, PageSettings settings)
{
    public int Index { get; } = index;
    public PageSettings Settings { get; } = settings;
    public List<LayoutLine> Lines { get; } = [];
    public List<LayoutCell> Cells { get; } = [];
    public List<LayoutImage> Images { get; } = [];
}

public readonly record struct CaretGeometry(int PageIndex, double X, double Y, double Height);

public sealed class DocumentLayout(PageSettings settings, IReadOnlyList<LayoutPage> pages)
{
    public const double PageGap = 24;
    public PageSettings Settings { get; } = settings;
    public IReadOnlyList<LayoutPage> Pages { get; } = pages;
    public double Width => Settings.Width;
    public double Height => Pages.Count * (Settings.Height + PageGap) - PageGap;
    public IEnumerable<LayoutLine> Lines => Pages.SelectMany(p => p.Lines);
    public double PageTop(int index) => index * (Settings.Height + PageGap);
    public CaretGeometry Caret(int position)
    {
        var line = Lines.LastOrDefault(l => position >= l.Start && position <= l.End) ?? Lines.LastOrDefault();
        return line is null ? new(0, Settings.MarginLeft, Settings.MarginTop, 14) : new(line.PageIndex, line.CaretX(position), line.Y, line.Height);
    }
    public int HitTest(double x, double documentY)
    {
        var pageIndex = Math.Clamp((int)(documentY / (Settings.Height + PageGap)), 0, Pages.Count - 1);
        var y = documentY - PageTop(pageIndex); var page = Pages[pageIndex];
        var line = page.Lines.OrderBy(l => (y < l.Y ? l.Y - y : y > l.Y + l.Height ? y - l.Y - l.Height : 0) * 10000 + (x < l.X ? l.X - x : x > l.X + l.Width ? x - l.X - l.Width : 0)).FirstOrDefault();
        return line?.HitTest(x) ?? Lines.LastOrDefault()?.End ?? 0;
    }
    public int VerticalMove(int position, double deltaY, double? desiredX = null)
    {
        var caret = Caret(position); return HitTest(desiredX ?? caret.X, PageTop(caret.PageIndex) + caret.Y + deltaY + caret.Height / 2);
    }
}
