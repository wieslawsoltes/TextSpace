using TextSpace.Core;

namespace TextSpace.Documents;

public static class SampleDocument
{
    private static Paragraph Styled(string text, string name)
    {
        var style = DocumentStyles.Find(name); return new(text, style.Character, style.Paragraph);
    }
    public static DocumentModel Create()
    {
        var intro = new Paragraph("Every great idea starts with a blank page. TextSpace gives your words room to become something extraordinary—with familiar tools, thoughtful typography, and a workspace that stays out of your way.");
        intro.Runs = [new("Every great idea starts with a blank page. ", new() { Bold = true }), new("TextSpace gives your words room to become something extraordinary—with familiar tools, thoughtful typography, and a workspace that stays out of your way.")];
        var table = TableBlock.Create(4, 3);
        var values = new[] { "Milestone", "Owner", "Status", "Shape the story", "Design team", "Complete", "Bring ideas together", "Everyone", "In progress", "Share the final draft", "Editorial", "Up next" };
        var n = 0; foreach (var cell in table.Rows.SelectMany(r => r.Cells)) cell.Blocks = [new Paragraph(values[n++], format: new() { SpaceAfter = 0, LineSpacing = 1.1 })];
        var document = new DocumentModel
        {
            Title = "A place for your ideas",
            Subject = "An editable introduction to TextSpace",
            Header = "TEXTSPACE  /  THE WRITING ROOM",
            Footer = "{PAGE}",
            Blocks =
            [
                Styled("A place for your ideas", "Title"),
                Styled("Write clearly. Create beautifully. Make it yours.", "Subtitle"),
                intro,
                Styled("Make yourself at home", "Heading 1"),
                new Paragraph("Click anywhere on this page and start writing. Select a few words to change their font, add emphasis, or choose a color. Use the ribbon above to explore the tools you already know."),
                new Paragraph("Give your document a clear structure with headings and styles.", format: new() { List = ListKind.Bullet, SpaceAfter = 5 }),
                new Paragraph("Bring information into focus with tables and pictures.", format: new() { List = ListKind.Bullet, SpaceAfter = 5 }),
                new Paragraph("Keep the conversation going with comments and tracked edits.", format: new() { List = ListKind.Bullet, SpaceAfter = 10 }),
                Styled("Good writing is clear thinking made visible.", "Quote"),
                Styled("A little structure goes a long way", "Heading 1"),
                new Paragraph("Turn a plan into a shared understanding. This table is editable: click a cell to change its text, or use the Table Layout tab to add a row."),
                table,
                new Paragraph("Your documents stay on this device. Save a .textspace file for a complete local copy, or export to .docx to continue your work in another editor.", new() { FontSize = 10, Color = "#595959" }, new() { SpaceBefore = 12, SpaceAfter = 0 }),
            ]
        };
        var index = new TextIndex(document); var start = index.Text.IndexOf("Every great idea", StringComparison.Ordinal);
        document.Comments.Add(new() { Start = start, End = start + 45, Author = "TextSpace", Text = "Welcome to your writing room. Try editing this sentence, then add a comment of your own." });
        return document;
    }
    public static DocumentModel Letter()
    {
        var document = DocumentJson.FromText("Your name\nYour address\nCity, postal code\n\n" + DateTime.Today.ToString("D") + "\n\nRecipient name\nOrganization\nAddress\n\nDear recipient,\n\nStart your letter here.\n\nKind regards,\nYour name", "Letter");
        return document;
    }
    public static DocumentModel Report()
    {
        return new() { Title = "Project report", Footer = "Page {PAGE} of {NUMPAGES}", Blocks = [Styled("Project report", "Title"), Styled("A clear view of what comes next", "Subtitle"), Styled("Executive summary", "Heading 1"), new Paragraph("Summarize the purpose, findings, and decisions in a few focused paragraphs."), Styled("Key findings", "Heading 1"), new Paragraph("Describe your most important finding.", format: new() { List = ListKind.Number }), Styled("Next steps", "Heading 1"), new Paragraph("Set out the actions, owners, and deadlines.")] };
    }
}
