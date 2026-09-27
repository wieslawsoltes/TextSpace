using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Flow layout in points. Paragraphs split at line boundaries; table rows split only when taller than a page.</summary>
public sealed class PageLayoutEngine(ITextMetrics metrics)
{
    public DocumentLayout Layout(DocumentModel document)
    {
        var section = DocumentSections.Resolve(DocumentSections.First(document), null);
        var settings = section.Page; var index = new TextIndex(document); var layouter = new ParagraphLayouter(metrics);
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
            page.Lines.Add(line);
        }
        void Paragraph(Paragraph paragraph)
        {
            if (paragraph.Format.PageBreakBefore && (y > settings.MarginTop || column > 0)) Next(true);
            var lines = layouter.Layout(paragraph, settings.ColumnWidth, index.StartOf(paragraph));
            var before = Math.Max(0, paragraph.Format.SpaceBefore);
            var keepHeight = lines.Take(Math.Min(2, lines.Count)).Sum(l => l.Height) + before;
            if (paragraph.Format.KeepWithNext) keepHeight = Math.Min(settings.ContentHeight, lines.Sum(l => l.Height) + before + paragraph.Format.SpaceAfter + 18);
            Ensure(keepHeight); y += before;
            if (paragraph.Format.List == ListKind.Number) number++; else if (paragraph.Format.List == ListKind.None) number = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                if (i == lines.Count - 2) Ensure(lines[i].Height + lines[i + 1].Height);
                Ensure(lines[i].Height);
                if (i == 0 && paragraph.Format.List != ListKind.None) lines[i].Marker = paragraph.Format.List == ListKind.Bullet ? "•" : number + ".";
                Place(lines[i], Left(), y); y += lines[i].Height;
            }
            y += Math.Max(0, paragraph.Format.SpaceAfter);
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
                        var lines = layouter.Layout(p, Math.Max(12, widths[c] - 2 * table.CellPadding), index.StartOf(p));
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
        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case Paragraph p: Paragraph(p); break;
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
