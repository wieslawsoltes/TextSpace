using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    /// <summary>Sets paragraph tab stops and optionally the document-wide default interval as one atomic edit.</summary>
    public void SetTabStops(IEnumerable<TabStop> stops, double? defaultInterval = null)
    {
        ArgumentNullException.ThrowIfNull(stops); EnsureWritable();
        var immutable = System.Collections.Immutable.ImmutableArray.CreateRange(stops);
        Execute("Tab stops", () =>
        {
            FormatParagraph("Tab stops", p => p with { TabStops = immutable });
            if (defaultInterval is { } value) Document.DefaultTabStop = value;
        });
    }
    public void ClearTabStops() => SetTabStops([]);
}
