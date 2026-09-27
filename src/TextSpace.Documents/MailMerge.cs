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
        var index = new TextIndex(result);
        var plans = new List<(Paragraph Paragraph, int Local, int Global, int Length, string Text)>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in record)
        {
            if (!unique.Add(pair.Key)) throw new InvalidDataException("Recipient field names must be unique ignoring case.");
            var token = "«" + pair.Key + "»";
            var replacement = pair.Value.Replace("\r\n", "\u2028").Replace('\n', '\u2028').Replace('\r', '\u2028');
            foreach (var entry in index.Paragraphs)
            {
                var text = entry.Paragraph.Text; var at = 0;
                while ((at = text.IndexOf(token, at, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    plans.Add((entry.Paragraph, at, entry.Start + at, token.Length, replacement));
                    at += token.Length;
                }
            }
        }
        foreach (var change in plans.OrderByDescending(p => p.Global))
        {
            var paragraph = change.Paragraph; var start = change.Global; var end = start + change.Length; var inserted = change.Text.Length;
            int Map(int position, bool right) => position < start ? position : position > end ? position + inserted - change.Length : start + (right ? inserted : 0);
            var style = paragraph.StyleAt(change.Local);
            paragraph.Runs = [.. paragraph.Slice(0, change.Local), new(change.Text, style), .. paragraph.Slice(change.Local + change.Length, paragraph.Length - change.Local - change.Length)];
            paragraph.Normalize();
            foreach (var bookmark in result.Bookmarks)
            {
                var point = bookmark.Start == bookmark.End;
                bookmark.Start = Map(bookmark.Start, point);
                bookmark.End = point ? bookmark.Start : Math.Max(bookmark.Start, Map(bookmark.End, true));
            }
            foreach (var field in result.Fields.ToArray())
            {
                if (start < field.End && end > field.Start) result.Fields.Remove(field); // Modified cached result becomes ordinary text.
                else if (field.Start >= end) { field.Start += inserted - change.Length; field.End += inserted - change.Length; }
            }
        }
        var updated = new TextIndex(result);
        foreach (var bookmark in result.Bookmarks)
        {
            var point = bookmark.Start == bookmark.End;
            bookmark.Start = updated.Snap(bookmark.Start, point);
            bookmark.End = point ? bookmark.Start : updated.Snap(bookmark.End, true);
        }
        result.Comments.Clear(); result.Changes.Clear(); result.Id = Guid.NewGuid().ToString("N"); result.Modified = DateTimeOffset.UtcNow; DocumentJson.Validate(result); return result;
    }
}
