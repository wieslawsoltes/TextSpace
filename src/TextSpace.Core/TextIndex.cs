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
    public int End { get; } = Start + Paragraph.Length;
}

/// <summary>
/// Immutable UTF-16 text-coordinate snapshot. Paragraph lookup is logarithmic;
/// identity lookup and repeated grapheme snapping reuse per-snapshot indexes.
/// Rebuild the index after mutating the source document.
/// </summary>
public sealed class TextIndex
{
    private readonly ParagraphAddress[] _paragraphs;
    private readonly int[] _ends;
    private readonly Dictionary<Paragraph, int> _identity = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, int[]> _graphemes = [];
    public IReadOnlyList<ParagraphAddress> Paragraphs => _paragraphs;
    public string Text { get; }
    public int Length => Text.Length;

    public TextIndex(DocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var addresses = new List<ParagraphAddress>(); var ends = new List<int>(); var text = new StringBuilder();
        void Walk(List<Block> blocks, TableBlock? owner)
        {
            foreach (var block in blocks)
            {
                if (block is Paragraph paragraph)
                {
                    if (addresses.Count > 0) text.Append('\n');
                    _identity.Add(paragraph, addresses.Count);
                    addresses.Add(new(paragraph, blocks, text.Length, owner));
                    foreach (var run in paragraph.Runs) text.Append(run.Text);
                    ends.Add(text.Length);
                }
                else if (block is TableBlock table)
                    foreach (var row in table.Rows)
                        foreach (var cell in row.Cells) Walk(cell.Blocks, table);
            }
        }
        Walk(document.Blocks, null);
        _paragraphs = addresses.ToArray(); _ends = ends.ToArray(); Text = text.ToString();
    }

    private int Find(int position)
    {
        if (_paragraphs.Length == 0) throw new InvalidOperationException("A document must contain an editable paragraph.");
        var low = 0; var high = _ends.Length - 1;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (position <= _ends[middle]) high = middle;
            else low = middle + 1;
        }
        return low;
    }

    public ParagraphAddress At(int position) => _paragraphs[Find(Math.Clamp(position, 0, Length))];

    public int StartOf(Paragraph paragraph) => _identity.TryGetValue(paragraph, out var index)
        ? _paragraphs[index].Start
        : throw new ArgumentException("The paragraph is not part of this text index.", nameof(paragraph));

    public int Snap(int position, bool forward = false)
    {
        position = Math.Clamp(position, 0, Length);
        if (position == 0 || position == Length) return position;
        var paragraphIndex = Find(position); var paragraph = _paragraphs[paragraphIndex]; var offset = position - paragraph.Start;
        if (position == _ends[paragraphIndex] || offset == 0) return position;
        if (!_graphemes.TryGetValue(paragraphIndex, out var boundaries))
        {
            boundaries = StringInfo.ParseCombiningCharacters(Text.Substring(paragraph.Start, _ends[paragraphIndex] - paragraph.Start));
            _graphemes.Add(paragraphIndex, boundaries);
        }
        var index = Array.BinarySearch(boundaries, offset);
        if (index >= 0) return position;
        index = ~index;
        return forward
            ? index < boundaries.Length ? paragraph.Start + boundaries[index] : _ends[paragraphIndex]
            : index > 0 ? paragraph.Start + boundaries[index - 1] : paragraph.Start;
    }

    public int Next(int position)
    {
        position = Math.Clamp(position, 0, Length);
        return Snap(position < Length ? position + 1 : Length, true);
    }
    public int Previous(int position)
    {
        position = Math.Clamp(position, 0, Length);
        return Snap(position > 0 ? position - 1 : 0);
    }
}
