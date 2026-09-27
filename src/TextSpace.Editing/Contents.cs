using System.Security.Cryptography;
using System.Text;
using TextSpace.Core;
using TextSpace.Documents;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    /// <summary>Creates an editable contents block with live REF and PAGEREF results, as one history transaction.</summary>
    public int InsertTableOfContents(bool updateExisting = false, Func<DocumentModel, FieldContext>? contextFactory = null)
    {
        EnsureWritable();
        if (CurrentTable is not null) throw new InvalidOperationException("Insert a table of contents outside table cells.");
        var headings = Index.Paragraphs.Where(p => p.Paragraph.Format.OutlineLevel > 0 && !string.IsNullOrWhiteSpace(p.Paragraph.Text))
            .Select(p => (p.Start, p.End, Text: p.Paragraph.Text, p.Paragraph.Format, p.Paragraph.DefaultStyle,
                Name: "_TextSpaceToc_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Paragraph.Id)))[..24])).ToArray();
        if (headings.Length is < 1 or > 1000) throw new InvalidOperationException("A contents block requires between 1 and 1,000 nonempty heading paragraphs.");
        Execute(updateExisting ? "Update table of contents" : "Insert table of contents", () =>
        {
            if (updateExisting)
            {
                var paragraphs = Index.Paragraphs.ToArray(); var at = Array.FindIndex(paragraphs, p => p.Paragraph.Format.StyleName == "TOCHeading");
                if (at >= 0)
                {
                    var last = at;
                    while (last + 1 < paragraphs.Length && paragraphs[last + 1].Paragraph.Format.StyleName.StartsWith("TOC", StringComparison.Ordinal)) last++;
                    SetSelection(paragraphs[at].Start, Math.Min(Index.Length, paragraphs[last].End + 1));
                }
            }
            var start = Selection.Start; var end = Selection.End;
            if (headings.Any(h => start < h.End && end > h.Start || start == end && start > h.Start && start < h.End))
                throw new InvalidOperationException("Place the contents block outside its source heading text.");
            var suffixFormat = Index.At(end).Paragraph.Format; var suffixStyle = Index.At(end).Paragraph.DefaultStyle;
            var prefix = start > Index.At(start).Start ? "\n" : "";
            var builder = new StringBuilder(prefix + "Contents\n");
            var references = new List<(int Start, int End, string Instruction)>();
            foreach (var heading in headings)
            {
                var referenceStart = start + builder.Length; builder.Append(heading.Text);
                references.Add((referenceStart, start + builder.Length, "REF " + heading.Name + " \\h"));
                builder.Append('\t'); referenceStart = start + builder.Length; builder.Append('1');
                references.Add((referenceStart, start + builder.Length, "PAGEREF " + heading.Name + " \\h")); builder.Append('\n');
            }
            var inserted = builder.ToString(); var delta = inserted.Length - (end - start);
            InsertText(inserted);
            var suffixParagraph = Index.At(start + inserted.Length).Paragraph;
            suffixParagraph.Format = suffixFormat.StyleName.StartsWith("TOC", StringComparison.Ordinal) ? new() : suffixFormat;
            suffixParagraph.DefaultStyle = suffixStyle;
            // Rebind after insertion: a bookmark at the insertion boundary must not include the generated contents itself.
            foreach (var heading in headings)
            {
                var movedStart = heading.Start >= end ? heading.Start + delta : heading.Start;
                var movedEnd = heading.End > start ? heading.End + delta : heading.End;
                var bookmark = FindBookmark(heading.Name);
                if (bookmark is null) { bookmark = new() { Name = heading.Name }; Document.Bookmarks.Add(bookmark); }
                bookmark.Start = movedStart; bookmark.End = movedEnd;
                var paragraph = Index.At(movedStart).Paragraph;
                paragraph.Format = heading.Format; paragraph.DefaultStyle = heading.DefaultStyle;
            }
            var generated = Index.Paragraphs.Where(p => p.Start >= start + prefix.Length && p.Start < start + inserted.Length).ToArray();
            for (var i = 0; i < generated.Length; i++)
            {
                var paragraph = generated[i].Paragraph; var level = i == 0 ? 0 : headings[Math.Min(i - 1, headings.Length - 1)].Format.OutlineLevel;
                var style = new TextStyle { FontSize = i == 0 ? 20 : 11, Bold = i == 0, Color = i == 0 ? "#0F4761" : "#202020" };
                paragraph.DefaultStyle = style;
                paragraph.Format = new() { StyleName = i == 0 ? "TOCHeading" : "TOC" + level, LeftIndent = Math.Max(0, level - 1) * 12, SpaceAfter = i == 0 ? 10 : 4, KeepWithNext = i == 0,
                    TabStops = i == 0 ? [] : [new TabStop { RelativeToRightEdge = true, Alignment = TabAlignment.Right, Leader = TabLeader.Dot }] };
                foreach (var run in paragraph.Runs) run.Style = style;
            }
            foreach (var reference in references) Document.Fields.Add(new() { Start = reference.Start, End = reference.End, Instruction = reference.Instruction });
            UpdateFields(contextFactory);
        });
        return headings.Length;
    }
}
