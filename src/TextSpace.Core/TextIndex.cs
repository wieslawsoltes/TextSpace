using System.Globalization;
using System.Text;

namespace TextSpace.Core;

public readonly record struct TextSelection(int Anchor, int Active)
{
    public int Start => Math.Min(Anchor, Active);
    public int End => Math.Max(Anchor, Active);
    public int Length => End - Start;
    public bool IsEmpty => Anchor == Active;
}

public sealed record ParagraphAddress(Paragraph Paragraph, List<Block> Container, int Start, TableBlock? Table)
{
    public int End => Start + Paragraph.Length;
}

/// <summary>Stable UTF-16 coordinate space. Newline separators are structural, not stored inside runs.</summary>
public sealed class TextIndex
{
    public IReadOnlyList<ParagraphAddress> Paragraphs { get; }
    public string Text { get; }
    public int Length => Text.Length;
    public TextIndex(DocumentModel document)
    {
        var addresses = new List<ParagraphAddress>(); var text = new StringBuilder();
        void Walk(List<Block> blocks, TableBlock? owner)
        {
            foreach (var block in blocks)
            {
                if (block is Paragraph p)
                {
                    if (addresses.Count > 0) text.Append('\n');
                    addresses.Add(new(p, blocks, text.Length, owner)); text.Append(p.Text);
                }
                else if (block is TableBlock table)
                    foreach (var cell in table.Rows.SelectMany(r => r.Cells)) Walk(cell.Blocks, table);
            }
        }
        Walk(document.Blocks, null); Paragraphs = addresses; Text = text.ToString();
    }
    public ParagraphAddress At(int position)
    {
        if (Paragraphs.Count == 0) throw new InvalidOperationException("A document must contain an editable paragraph.");
        position = Math.Clamp(position, 0, Length);
        foreach (var p in Paragraphs) if (position <= p.End) return p;
        return Paragraphs[^1];
    }
    public int StartOf(Paragraph paragraph) => Paragraphs.First(p => ReferenceEquals(p.Paragraph, paragraph)).Start;
    public int Snap(int position, bool forward = false)
    {
        position = Math.Clamp(position, 0, Length);
        if (position == 0 || position == Length) return position;
        var p = At(position); var offset = position - p.Start;
        if (offset == p.Paragraph.Length) return position;
        var boundaries = StringInfo.ParseCombiningCharacters(p.Paragraph.Text);
        var index = Array.BinarySearch(boundaries, offset);
        if (index >= 0) return position;
        index = ~index;
        return p.Start + (forward ? index < boundaries.Length ? boundaries[index] : p.Paragraph.Length : index > 0 ? boundaries[index - 1] : 0);
    }
    public int Next(int position) => Snap(Math.Min(Length, position + 1), true);
    public int Previous(int position) => Snap(Math.Max(0, position - 1));
}
