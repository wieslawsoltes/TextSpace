using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>Flow layout in points. Paragraphs split at line boundaries; table rows split only when taller than a page.</summary>
public sealed class PageLayoutEngine(ITextMetrics metrics)
{
    public ParagraphLayoutCache ParagraphCache { get; } = new(metrics);
    public DocumentLayout Layout(DocumentModel document)
    {
        var flow = new SectionFlow(document);
        var index = new TextIndex(document); var layouter = ParagraphCache; var number = 0;
        void Paragraph(Paragraph paragraph, int blockIndex)
        {
            var format = paragraph.Format;
            if (format.PageBreakBefore && (flow.Y > flow.Settings.MarginTop || flow.Column > 0 || flow.Page.Regions.Count > 1)) flow.Next(true);
            var lines = layouter.Layout(paragraph, flow.Settings.ColumnWidth, index.StartOf(paragraph), document.DefaultTabStop);
            var before = Math.Max(0, format.SpaceBefore);
            var headCount = format.WidowControl ? Math.Min(2, lines.Count) : 1;
            var headHeight = lines.Take(headCount).Sum(l => l.Height);
            var paragraphHeight = lines.Sum(l => l.Height);
            var required = before + headHeight;
            if (format.KeepLinesTogether && before + paragraphHeight <= flow.Settings.ContentHeight) required = before + paragraphHeight;
            if (format.KeepWithNext)
            {
                var chain = before + paragraphHeight; var previous = paragraph;
                for (var next = blockIndex + 1; next < document.Blocks.Count && previous.Format.KeepWithNext; next++)
                {
                    if (document.Blocks[next] is not Paragraph following || following.Format.PageBreakBefore) break;
                    var followingLines = layouter.Layout(following, flow.Settings.ColumnWidth, index.StartOf(following), document.DefaultTabStop);
                    var whole = following.Format.KeepLinesTogether || following.Format.KeepWithNext;
                    chain += Math.Max(0, previous.Format.SpaceAfter) + Math.Max(0, following.Format.SpaceBefore)
                        + (whole ? followingLines.Sum(l => l.Height) : followingLines.Take(following.Format.WidowControl ? 2 : 1).Sum(l => l.Height));
                    previous = following;
                    if (chain > flow.Settings.ContentHeight) break;
                }
                // Over-height keep chains must degrade rather than loop or create empty pages.
                if (chain <= flow.Settings.ContentHeight) required = Math.Max(required, chain);
            }
            flow.Ensure(required); flow.Y += before;
            if (format.List == ListKind.Number) number++; else if (format.List == ListKind.None) number = 0;
            var at = 0;
            while (at < lines.Count)
            {
                var capacity = flow.Limit - flow.Y;
                var fit = 0; var height = 0d;
                while (at + fit < lines.Count && height + lines[at + fit].Height <= capacity + 0.0001) height += lines[at + fit++].Height;
                if (fit == 0)
                {
                    if (flow.Y > flow.Top + 0.0001) { flow.Next(); continue; }
                    if (flow.Top > flow.Settings.MarginTop + 0.0001 && lines[at].Height > flow.Capacity)
                    { flow.Next(true); continue; }
                    fit = 1; // A single oversized line cannot satisfy the page geometry, but must make progress.
                }
                if (format.WidowControl && at + fit < lines.Count)
                {
                    if (at == 0 && fit == 1 && headHeight <= flow.Capacity && flow.Y > flow.Top + 0.0001) { flow.Next(); continue; }
                    if (lines.Count - at - fit == 1 && fit > 1)
                    {
                        if (at == 0 && fit == 2 && paragraphHeight <= flow.Capacity && flow.Y > flow.Top + 0.0001) { flow.Next(); continue; }
                        if (fit > 2 || at > 0) fit--;
                    }
                }
                for (var i = 0; i < fit; i++)
                {
                    var line = lines[at];
                    if (at == 0 && format.List != ListKind.None) line.Marker = format.List == ListKind.Bullet ? "•" : number + ".";
                    flow.Place(line); flow.Y += line.Height; at++;
                }
                if (at < lines.Count) flow.Next();
            }
            flow.Y += Math.Max(0, format.SpaceAfter);
        }
        void Table(TableBlock table)
        {
            var layout = new TableLayouter(layouter, index, document.DefaultTabStop, Math.Max(12, flow.Settings.ContentHeight - 24))
                .Measure(table, flow.Settings.ColumnWidth);
            var repeatHeight = layout.HeaderHeight < flow.Capacity * 0.5 ? layout.HeaderHeight : 0;
            void Continue(double consumed, double minimum = 0)
            {
                flow.Next();
                if (repeatHeight > 0 && consumed >= repeatHeight - 0.0001 && repeatHeight + minimum < flow.Capacity)
                {
                    var first = flow.Page.Lines.Count;
                    flow.Y += layout.DrawSlice(flow.Page, 0, repeatHeight, flow.Left, flow.Y, replica: true);
                    flow.AttributeLines(first);
                }
            }
            foreach (var group in layout.Groups)
            {
                var height = group.End - group.Start;
                // Respect cantSplit for rows/merged groups that fit. Splittable rows
                // consume remaining space; over-height groups must make progress.
                if (!group.AllowSplit && height <= flow.Settings.ContentHeight && height > flow.Capacity)
                    flow.Ensure(height);
                if ((!group.AllowSplit || group.End <= layout.HeaderHeight) && height <= flow.Capacity && flow.Y + height > flow.Limit && flow.Y > flow.Top + 0.0001)
                    Continue(group.Start, height);
                var consumed = group.Start;
                while (consumed < group.End - 0.0001)
                {
                    var available = flow.Limit - flow.Y;
                    if (available < 1) { Continue(consumed); continue; }
                    var end = Math.Min(group.End, consumed + available);
                    var cut = layout.Cut(consumed, end);
                    if (cut <= consumed + 0.0001)
                    {
                        if (flow.Y > flow.Top + 0.0001)
                        {
                            // Do not repeat a header that would prevent the next item fitting.
                            Continue(consumed, flow.Capacity); continue;
                        }
                        cut = end; // An individually oversized line must still make progress.
                    }
                    var first = flow.Page.Lines.Count;
                    var drawn = layout.DrawSlice(flow.Page, consumed, cut, flow.Left, flow.Y);
                    flow.AttributeLines(first);
                    flow.Y += drawn; consumed = cut;
                    if (consumed < group.End - 0.0001) Continue(consumed);
                }
            }
            flow.Y += 8;
        }
        void BreakMarker(string label) => flow.Page.Breaks.Add(new(label, flow.Left, Math.Min(flow.Y, flow.Limit), flow.Settings.ColumnWidth));
        for (var blockIndex = 0; blockIndex < document.Blocks.Count; blockIndex++)
        {
            var block = document.Blocks[blockIndex];
            if (block is Paragraph && (blockIndex == 0 || document.Blocks[blockIndex - 1] is SectionBreakBlock))
            {
                var balancedHeight = ColumnBalancer.Height(document, blockIndex, index, layouter, flow);
                if (balancedHeight is { } height) flow.Balance(height);
            }
            switch (block)
            {
                case Paragraph p: Paragraph(p, blockIndex); break;
                case TableBlock table: Table(table); break;
                case PageBreakBlock: BreakMarker("Page Break"); flow.Next(true); break;
                case ColumnBreakBlock: BreakMarker("Column Break"); flow.Next(); break;
                case SectionBreakBlock boundary:
                    BreakMarker("Section Break (" + boundary.Kind + ")");
                    flow.Switch(boundary); number = 0;
                    break;
                case ImageBlock image:
                    var ratio = Math.Min(1, Math.Min(flow.Settings.ColumnWidth / image.Width, flow.Settings.ContentHeight / image.Height));
                    var width = image.Width * ratio; var height = image.Height * ratio; flow.Ensure(height);
                    var x = flow.Left + (image.Alignment == TextAlignment.Center ? (flow.Settings.ColumnWidth - width) / 2 : image.Alignment == TextAlignment.Right ? flow.Settings.ColumnWidth - width : 0);
                    flow.Page.Images.Add(new(image, new(x, flow.Y, width, height))); flow.Y += height + 8; break;
            }
        }
        return flow.Finish(document);
    }
}
