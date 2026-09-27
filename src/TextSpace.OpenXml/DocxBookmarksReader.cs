using TextSpace.Core;

namespace TextSpace.OpenXml;

public sealed partial class DocxReader
{
    private void LoadBookmarks(DocumentModel document)
    {
        var index = new TextIndex(document);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _bookmarkStarts)
        {
            var (name, start) = pair.Value;
            if (!Bookmark.IsValidName(name) || !names.Add(name)
                || !_bookmarkEnds.TryGetValue(pair.Key, out var end) || end < start)
            {
                Warn("An invalid, duplicate, or unmatched bookmark was omitted.");
                continue;
            }
            if (document.Bookmarks.Count >= 10_000) throw new InvalidDataException("Too many bookmarks.");
            var snappedStart = index.Snap(Math.Clamp(start, 0, index.Length));
            var snappedEnd = start == end ? snappedStart : index.Snap(Math.Clamp(end, snappedStart, index.Length), true);
            document.Bookmarks.Add(new() { Name = name, Start = snappedStart, End = snappedEnd });
        }
    }
}
