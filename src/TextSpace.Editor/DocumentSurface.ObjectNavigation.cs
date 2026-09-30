using TextSpace.Layout;

namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    private DocumentLayout? _objectIndexLayout;
    private VisualObjectIndex? _objectIndex;
    private VisualObjectIndex ObjectIndex
    {
        get
        {
            if (!ReferenceEquals(_objectIndexLayout, Layout))
            {
                _objectIndex = new(Layout); _objectIndexLayout = Layout;
            }
            return _objectIndex!;
        }
    }
    /// <summary>Canonical, first-placement objects; repeated table headers are excluded.</summary>
    public IReadOnlyList<VisualObjectLocation> ObjectLocations => ObjectIndex.Locations;

    /// <summary>Cycles selection without editing text, geometry or document history.</summary>
    public void SelectNextObject(bool backwards = false)
    {
        if (IsObjectEditorOpen || IsVisualGestureActive) return;
        if (ObjectIndex.Next(_selectedObjectId, backwards) is { } next) SelectObject(next.Item.Object.Id, reveal: true);
        FocusEditor();
    }
    private (int Page, LayoutObject Item)? SelectedPlacement() => ObjectIndex.TryLocate(_selectedObjectId, out var location)
        ? (location.PageIndex, location.Item) : null;
}
