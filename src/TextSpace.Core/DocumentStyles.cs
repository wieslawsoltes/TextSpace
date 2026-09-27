namespace TextSpace.Core;

public sealed record DocumentStyle(string Name, TextStyle Character, ParagraphFormat Paragraph);

public static class DocumentStyles
{
    public static IReadOnlyList<DocumentStyle> BuiltIn { get; } =
    [
        new("Normal", new(), new()),
        new("No Spacing", new(), new() { SpaceAfter = 0, LineSpacing = 1 }),
        new("Heading 1", new() { FontSize = 20, Color = "#0F4761" }, new() { StyleName = "Heading 1", OutlineLevel = 1, SpaceBefore = 18, SpaceAfter = 8, KeepWithNext = true }),
        new("Heading 2", new() { FontSize = 16, Color = "#0F4761" }, new() { StyleName = "Heading 2", OutlineLevel = 2, SpaceBefore = 14, SpaceAfter = 6, KeepWithNext = true }),
        new("Heading 3", new() { FontSize = 13, Bold = true, Color = "#0F4761" }, new() { StyleName = "Heading 3", OutlineLevel = 3, SpaceBefore = 10, SpaceAfter = 4, KeepWithNext = true }),
        new("Title", new() { FontSize = 32, Color = "#17365D" }, new() { StyleName = "Title", SpaceAfter = 6, KeepWithNext = true }),
        new("Subtitle", new() { FontSize = 15, Color = "#595959" }, new() { StyleName = "Subtitle", SpaceAfter = 18 }),
        new("Quote", new() { Italic = true, Color = "#595959" }, new() { StyleName = "Quote", LeftIndent = 24, RightIndent = 24, SpaceBefore = 12, SpaceAfter = 12 }),
        new("Caption", new() { FontSize = 9, Italic = true, Color = "#595959" }, new() { StyleName = "Caption", SpaceAfter = 6 }),
    ];
    public static DocumentStyle Find(string name) => BuiltIn.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? BuiltIn[0];
}
