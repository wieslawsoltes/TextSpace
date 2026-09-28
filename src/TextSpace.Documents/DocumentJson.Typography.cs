using TextSpace.Core;

namespace TextSpace.Documents;

public static partial class DocumentJson
{
    private static void ValidateTypography(ParagraphFormat format)
    {
        if (!double.IsFinite(format.FirstLineIndent) || Math.Abs(format.FirstLineIndent) > 4000)
            throw new InvalidDataException("Invalid first-line indentation.");
        if (format.TabStops.IsDefault || format.TabStops.Length > 128) throw new InvalidDataException("A paragraph supports at most 128 tab stops.");
        var positions = new HashSet<(long, bool)>();
        foreach (var stop in format.TabStops)
        {
            if (stop is null || !double.IsFinite(stop.Position) || stop.Position is < -4000 or > 4000
                || !Enum.IsDefined(stop.Alignment) || !Enum.IsDefined(stop.Leader)
                || stop.DecimalCharacter is not ('.' or ',')
                || !positions.Add(((long)Math.Round(stop.Position * 20), stop.RelativeToRightEdge)))
                throw new InvalidDataException("Invalid, duplicate, or unsupported tab stop.");
        }
    }
}
