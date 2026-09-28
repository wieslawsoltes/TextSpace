using System.Text.Json;
using System.Text.Json.Serialization;
using TextSpace.Core;

namespace TextSpace.Documents;

public static partial class DocumentJson
{
    public const int MaxCharacters = 5_000_000;
    public const int MaxFileBytes = 32 * 1024 * 1024;
    public static string Save(DocumentModel document) => JsonSerializer.Serialize(document, DocumentJsonContext.Default.DocumentModel);
    public static DocumentModel Clone(DocumentModel document) => Load(Save(document));
    public static DocumentModel Load(string json)
    {
        if (json.Length > MaxFileBytes) throw new InvalidDataException("The document exceeds the 32 MB import limit.");
        var document = JsonSerializer.Deserialize(json, DocumentJsonContext.Default.DocumentModel) ?? throw new InvalidDataException("The document is empty.");
        Validate(document); return document;
    }
    public static void Validate(DocumentModel document)
    {
        if (document.FormatVersion != 1) throw new InvalidDataException("Unsupported TextSpace document version.");
        if (document.Blocks is null || document.Comments is null || document.Changes is null || document.Bookmarks is null || document.Page is null) throw new InvalidDataException("Missing document structure.");
        ValidateSections(document);
        if (!double.IsFinite(document.DefaultTabStop) || document.DefaultTabStop is < 1 or > 720)
            throw new InvalidDataException("The default tab interval must be between 1 and 720 points.");
        var page = document.Page;
        if (!double.IsFinite(page.Width + page.Height + page.MarginTop + page.MarginBottom + page.MarginLeft + page.MarginRight + page.ColumnGap) || page.Width is < 144 or > 4000 || page.Height is < 144 or > 4000 || page.MarginLeft < 0 || page.MarginRight < 0 || page.MarginTop < 0 || page.MarginBottom < 0 || page.ContentHeight < 36 || page.ContentWidth < 36 || page.Columns is < 1 or > 3 || page.ColumnWidth < 24) throw new InvalidDataException("Invalid page geometry.");
        var count = 0; var chars = 0; long images = 0; var ids = new HashSet<string>();
        void Walk(List<Block> blocks, int depth)
        {
            if (depth > 8) throw new InvalidDataException("Tables are nested too deeply.");
            foreach (var block in blocks)
            {
                if (++count > 50_000) throw new InvalidDataException("Too many document blocks.");
                if (string.IsNullOrWhiteSpace(block.Id) || !ids.Add(block.Id)) block.Id = Guid.NewGuid().ToString("N");
                switch (block)
                {
                    case Paragraph p:
                        if (p.Runs is null || p.Format is null || p.DefaultStyle is null) throw new InvalidDataException("Invalid paragraph.");
                        if (!double.IsFinite(p.Format.LineSpacing + p.Format.LeftIndent + p.Format.RightIndent + p.Format.SpaceBefore + p.Format.SpaceAfter) || p.Format.LineSpacing is < 0.5 or > 10 || Math.Abs(p.Format.LeftIndent) > 2000 || Math.Abs(p.Format.RightIndent) > 2000) throw new InvalidDataException("Invalid paragraph geometry.");
                        ValidateTypography(p.Format);
                        foreach (var run in p.Runs)
                        {
                            if (run.Text is null || run.Style is null || !double.IsFinite(run.Style.FontSize) || run.Style.FontSize is < 1 or > 400) throw new InvalidDataException("Invalid text formatting.");
                            if (run.Text.Contains('\n') || run.Text.Contains('\r')) throw new InvalidDataException("Paragraph runs cannot contain paragraph separators.");
                            chars += run.Text.Length;
                        }
                        if (chars > MaxCharacters) throw new InvalidDataException("The document exceeds five million characters.");
                        p.Normalize(); break;
                    case TableBlock table:
                        if (table.Rows is null || table.Rows.Count is < 1 or > 200 || !double.IsFinite(table.CellPadding) || table.CellPadding is < 0 or > 72)
                            throw new InvalidDataException("Invalid table.");
                        if (table.Rows.Any(row => row?.Cells is null || row.Cells.Count is < 1 or > 20
                            || !double.IsFinite(row.MinimumHeight) || row.MinimumHeight is < 0 or > 4000))
                            throw new InvalidDataException("Invalid table row.");
                        // Legacy native documents could have ragged rows. Add explicit logical slots.
                        var columns = table.Rows.Max(row => row.Cells.Count);
                        foreach (var row in table.Rows) while (row.Cells.Count < columns) row.Cells.Add(new());
                        var grid = new TableGrid(table);
                        foreach (var region in grid.Regions)
                        {
                            if (region.Cell.Blocks.Count == 0) region.Cell.Blocks.Add(new Paragraph());
                            Walk(region.Cell.Blocks, depth + 1);
                        }
                        break;
                    case SectionBreakBlock section:
                        if (depth != 0) throw new InvalidDataException("Section breaks cannot occur inside a table.");
                        break;
                    case ColumnBreakBlock:
                        if (depth != 0) throw new InvalidDataException("Column breaks cannot occur inside a table.");
                        break;
                    case ImageBlock image:
                        images += image.Data?.Length ?? 0;
                        if (images > MaxFileBytes || !double.IsFinite(image.Width + image.Height) || image.Width is <= 0 or > 4000 || image.Height is <= 0 or > 4000) throw new InvalidDataException("Invalid picture.");
                        break;
                }
            }
        }
        Walk(document.Blocks, 0);
        if (!document.Paragraphs().Any()) document.Blocks.Add(new Paragraph());
        ValidateFields(document);
        if (document.Bookmarks.Count == 0) return;
        var index = new TextIndex(document);
        if (document.Bookmarks.Count > 10_000) throw new InvalidDataException("Too many bookmarks.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bookmarkIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bookmark in document.Bookmarks)
        {
            if (bookmark is null || !Bookmark.IsValidName(bookmark.Name) || !names.Add(bookmark.Name)
                || string.IsNullOrWhiteSpace(bookmark.Id) || !bookmarkIds.Add(bookmark.Id))
                throw new InvalidDataException("Bookmarks require unique names and identifiers.");
            if (bookmark.Start < 0 || bookmark.End < bookmark.Start || bookmark.End > index.Length
                || index.Snap(bookmark.Start) != bookmark.Start || index.Snap(bookmark.End) != bookmark.End)
                throw new InvalidDataException("A bookmark lies outside the document or splits a Unicode grapheme.");
        }
    }
    public static DocumentModel FromText(string text, string title = "Document1")
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (text.Length > MaxCharacters) throw new InvalidDataException("The text exceeds the import limit.");
        return new() { Title = title, Blocks = text.Split('\n').Select(line => (Block)new Paragraph(line)).ToList() };
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(DocumentModel))]
internal partial class DocumentJsonContext : JsonSerializerContext;
