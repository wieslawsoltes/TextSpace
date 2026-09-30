using TextSpace.Core;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class ObjectNavigationTests
{
    private static (DocumentLayout Layout, ShapeBlock Front, ShapeBlock Inline) Overlap()
    {
        var front = new ShapeBlock { Placement = new() { Floating = true } }; var inline = new ShapeBlock();
        var page = new LayoutPage(0, new()); page.Objects.Add(new(front, new(40, 50, 120, 80))); page.Objects.Add(new(inline, new(40, 50, 120, 80)));
        return (new(new(), [page]), front, inline);
    }
    [Fact]
    public void FloatingObjectWinsEvenWhenInlineObjectComesLaterInSourceOrder()
    {
        var (layout, front, _) = Overlap(); var index = new VisualObjectIndex(layout);
        Assert.True(index.TryHitTest(0, 80, 80, out var result)); Assert.Same(front, result.Item.Object);
    }
    [Fact]
    public void LastObjectInTheSameLayerWins()
    {
        var (layout, _, inline) = Overlap(); inline.Placement = new() { Floating = true };
        var index = new VisualObjectIndex(layout); Assert.True(index.TryHitTest(0, 80, 80, out var result)); Assert.Same(inline, result.Item.Object);
    }
    [Fact]
    public void ReplicasNeverHideASelectableObjectOrDuplicateNavigation()
    {
        var (layout, front, inline) = Overlap();
        layout.Pages[0].Objects.Add(new(new ShapeBlock { Placement = new() { Floating = true } }, new(40, 50, 120, 80)) { IsReplica = true });
        var index = new VisualObjectIndex(layout); Assert.Equal(2, index.Locations.Count);
        Assert.True(index.TryHitTest(0, 80, 80, out var result)); Assert.Same(front, result.Item.Object);
        Assert.Equal(inline.Id, index.Next(front.Id)!.Value.Item.Object.Id);
    }
    [Fact]
    public void NavigationWrapsAndCanReachCoveredObjectsWithoutEditingThem()
    {
        var (layout, front, inline) = Overlap(); var index = new VisualObjectIndex(layout);
        Assert.Same(front, index.Next(null)!.Value.Item.Object); Assert.Same(inline, index.Next(null, true)!.Value.Item.Object);
        Assert.Same(inline, index.Next(front.Id)!.Value.Item.Object); Assert.Same(front, index.Next(inline.Id)!.Value.Item.Object);
        Assert.Same(inline, index.Next(front.Id, true)!.Value.Item.Object); Assert.Same(front, index.Next("missing")!.Value.Item.Object);
    }
    [Fact]
    public void RotatedObjectUsesItsLocalFrameRatherThanUnrotatedRectangle()
    {
        var shape = new ShapeBlock { Placement = new() { Rotation = 90 } }; var page = new LayoutPage(0, new());
        page.Objects.Add(new(shape, new(100, 100, 100, 20))); var index = new VisualObjectIndex(new(new(), [page]));
        Assert.True(index.TryHitTest(0, 150, 65, out _)); Assert.False(index.TryHitTest(0, 105, 110, out _));
    }
    [Fact]
    public void LegacyImagePlacementsWorkWithoutDuplicatingModernObjectLists()
    {
        var image = new ImageBlock(); var page = new LayoutPage(0, new()); page.Images.Add(new(image, new(10, 20, 80, 60)));
        var index = new VisualObjectIndex(new(new(), [page])); Assert.True(index.TryLocate(image.Id, out var result)); Assert.Same(image, result.Item.Object);
        page.Objects.Add(new(image, new(10, 20, 80, 60))); Assert.Single(new VisualObjectIndex(new(new(), [page])).Locations);
    }
    [Fact]
    public void PageLookupAndSplitObjectIdentityAreStable()
    {
        var shape = new ShapeBlock(); var a = new LayoutPage(0, new()); var b = new LayoutPage(1, new());
        a.Objects.Add(new(shape, new(20, 20, 80, 60))); b.Objects.Add(new(shape, new(20, 30, 80, 60)));
        var index = new VisualObjectIndex(new(new(), [a, b])); Assert.Single(index.Locations);
        Assert.True(index.TryLocate(shape.Id, out var location)); Assert.Equal(0, location.PageIndex);
        Assert.True(index.TryHitTest(1, 50, 50, out location)); Assert.Equal(1, location.PageIndex);
    }
    [Fact]
    public void EmptyAndNonfiniteQueriesAreRejectedWithoutThrowing()
    {
        var index = new VisualObjectIndex(new(new(), [new LayoutPage(0, new())]));
        Assert.Null(index.Next(null)); Assert.False(index.TryLocate(null, out _));
        Assert.False(index.TryHitTest(-1, 0, 0, out _)); Assert.False(index.TryHitTest(1, 0, 0, out _));
        Assert.False(index.TryHitTest(0, double.NaN, 0, out _));
    }
    [Fact]
    public void PublishedNavigationListCannotBeMutated()
    {
        var (layout, _, _) = Overlap(); var list = Assert.IsAssignableFrom<IList<VisualObjectLocation>>(new VisualObjectIndex(layout).Locations);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }
}
