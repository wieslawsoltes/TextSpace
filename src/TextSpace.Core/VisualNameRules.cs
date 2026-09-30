using System.Text;

namespace TextSpace.Core;

/// <summary>Bounded, single-line object labels. Names need not be unique; IDs remain authoritative.</summary>
public static class VisualNameRules
{
    public const int MaximumLength = 256;

    public static bool IsValid(string? name) => VisualTextRules.IsValid(name, MaximumLength, false)
        && !name!.Contains('\u2028') && !name.Contains('\u2029');

    /// <summary>Normalizes external names without splitting a Unicode scalar. Callers must report any change.</summary>
    public static string NormalizeImported(string? name)
    {
        if (name is null) return "";
        if (IsValid(name)) return name;
        var result = new StringBuilder(Math.Min(name.Length, MaximumLength));
        Span<char> buffer = stackalloc char[2];
        foreach (var rune in name.EnumerateRunes())
        {
            var value = Rune.IsControl(rune) || rune.Value is 0x2028 or 0x2029 or 0xFFFE or 0xFFFF ? new Rune(' ') : rune;
            var count = value.EncodeToUtf16(buffer);
            if (result.Length + count > MaximumLength) break;
            result.Append(buffer[..count]);
        }
        return result.ToString();
    }
}
