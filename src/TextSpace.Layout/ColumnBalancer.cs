using TextSpace.Core;

namespace TextSpace.Layout;

/// <summary>
/// Balances a paragraph-only section band ending at a continuous boundary.
/// A bounded binary search packs indivisible line groups, retaining paragraph
/// keeps and widow/orphan pairs. Explicit breaks, tables and images keep their
/// normal sequential flow; they are never reordered or flattened for balance.
/// </summary>
internal static class ColumnBalancer
{
    public static double? Height(DocumentModel document, int start, TextIndex index,
        ParagraphLayoutCache cache, SectionFlow flow)
    {
        if (flow.Settings.Columns < 2 || flow.Column != 0 || flow.Y != flow.Top) return null;
        var paragraphs = new List<Paragraph>();
        var terminated = false;
        for (var i = start; i < document.Blocks.Count; i++)
        {
            if (document.Blocks[i] is SectionBreakBlock boundary)
            {
                terminated = boundary.Kind == SectionBreakKind.Continuous
                    && SectionFlow.SamePaper(flow.Settings, boundary.Section.Page);
                break;
            }
            if (document.Blocks[i] is not Paragraph p || p.Format.PageBreakBefore || paragraphs.Count >= 1000) return null;
            paragraphs.Add(p);
        }
        if (!terminated || paragraphs.Count == 0) return null;
        var groups = new List<double>(); var pending = 0d;
        foreach (var paragraph in paragraphs)
        {
            var lines = cache.Layout(paragraph, flow.Settings.ColumnWidth, index.StartOf(paragraph), document.DefaultTabStop);
            if (groups.Count + lines.Count > 50_000) return null;
            for (var i = 0; i < lines.Count; i++)
            {
                pending += lines[i].Height;
                if (i == 0) pending += Math.Max(0, paragraph.Format.SpaceBefore);
                if (i == lines.Count - 1) pending += Math.Max(0, paragraph.Format.SpaceAfter);
                var join = i < lines.Count - 1
                    ? paragraph.Format.KeepLinesTogether || paragraph.Format.WidowControl && (i == 0 || i == lines.Count - 2)
                    : paragraph.Format.KeepWithNext;
                if (!join) { groups.Add(pending); pending = 0; }
            }
        }
        if (pending > 0) groups.Add(pending);
        if (groups.Count == 0) return null;
        var maximum = flow.Capacity;
        bool Fits(double height)
        {
            var columns = 1; var used = 0d;
            foreach (var group in groups)
            {
                if (group > height + 0.00001) return false;
                if (used > 0 && used + group > height + 0.00001) { columns++; used = 0; }
                if (columns > flow.Settings.Columns) return false;
                used += group;
            }
            return true;
        }
        if (!Fits(maximum)) return null;
        var low = Math.Max(groups.Max(), groups.Sum() / flow.Settings.Columns); var high = maximum;
        for (var i = 0; i < 32 && high - low > 1d / 1024; i++)
        {
            var middle = (low + high) / 2;
            if (Fits(middle)) high = middle; else low = middle;
        }
        return Math.Min(maximum, high + 1d / 1024);
    }
}
