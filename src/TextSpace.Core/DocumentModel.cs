using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TextSpace.Core;

public sealed class DocumentModel
{
    public int FormatVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Document1";
    public string Author { get; set; } = "You";
    public string Subject { get; set; } = "";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    public PageSettings Page { get; set; } = new();
    public string Header { get; set; } = "";
    public string Footer { get; set; } = "";
    public List<Block> Blocks { get; set; } = [new Paragraph()];
    public List<CommentThread> Comments { get; set; } = [];
    public List<TrackedEdit> Changes { get; set; } = [];
    public SectionOptions SectionOptions { get; set; } = new();
    public List<DocumentField> Fields { get; set; } = [];
    public List<Bookmark> Bookmarks { get; set; } = [];
    [JsonIgnore] public string PlainText => new TextIndex(this).Text;
    [JsonIgnore] public int WordCount => Regex.Matches(PlainText, @"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*").Count;
    public IEnumerable<Paragraph> Paragraphs() => Walk(Blocks);
    public static IEnumerable<Paragraph> Walk(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph p) yield return p;
            else if (block is TableBlock table)
                foreach (var p2 in table.Rows.SelectMany(r => r.Cells).SelectMany(c => Walk(c.Blocks))) yield return p2;
        }
    }
}
