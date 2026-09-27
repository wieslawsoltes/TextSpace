using System.Globalization;
using TextSpace.Core;

namespace TextSpace.Documents;

public sealed record FieldContext
{
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;
    public string CultureName { get; init; } = "en-US";
    public Func<int, FieldPageInfo>? PageAt { get; init; }
}

public sealed record FieldResult(string Id, string Value, bool Evaluated, string? Error = null);

/// <summary>Pure bounded evaluation. No scripts, files, network resources, processes or external field providers are invoked.</summary>
public static class FieldEngine
{
    public const int MaximumResultLength = 64_000;

    public static IReadOnlyList<FieldResult> Evaluate(DocumentModel document, FieldContext? context = null)
    {
        context ??= new();
        var culture = CultureInfo.GetCultureInfo(context.CultureName);
        var index = new TextIndex(document); var fields = document.Fields.OrderBy(f => f.Start).ToArray();
        var memo = new Dictionary<string, FieldResult>(StringComparer.Ordinal); var active = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); var sequenceValues = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            try
            {
                var instruction = FieldInstruction.Parse(field.Instruction);
                if (!instruction.Supported || instruction.Name != "SEQ") continue;
                var current = sequences.GetValueOrDefault(instruction.Argument);
                current = instruction.Restart ?? (instruction.Repeat ? current : checked(current + 1));
                sequences[instruction.Argument] = current; sequenceValues[field.Id] = current;
            }
            catch (FormatException) { }
        }
        FieldResult EvaluateField(DocumentField field)
        {
            if (memo.TryGetValue(field.Id, out var found)) return found;
            var cached = index.Text.Substring(field.Start, field.End - field.Start);
            if (field.Locked) return new(field.Id, cached, false);
            if (!active.Add(field.Id)) return new(field.Id, "Error! Circular reference.", true, "Circular field dependency.");
            if (active.Count > 64) { active.Remove(field.Id); return new(field.Id, "Error! Reference depth exceeded.", true, "Reference depth exceeds 64."); }
            FieldResult result;
            try
            {
                var instruction = FieldInstruction.Parse(field.Instruction);
                if (!instruction.Supported) return new(field.Id, cached, false, "Unsupported instruction retained without evaluation.");
                var page = context.PageAt?.Invoke(field.Start);
                string Number(int value, PageNumberStyle fallback = PageNumberStyle.Decimal) => DocumentSections.FormatNumber(value, instruction.NumberFormat switch
                {
                    "ROMAN" => PageNumberStyle.UpperRoman, "roman" => PageNumberStyle.LowerRoman, "ALPHABETIC" => PageNumberStyle.UpperLetter,
                    "alphabetic" => PageNumberStyle.LowerLetter, "Arabic" => PageNumberStyle.Decimal, _ => fallback
                });
                string Date(DateTimeOffset date, bool time = false) => date.ToString(instruction.DateFormat ?? (time ? "HH:mm" : "MMMM d, yyyy"), culture);
                string Reference(bool pageReference)
                {
                    var bookmark = document.Bookmarks.FirstOrDefault(b => b.Name.Equals(instruction.Argument, StringComparison.OrdinalIgnoreCase));
                    if (bookmark is null) throw new InvalidOperationException("Reference source not found.");
                    if (pageReference)
                    {
                        var info = context.PageAt?.Invoke(bookmark.Start) ?? throw new InvalidOperationException("Page layout is required.");
                        return Number(info.PageNumber, info.NumberStyle);
                    }
                    var text = index.Text.Substring(bookmark.Start, bookmark.End - bookmark.Start);
                    foreach (var dependency in fields.Where(f => f.Start >= bookmark.Start && f.End <= bookmark.End).OrderByDescending(f => f.Start))
                    {
                        var nested = EvaluateField(dependency);
                        if (nested.Error is not null && nested.Evaluated) throw new InvalidOperationException(nested.Error);
                        text = text[..(dependency.Start - bookmark.Start)] + nested.Value + text[(dependency.End - bookmark.Start)..];
                        if (text.Length > MaximumResultLength) throw new InvalidOperationException("Reference result exceeds the limit.");
                    }
                    return text.Replace('\n', '\u2028');
                }
                if (instruction.Name is "PAGE" or "NUMPAGES" or "SECTION" or "SECTIONPAGES" or "PAGEREF" && page is null)
                    return new(field.Id, cached, false, "Page layout is required to update this field.");
                var value = instruction.Name switch
                {
                    "TITLE" => document.Title, "AUTHOR" => document.Author, "SUBJECT" => document.Subject, "FILENAME" => document.Title + ".docx",
                    "NUMWORDS" => Number(document.WordCount), "NUMCHARS" => Number(index.Text.Count(ch => !char.IsWhiteSpace(ch))),
                    "PAGE" => Number(page!.Value.PageNumber, page.Value.NumberStyle), "NUMPAGES" => Number(page!.Value.PageCount),
                    "SECTION" => Number(page!.Value.SectionNumber), "SECTIONPAGES" => Number(page!.Value.SectionPages),
                    "DATE" => Date(context.Now), "TIME" => Date(context.Now, true), "CREATEDATE" => Date(document.Created), "SAVEDATE" => Date(document.Modified),
                    "SEQ" => Number(sequenceValues.GetValueOrDefault(field.Id)), "REF" => Reference(false), "PAGEREF" => Reference(true),
                    _ => cached
                };
                value = value.Replace("\r\n", "\u2028").Replace('\r', '\u2028').Replace('\n', '\u2028');
                if (value.Length == 0) value = "\u200B"; // A stable, zero-width cached range remains addressable.
                if (value.Length > MaximumResultLength) throw new InvalidOperationException("Field result exceeds 64,000 characters.");
                result = new(field.Id, value, true);
            }
            catch (Exception error) when (error is FormatException or InvalidOperationException or ArgumentException or OverflowException)
            {
                result = new(field.Id, "Error! " + error.Message, true, error.Message);
            }
            finally { active.Remove(field.Id); }
            memo[field.Id] = result; return result;
        }
        return fields.Select(EvaluateField).ToArray();
    }
}
