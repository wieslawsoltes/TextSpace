using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    private sealed record AddressSnapshot(string Id, int Start, int End);

    /// <summary>
    /// Applies a table-structure edit without relying on ambiguous text diffs.
    /// Surviving paragraph identities retain their local offsets; anchors inside
    /// removed cells collapse to the next surviving paragraph, or the previous end.
    /// </summary>
    private void StructuralEdit(string label, Action mutation, bool preserveSelection = true)
    {
        Execute(label, () =>
        {
            var oldIndex = Index;
            var addresses = oldIndex.Paragraphs.Select(a => new AddressSnapshot(a.Paragraph.Id, a.Start, a.End)).ToArray();
            var selection = Selection;
            var fields = Document.Fields.Select(f => (Field: f, Start: f.Start, End: f.End, Text: oldIndex.Text.Substring(f.Start, f.End - f.Start))).ToArray();
            var bookmarks = Document.Bookmarks.Select(b => (Bookmark: b, Start: b.Start, End: b.End)).ToArray();
            var comments = Document.Comments.Select(c => (Comment: c, Start: c.Start, End: c.End)).ToArray();
            var changes = Document.Changes.Select(c => (Change: c, Start: c.Start, End: c.Start + c.Inserted.Length)).ToArray();
            mutation();
            var next = Index;
            var surviving = next.Paragraphs.ToDictionary(a => a.Paragraph.Id, StringComparer.Ordinal);
            // Resolve surviving identities and nearest fallbacks once. Range remapping
            // is O(log paragraphCount), not a linear search for every review/bookmark endpoint.
            var mapped = addresses.Select(a => surviving.GetValueOrDefault(a.Id)).ToArray();
            var following = new int[addresses.Length]; var preceding = new int[addresses.Length];
            var nextStart = -1; var previousEnd = 0;
            for (var i = addresses.Length - 1; i >= 0; i--)
            {
                following[i] = nextStart;
                if (mapped[i] is { } item) nextStart = item.Start;
            }
            for (var i = 0; i < addresses.Length; i++)
            {
                preceding[i] = previousEnd;
                if (mapped[i] is { } item) previousEnd = item.End;
            }
            int Map(int position)
            {
                position = Math.Clamp(position, 0, oldIndex.Length);
                var low = 0; var high = addresses.Length;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    if (addresses[middle].End < position) low = middle + 1; else high = middle;
                }
                var at = Math.Min(low, addresses.Length - 1);
                if (at < 0) return 0;
                if (mapped[at] is { } same)
                    return same.Start + Math.Clamp(position - addresses[at].Start, 0, same.End - same.Start);
                return following[at] >= 0 ? following[at] : preceding[at];
            }
            foreach (var anchor in fields)
            {
                var start = Map(anchor.Start); var end = Map(anchor.End);
                if (end <= start || end - start != anchor.Text.Length || !next.Text.AsSpan(start, end - start).SequenceEqual(anchor.Text)
                    || !ReferenceEquals(next.At(start).Paragraph, next.At(end).Paragraph)) Document.Fields.Remove(anchor.Field);
                else { anchor.Field.Start = start; anchor.Field.End = end; }
            }
            foreach (var anchor in comments)
            {
                anchor.Comment.Start = Map(anchor.Start);
                anchor.Comment.End = Math.Max(anchor.Comment.Start, Map(anchor.End));
            }
            foreach (var anchor in bookmarks)
            {
                anchor.Bookmark.Start = Map(anchor.Start);
                anchor.Bookmark.End = Math.Max(anchor.Bookmark.Start, Map(anchor.End));
            }
            foreach (var anchor in changes)
            {
                var change = anchor.Change;
                change.Start = Map(anchor.Start);
                var end = Map(anchor.End);
                if (end - change.Start != change.Inserted.Length ||
                    change.Inserted.Length > next.Length - change.Start ||
                    !next.Text.AsSpan(change.Start, change.Inserted.Length).SequenceEqual(change.Inserted))
                    change.CanReject = false;
            }
            if (preserveSelection) Selection = new(next.Snap(Map(selection.Anchor)), next.Snap(Map(selection.Active)));
        });
    }
}
