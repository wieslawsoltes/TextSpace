using System.Text.RegularExpressions;

namespace TextSpace.Core;

/// <summary>
/// Explicit captured text and statistics, independent of later document mutations.
/// Create a new snapshot after an edit; no global or identity-only model cache is used.
/// </summary>
public sealed partial class DocumentTextSnapshot
{
    public string Text { get; }
    public int WordCount { get; }
    public int CharactersWithoutSpaces { get; }
    public DocumentTextSnapshot(DocumentModel document) : this(new TextIndex(document).Text) { }
    public DocumentTextSnapshot(string text)
    {
        ArgumentNullException.ThrowIfNull(text); Text = text;
        WordCount = CountWords(text);
        var characters = 0; foreach (var c in text) if (!char.IsWhiteSpace(c)) characters++;
        CharactersWithoutSpaces = characters;
    }
    public int WordsIn(TextSelection selection)
    {
        if (selection.Start < 0 || selection.End > Text.Length) throw new ArgumentOutOfRangeException(nameof(selection));
        return CountWords(Text.AsSpan(selection.Start, selection.Length));
    }
    public static int CountWords(ReadOnlySpan<char> text) => WordPattern().Count(text);
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
