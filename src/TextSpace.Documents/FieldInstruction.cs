using System.Globalization;
using System.Text;
using TextSpace.Core;

namespace TextSpace.Documents;

/// <summary>Bounded parser for a non-executing subset of Word field instructions.</summary>
public sealed record FieldInstruction(string Name, string Argument, string? NumberFormat, string? DateFormat, int? Restart, bool Repeat, bool Hyperlink, bool Supported)
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "TITLE", "AUTHOR", "SUBJECT", "FILENAME", "NUMWORDS", "NUMCHARS", "PAGE", "NUMPAGES", "SECTION", "SECTIONPAGES",
        "REF", "PAGEREF", "SEQ", "DATE", "TIME", "CREATEDATE", "SAVEDATE"
    };

    public static FieldInstruction Parse(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 1024 || code.Any(char.IsControl)) throw new FormatException("A field instruction must contain 1–1,024 printable characters.");
        var tokens = new List<string>(); var buffer = new StringBuilder(); var quoted = false;
        foreach (var ch in code)
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(ch) && !quoted) { if (buffer.Length > 0) { tokens.Add(buffer.ToString()); buffer.Clear(); } }
            else buffer.Append(ch);
        }
        if (quoted) throw new FormatException("A quoted field argument is not closed.");
        if (buffer.Length > 0) tokens.Add(buffer.ToString());
        if (tokens.Count == 0) throw new FormatException("A field name is required.");
        var name = tokens[0].ToUpperInvariant(); var argument = ""; string? numberFormat = null, dateFormat = null;
        int? restart = null; var repeat = false; var hyperlink = false; var supported = Names.Contains(name);
        for (var i = 1; i < tokens.Count; i++)
        {
            string Operand() => ++i < tokens.Count ? tokens[i] : throw new FormatException("Missing field switch argument.");
            switch (tokens[i])
            {
                case "\\*":
                    var format = Operand();
                    if (!format.Equals("MERGEFORMAT", StringComparison.OrdinalIgnoreCase) && !format.Equals("CHARFORMAT", StringComparison.OrdinalIgnoreCase))
                    {
                        numberFormat = format;
                        if (format is not ("Arabic" or "ROMAN" or "roman" or "ALPHABETIC" or "alphabetic")) supported = false;
                    }
                    break;
                case "\\@": dateFormat = Operand(); if (dateFormat.Length > 100) throw new FormatException("The date format is too long."); break;
                case "\\r": if (!int.TryParse(Operand(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value is < 1 or > 1_000_000) throw new FormatException("Sequence restart must be between 1 and 1,000,000."); restart = value; break;
                case "\\c": repeat = true; break;
                case "\\h": hyperlink = true; break;
                default:
                    if (tokens[i].StartsWith('\\') || argument.Length != 0) supported = false;
                    else argument = tokens[i];
                    break;
            }
        }
        if (name is "REF" or "PAGEREF" or "SEQ")
        {
            if (!Bookmark.IsValidName(argument)) throw new FormatException("This field requires a valid bookmark or sequence identifier.");
        }
        else if (argument.Length != 0) supported = false;
        if (dateFormat is not null && name is not ("DATE" or "TIME" or "CREATEDATE" or "SAVEDATE")) supported = false;
        if ((restart is not null || repeat) && name != "SEQ") supported = false;
        if (hyperlink && name is not ("REF" or "PAGEREF")) supported = false;
        return new(name, argument, numberFormat, dateFormat, restart, repeat, hyperlink, supported);
    }

    public string RenameTarget(string original, string replacement, string source)
    {
        if (Name is not ("REF" or "PAGEREF") || !Argument.Equals(original, StringComparison.OrdinalIgnoreCase)) return source;
        // Accepted bookmark names contain no spaces or quotes. Replace exactly the argument token, not unrelated switches.
        var at = source.IndexOf(Argument, source.IndexOf(Name, StringComparison.OrdinalIgnoreCase) + Name.Length, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? source : source[..at] + replacement + source[(at + Argument.Length)..];
    }
}
