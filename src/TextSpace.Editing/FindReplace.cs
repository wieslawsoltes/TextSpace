using System.Text.RegularExpressions;

namespace TextSpace.Editing;

public sealed record SearchMatch(int Start, int Length, string Preview);

public sealed partial class EditorSession
{
    public IReadOnlyList<SearchMatch> Find(string query, bool matchCase = false, bool wholeWord = false)
    {
        if (string.IsNullOrEmpty(query)) return [];
        var text = Index.Text; var pattern = Regex.Escape(query);
        if (wholeWord) pattern = @"(?<![\p{L}\p{N}_])" + pattern + @"(?![\p{L}\p{N}_])";
        var regex = new Regex(pattern, matchCase ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
        return regex.Matches(text).Cast<Match>().Take(10_000).Select(m => new SearchMatch(m.Index, m.Length, text.Substring(Math.Max(0, m.Index - 24), Math.Min(text.Length, m.Index + m.Length + 50) - Math.Max(0, m.Index - 24)).Replace('\n', ' '))).ToArray();
    }
    public int ReplaceAll(string query, string replacement, bool matchCase = false, bool wholeWord = false)
    {
        var matches = Find(query, matchCase, wholeWord); if (matches.Count == 0) return 0;
        Execute("Replace all", () => { foreach (var match in matches.Reverse()) Replace(match.Start, match.Length, replacement, "Replace all"); });
        return matches.Count;
    }
    public void SelectWord(int position)
    {
        var index = Index; var text = index.Text; if (text.Length == 0) return;
        position = Math.Clamp(position, 0, text.Length - 1); var start = position; var end = position;
        bool Word(char c) => char.IsLetterOrDigit(c) || c is '_' or '\'' or '’';
        if (Word(text[position])) { while (start > 0 && Word(text[start - 1])) start--; while (end < text.Length && Word(text[end])) end++; }
        else end = index.Next(position);
        SetSelection(start, end);
    }
    public int WordBoundary(int position, int direction)
    {
        var text = Index.Text; position = Math.Clamp(position, 0, text.Length);
        if (direction < 0) { while (position > 0 && char.IsWhiteSpace(text[position - 1])) position--; while (position > 0 && !char.IsWhiteSpace(text[position - 1])) position--; }
        else { while (position < text.Length && !char.IsWhiteSpace(text[position])) position++; while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        return Index.Snap(position, direction > 0);
    }
}
