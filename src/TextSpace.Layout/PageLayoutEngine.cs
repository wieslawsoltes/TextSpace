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
            var columns = table.Rows.Max(r => r.Cells.Count); var width = settings.ColumnWidth;
            var weights = table.ColumnWidths.Count == columns && table.ColumnWidths.All(w => double.IsFinite(w) && w > 0) ? table.ColumnWidths.ToArray() : Enumerable.Repeat(1d, columns).ToArray();
            var total = weights.Sum(); var widths = weights.Select(w => width * w / total).ToArray();
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex]; var cellLines = new List<(LayoutLine line, double x, double y)>(); var rowHeight = 0d; var cellX = 0d;
                for (var c = 0; c < row.Cells.Count; c++)
                {
                    var cellY = table.CellPadding;
                    foreach (var p in DocumentModel.Walk(row.Cells[c].Blocks))
                    {
                        cellY += Math.Max(0, p.Format.SpaceBefore);
                        var lines = layouter.Layout(p, Math.Max(12, widths[c] - 2 * table.CellPadding), index.StartOf(p), document.DefaultTabStop);
                        foreach (var line in lines) { cellLines.Add((line, cellX + table.CellPadding, cellY)); cellY += line.Height; }
                        cellY += Math.Max(0, p.Format.SpaceAfter);
                    }
                    rowHeight = Math.Max(rowHeight, cellY + table.CellPadding); cellX += widths[c];
                }
                rowHeight = Math.Max(rowHeight, 24);
                Ensure(Math.Min(rowHeight, settings.ContentHeight)); var consumed = 0d;
                while (consumed < rowHeight - 0.01)
                {
                    var available = settings.Height - settings.MarginBottom - y;
                    var segment = Math.Min(rowHeight - consumed, available);
                    if (segment < 12) { Next(); continue; }
                    var crossing = cellLines.Where(l => l.y >= consumed && l.y < consumed + segment && l.y + l.line.Height > consumed + segment).ToArray();
                    if (crossing.Length > 0) segment = crossing.Min(l => l.y) - consumed;
                    if (segment < 1) segment = Math.Min(rowHeight - consumed, available);
                    cellX = Left();
                    for (var c = 0; c < row.Cells.Count; c++)
                    {
                        var fill = row.Cells[c].Shading ?? (table.HeaderRow && rowIndex == 0 ? "#D9E5F5" : table.BandedRows && rowIndex % 2 == 0 ? "#F3F6FA" : null);
                        page.Cells.Add(new(table.Id, new(cellX, y, widths[c], segment), fill, table.HeaderRow && rowIndex == 0)); cellX += widths[c];
                    }
                    foreach (var item in cellLines.Where(l => l.y >= consumed - 0.01 && l.y < consumed + segment - 0.01)) Place(item.line, Left() + item.x, y + item.y - consumed);
                    y += segment; consumed += segment; if (consumed < rowHeight - 0.01) Next();
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
