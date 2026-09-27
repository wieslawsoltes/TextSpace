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
            var comments = Document.Comments.Select(c => (Comment: c, Start: c.Start, End: c.End)).ToArray();
            var changes = Document.Changes.Select(c => (Change: c, Start: c.Start, End: c.Start + c.Inserted.Length)).ToArray();
            mutation();
            var next = Index;
            var surviving = next.Paragraphs.ToDictionary(a => a.Paragraph.Id, StringComparer.Ordinal);
            int Map(int position)
            {
                position = Math.Clamp(position, 0, oldIndex.Length);
                var at = Array.FindIndex(addresses, a => position <= a.End);
                if (at < 0) at = addresses.Length - 1;
                if (at < 0) return 0;
                var address = addresses[at];
                if (surviving.TryGetValue(address.Id, out var same))
                    return same.Start + Math.Clamp(position - address.Start, 0, same.Paragraph.Length);
                for (var i = at + 1; i < addresses.Length; i++)
                    if (surviving.TryGetValue(addresses[i].Id, out var after)) return after.Start;
                for (var i = at - 1; i >= 0; i--)
                    if (surviving.TryGetValue(addresses[i].Id, out var before)) return before.End;
                return 0;
            }
            foreach (var anchor in comments)
            {
                anchor.Comment.Start = Map(anchor.Start);
                anchor.Comment.End = Math.Max(anchor.Comment.Start, Map(anchor.End));
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
