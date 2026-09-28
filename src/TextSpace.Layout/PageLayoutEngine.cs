using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Flow layout in points. Paragraphs split at line boundaries; table rows split only when taller than a page.</summary>
public sealed class PageLayoutEngine(ITextMetrics metrics)
{
    public ParagraphLayoutCache ParagraphCache { get; } = new(metrics);
    public DocumentLayout Layout(DocumentModel document)
    {
        var section = DocumentSections.Resolve(DocumentSections.First(document), null);
        var settings = section.Page; var index = new TextIndex(document); var layouter = ParagraphCache;
        var pages = new List<LayoutPage>(); var sectionIndex = 0; var sectionPage = 0;
        var pageNumber = section.Options.PageNumberStart ?? 1;
        LayoutPage NewPage(bool parityBlank = false)
        {
            if (pages.Count >= 10_000) throw new InvalidOperationException("The document exceeds the pagination limit.");
            var created = new LayoutPage(pages.Count, settings)
            {
                SectionIndex = sectionIndex, SectionPageIndex = sectionPage++, PageNumber = pageNumber++,
                Section = section, IsParityBlank = parityBlank
            };
            pages.Add(created); return created;
        }
        var page = NewPage(); var column = 0; var y = settings.MarginTop; var number = 0;
        double Left() => settings.MarginLeft + column * (settings.ColumnWidth + settings.ColumnGap);
        void Next(bool forcePage = false)
        {
            if (!forcePage && column + 1 < settings.Columns) column++;
            else { page = NewPage(); column = 0; }
            y = settings.MarginTop;
        }
        void Ensure(double height) { if (y + height > settings.Height - settings.MarginBottom && y > settings.MarginTop + 0.1) Next(); }
        void Place(LayoutLine line, double x, double lineY)
        {
            line.PageIndex = page.Index; line.X += x; line.Y = lineY;
            foreach (var chunk in line.Chunks) chunk.X += x;
            for (var i = 0; i < line.BarTabs.Length; i++) line.BarTabs[i] += x;
            page.Lines.Add(line);
        }
        void Paragraph(Paragraph paragraph, int blockIndex)
        {
            var format = paragraph.Format;
            if (format.PageBreakBefore && (y > settings.MarginTop || column > 0)) Next(true);
            var lines = layouter.Layout(paragraph, settings.ColumnWidth, index.StartOf(paragraph), document.DefaultTabStop);
            var before = Math.Max(0, format.SpaceBefore);
            var headCount = format.WidowControl ? Math.Min(2, lines.Count) : 1;
            var headHeight = lines.Take(headCount).Sum(l => l.Height);
            var paragraphHeight = lines.Sum(l => l.Height);
            var required = before + headHeight;
            if (format.KeepLinesTogether && before + paragraphHeight <= settings.ContentHeight) required = before + paragraphHeight;
            if (format.KeepWithNext)
            {
                var chain = before + paragraphHeight; var previous = paragraph;
                for (var next = blockIndex + 1; next < document.Blocks.Count && previous.Format.KeepWithNext; next++)
                {
                    if (document.Blocks[next] is not Paragraph following || following.Format.PageBreakBefore) break;
                    var followingLines = layouter.Layout(following, settings.ColumnWidth, index.StartOf(following), document.DefaultTabStop);
                    var whole = following.Format.KeepLinesTogether || following.Format.KeepWithNext;
                    chain += Math.Max(0, previous.Format.SpaceAfter) + Math.Max(0, following.Format.SpaceBefore)
                        + (whole ? followingLines.Sum(l => l.Height) : followingLines.Take(following.Format.WidowControl ? 2 : 1).Sum(l => l.Height));
                    previous = following;
                    if (chain > settings.ContentHeight) break;
                }
                // Over-height keep chains must degrade rather than loop or create empty pages.
                if (chain <= settings.ContentHeight) required = Math.Max(required, chain);
            }
            Ensure(required); y += before;
            if (format.List == ListKind.Number) number++; else if (format.List == ListKind.None) number = 0;
            var at = 0;
            while (at < lines.Count)
            {
                var capacity = settings.Height - settings.MarginBottom - y;
                var fit = 0; var height = 0d;
                while (at + fit < lines.Count && height + lines[at + fit].Height <= capacity + 0.0001) height += lines[at + fit++].Height;
                if (fit == 0)
                {
                    if (y > settings.MarginTop + 0.0001) { Next(); continue; }
                    fit = 1; // A single oversized line cannot satisfy the page geometry, but must make progress.
                }
                if (format.WidowControl && at + fit < lines.Count)
                {
                    if (at == 0 && fit == 1 && headHeight <= settings.ContentHeight && y > settings.MarginTop + 0.0001) { Next(); continue; }
                    if (lines.Count - at - fit == 1 && fit > 1)
                    {
                        if (at == 0 && fit == 2 && paragraphHeight <= settings.ContentHeight && y > settings.MarginTop + 0.0001) { Next(); continue; }
                        if (fit > 2 || at > 0) fit--;
                    }
                }
                for (var i = 0; i < fit; i++)
                {
                    var line = lines[at];
                    if (at == 0 && format.List != ListKind.None) line.Marker = format.List == ListKind.Bullet ? "•" : number + ".";
                    Place(line, Left(), y); y += line.Height; at++;
                }
                if (at < lines.Count) Next();
            }
            y += Math.Max(0, format.SpaceAfter);
        }
        void Table(TableBlock table)
        {
            var layout = new TableLayouter(layouter, index, document.DefaultTabStop, Math.Max(12, settings.ContentHeight - 24))
                .Measure(table, settings.ColumnWidth);
            var repeatHeight = layout.HeaderHeight < settings.ContentHeight * 0.5 ? layout.HeaderHeight : 0;
            void Continue(double consumed, double minimum = 0)
            {
                Next();
                if (repeatHeight > 0 && consumed >= repeatHeight - 0.0001 && repeatHeight + minimum < settings.ContentHeight)
                    y += layout.DrawSlice(page, 0, repeatHeight, Left(), y, replica: true);
            }
            foreach (var group in layout.Groups)
            {
                var height = group.End - group.Start;
                // Respect cantSplit for rows/merged groups that fit. Splittable rows
                // consume remaining space; over-height groups must make progress.
                if ((!group.AllowSplit || group.End <= layout.HeaderHeight) && height <= settings.ContentHeight && y + height > settings.Height - settings.MarginBottom && y > settings.MarginTop + 0.0001)
                    Continue(group.Start, height);
                var consumed = group.Start;
                while (consumed < group.End - 0.0001)
                {
                    var available = settings.Height - settings.MarginBottom - y;
                    if (available < 1) { Continue(consumed); continue; }
                    var end = Math.Min(group.End, consumed + available);
                    var cut = layout.Cut(consumed, end);
                    if (cut <= consumed + 0.0001)
                    {
                        if (y > settings.MarginTop + 0.0001)
                        {
                            // Do not repeat a header that would prevent the next item fitting.
                            Continue(consumed, settings.ContentHeight); continue;
                        }
                        cut = end; // An individually oversized line must still make progress.
                    }
                    var drawn = layout.DrawSlice(page, consumed, cut, Left(), y);
                    y += drawn; consumed = cut;
                    if (consumed < group.End - 0.0001) Continue(consumed);
                }
            }
            y += 8;
        }
        void BreakMarker(string label) => page.Breaks.Add(new(label, Left(), Math.Min(y, settings.Height - settings.MarginBottom), settings.ColumnWidth));
        for (var blockIndex = 0; blockIndex < document.Blocks.Count; blockIndex++)
        {
            var block = document.Blocks[blockIndex];
            switch (block)
            {
                case Paragraph p: Paragraph(p, blockIndex); break;
                case TableBlock table: Table(table); break;
                case PageBreakBlock: BreakMarker("Page Break"); Next(true); break;
                case ColumnBreakBlock: BreakMarker("Column Break"); Next(); break;
                case SectionBreakBlock boundary:
                    BreakMarker("Section Break (" + boundary.Kind + ")");
                    // Parity is physical (recto/verso), independent of a section's displayed numbering restart.
                    var nextPhysicalNumber = pages.Count + 1;
                    if (boundary.Kind == SectionBreakKind.OddPage && nextPhysicalNumber % 2 == 0
                        || boundary.Kind == SectionBreakKind.EvenPage && nextPhysicalNumber % 2 != 0) NewPage(true);
                    section = DocumentSections.Resolve(boundary.Section, section); settings = section.Page;
                    sectionIndex++; sectionPage = 0; pageNumber = section.Options.PageNumberStart ?? pageNumber;
                    page = NewPage(); column = 0; y = settings.MarginTop; number = 0;
                    break;
                case ImageBlock image:
                    var ratio = Math.Min(1, Math.Min(settings.ColumnWidth / image.Width, settings.ContentHeight / image.Height));
                    var width = image.Width * ratio; var height = image.Height * ratio; Ensure(height);
                    var x = Left() + (image.Alignment == TextAlignment.Center ? (settings.ColumnWidth - width) / 2 : image.Alignment == TextAlignment.Right ? settings.ColumnWidth - width : 0);
                    page.Images.Add(new(image, new(x, y, width, height))); y += height + 8; break;
            }
            if (pages.Count > 10_000) throw new InvalidOperationException("The document exceeds the pagination limit.");
        }
        foreach (var group in pages.GroupBy(p => p.SectionIndex))
            foreach (var member in group) member.SectionPageCount = group.Count();
        return new(document.Page, pages);
    }
}
