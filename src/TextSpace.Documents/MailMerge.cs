using System.Text;
using TextSpace.Core;

namespace TextSpace.Documents;

public sealed record MergeData(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string>> Records);

/// <summary>Bounded RFC-4180-style CSV input and rich-run-safe «field» substitution.</summary>
public static class MailMerge
{
    public static MergeData ParseCsv(string csv)
    {
        if (csv.Length > 4_000_000) throw new InvalidDataException("Recipient data exceeds 4 MB.");
        var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        void EndField() { row.Add(field.ToString()); field.Clear(); if (row.Count > 200) throw new InvalidDataException("Too many recipient columns."); }
        void EndRow() { EndField(); if (row.Any(v => v.Length > 0)) rows.Add(row); row = []; if (rows.Count > 10_001) throw new InvalidDataException("Too many recipient records."); }
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (c == '"') { if (quoted && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; } else if (field.Length == 0 || quoted) quoted = !quoted; else field.Append(c); }
            else if (c == ',' && !quoted) EndField();
            else if (c is '\r' or '\n' && !quoted) { if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++; EndRow(); }
            else field.Append(c);
        }
        if (quoted) throw new InvalidDataException("A quoted CSV field is not closed.");
        if (field.Length > 0 || row.Count > 0) EndRow();
        if (rows.Count == 0) throw new InvalidDataException("The recipient file is empty.");
        var columns = rows[0].Select((name, i) => string.IsNullOrWhiteSpace(name) ? "Field" + (i + 1) : name.Trim().TrimStart('\uFEFF')).ToArray();
        if (columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length) throw new InvalidDataException("Recipient column names must be unique.");
        var records = rows.Skip(1).Select(values => (IReadOnlyDictionary<string, string>)columns.Select((column, i) => (column, value: i < values.Count ? values[i] : "")).ToDictionary(x => x.column, x => x.value, StringComparer.OrdinalIgnoreCase)).ToArray();
        return new(columns, records);
    }
    public static DocumentModel Merge(DocumentModel template, IReadOnlyDictionary<string, string> record)
    {
        var result = DocumentJson.Clone(template);
        foreach (var paragraph in result.Paragraphs())
        {
            var text = paragraph.Text; var replacements = new List<(int Start, int Length, string Text)>();
            foreach (var pair in record)
            {
                var token = "«" + pair.Key + "»"; var at = 0;
                while ((at = text.IndexOf(token, at, StringComparison.OrdinalIgnoreCase)) >= 0) { replacements.Add((at, token.Length, pair.Value.Replace("\r\n", "\u2028").Replace('\n', '\u2028').Replace('\r', '\u2028'))); at += token.Length; }
            }
            foreach (var replacement in replacements.OrderByDescending(r => r.Start))
            {
                var style = paragraph.StyleAt(replacement.Start); var before = paragraph.Slice(0, replacement.Start); var after = paragraph.Slice(replacement.Start + replacement.Length, paragraph.Length - replacement.Start - replacement.Length);
                paragraph.Runs = [.. before, new(replacement.Text, style), .. after]; paragraph.Normalize();
            }
        }
        result.Comments.Clear(); result.Changes.Clear(); result.Id = Guid.NewGuid().ToString("N"); result.Modified = DateTimeOffset.UtcNow; DocumentJson.Validate(result); return result;
    }
}
