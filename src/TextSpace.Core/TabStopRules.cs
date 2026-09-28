using System.Collections.Immutable;

namespace TextSpace.Core;

/// <summary>Shared native tab-stop invariants, using twip-rounded position/edge identity.</summary>
public static class TabStopRules
{
    public const int MaximumCount = 128;
    public readonly record struct Key(long Twips, bool RelativeToRightEdge);

    public static bool IsValid(TabStop? stop) => stop is not null
        && double.IsFinite(stop.Position) && stop.Position is >= -4000 and <= 4000
        && Enum.IsDefined(stop.Alignment) && Enum.IsDefined(stop.Leader)
        && stop.DecimalCharacter is '.' or ',';

    public static Key GetKey(TabStop stop) => new(
        checked((long)Math.Round(stop.Position * 20)), stop.RelativeToRightEdge);

    /// <summary>Empty and small tab collections do not allocate validation state.</summary>
    public static void Validate(ImmutableArray<TabStop> stops)
    {
        if (stops.IsDefault)
            throw new InvalidDataException("The paragraph's tab-stop collection is uninitialized. Use an empty array for no custom stops.");
        if (stops.Length > MaximumCount)
            throw new InvalidDataException($"The paragraph contains {stops.Length} tab stops; the supported maximum is {MaximumCount}. Open and Repair can create a separate copy; the original must be preserved.");
        if (stops.IsEmpty) return;
        Span<Key> small = stackalloc Key[8];
        HashSet<Key>? large = stops.Length > small.Length ? new(stops.Length) : null;
        var count = 0;
        foreach (var stop in stops)
        {
            if (!IsValid(stop)) throw new InvalidDataException("Invalid or unsupported tab stop.");
            var key = GetKey(stop);
            if (large is not null)
            {
                if (!large.Add(key)) throw new InvalidDataException("Duplicate tab-stop position and edge at twip precision.");
            }
            else
            {
                for (var i = 0; i < count; i++)
                    if (small[i] == key) throw new InvalidDataException("Duplicate tab-stop position and edge at twip precision.");
                small[count++] = key;
            }
        }
    }
}
