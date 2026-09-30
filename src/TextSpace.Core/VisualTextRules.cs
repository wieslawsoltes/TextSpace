namespace TextSpace.Core;

/// <summary>Unicode and XML-safe text shared by native visual-object editing and export.</summary>
public static class VisualTextRules
{
    public static bool IsValid(string? text, int maximumCharacters, bool allowLineBreaks = true)
    {
        if (text is null || text.Length > maximumCharacters) return false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\uFFFE' or '\uFFFF') return false;
            if (char.IsHighSurrogate(c))
            {
                if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false;
            }
            else if (char.IsLowSurrogate(c)) return false;
            else if (char.IsControl(c) && !(allowLineBreaks && c is '\t' or '\r' or '\n')) return false;
        }
        return true;
    }
}
