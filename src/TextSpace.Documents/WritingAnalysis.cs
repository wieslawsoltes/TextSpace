using System.Text.RegularExpressions;
using TextSpace.Core;

namespace TextSpace.Documents;

public sealed record WritingIssue(string Kind, string Message, int Start, int Length, string? Replacement = null);
public sealed record WritingStatistics(int Words, int Characters, int CharactersWithoutSpaces, int Paragraphs, int Sentences, int ReadingMinutes);

/// <summary>Transparent, offline writing checks. This is not a dictionary-based spelling or AI grammar service.</summary>
public static class WritingAnalysis
{
    public static WritingStatistics Statistics(DocumentModel document)
    {
        var text = document.PlainText; var words = document.WordCount;
        return new(words, text.Length, text.Count(c => !char.IsWhiteSpace(c)), document.Paragraphs().Count(), Regex.Matches(text, @"[^.!?\n]+[.!?]|\b[^.!?\n]+$").Count, Math.Max(1, (int)Math.Ceiling(words / 220d)));
    }
    public static IReadOnlyList<WritingIssue> Check(DocumentModel document)
    {
        var text = document.PlainText; var result = new List<WritingIssue>();
        foreach (Match match in Regex.Matches(text, @"\b([\p{L}]+)( +)\1\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(300))) result.Add(new("Repeated word", "A word appears twice in a row.", match.Index, match.Length, match.Groups[1].Value));
        foreach (Match match in Regex.Matches(text, " {2,}", RegexOptions.None, TimeSpan.FromMilliseconds(300))) result.Add(new("Spacing", "Use a single space between words.", match.Index, match.Length, " "));
        foreach (Match match in Regex.Matches(text, @" +[,.!?;:]", RegexOptions.None, TimeSpan.FromMilliseconds(300))) result.Add(new("Punctuation", "Remove the space before punctuation.", match.Index, match.Length, match.Value.TrimStart()));
        foreach (Match match in Regex.Matches(text, @"[^.!?\n]{220,}[.!?]", RegexOptions.None, TimeSpan.FromMilliseconds(300))) result.Add(new("Clarity", "This sentence is long. Consider splitting it into shorter sentences.", match.Index, match.Length));
        return result.OrderBy(r => r.Start).Take(200).ToArray();
    }
}
