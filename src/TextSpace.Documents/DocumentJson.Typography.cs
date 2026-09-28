using TextSpace.Core;

namespace TextSpace.Documents;

public static partial class DocumentJson
{
    private static void ValidateTypography(ParagraphFormat format)
    {
        if (!double.IsFinite(format.FirstLineIndent) || Math.Abs(format.FirstLineIndent) > 4000)
            throw new InvalidDataException("Invalid first-line indentation.");
        TabStopRules.Validate(format.TabStops);
    }
}
