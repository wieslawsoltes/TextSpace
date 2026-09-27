using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    /// <summary>Adds a named bookmark, or moves the existing bookmark to the current selection.</summary>
    public void SetBookmark(string name)
    {
        ValidateBookmarkName(name);
        Execute("Set bookmark", () =>
        {
            var bookmark = Document.Bookmarks.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (bookmark is null)
            {
                bookmark = new() { Name = name };
                Document.Bookmarks.Add(bookmark);
            }
            bookmark.Start = Selection.Start;
            bookmark.End = Selection.End;
        });
    }

    public void RenameBookmark(string name, string replacement)
    {
        ValidateBookmarkName(replacement);
        Execute("Rename bookmark", () =>
        {
            var bookmark = FindBookmark(name) ?? throw new InvalidOperationException("The bookmark no longer exists.");
            if (Document.Bookmarks.Any(b => b.Id != bookmark.Id && b.Name.Equals(replacement, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A bookmark with this name already exists.");
            var original = bookmark.Name;
            bookmark.Name = replacement;
            foreach (var run in Document.Paragraphs().SelectMany(p => p.Runs))
                if (run.Style.Hyperlink is { } target && target.Equals("#" + original, StringComparison.OrdinalIgnoreCase))
                    run.Style = run.Style with { Hyperlink = "#" + replacement };
        });
    }

    public void DeleteBookmark(string name) => Execute("Delete bookmark", () =>
        Document.Bookmarks.RemoveAll(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

    public Bookmark? FindBookmark(string name) => Document.Bookmarks.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Navigation is allowed in reading mode and does not create an undo record.</summary>
    public bool GoToBookmark(string name)
    {
        var bookmark = FindBookmark(name);
        if (bookmark is null) return false;
        SetSelection(bookmark.Start, bookmark.End);
        return true;
    }

    private static void ValidateBookmarkName(string name)
    {
        if (!Bookmark.IsValidName(name))
            throw new ArgumentException("Use up to 40 ASCII letters, digits, or underscores; begin with a letter or underscore.", nameof(name));
    }
}
