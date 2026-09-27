namespace TextSpace.Core;

/// <summary>A named range in UTF-16 document coordinates; collapsed bookmarks have right insertion gravity.</summary>
public sealed class Bookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Bookmark";
    public int Start { get; set; }
    public int End { get; set; }

    public static bool IsValidName(string? name) => name is { Length: > 0 and <= 40 }
        && (char.IsAsciiLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
