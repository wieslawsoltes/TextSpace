using TextSpace.Core;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class ObjectIndexCacheTests
{
    private static DocumentLayout Layout()
    {
        var page = new LayoutPage(0, new()); page.Objects.Add(new(new ShapeBlock(), new(10, 10, 100, 60)));
        return new(new(), [page]);
    }
    [Fact]
    public void NullSelectionNeverBuildsNavigationGeometry()
    {
        var cache = new VisualObjectIndexCache(); var layout = Layout();
        for (var i = 0; i < 1000; i++) Assert.False(cache.TryLocate(layout, null, out _));
        Assert.False(cache.IsPopulated);
    }
    [Fact]
    public void ExplicitNavigationBuildsOnceAndReusesTheIndex()
    {
        var cache = new VisualObjectIndexCache(); var layout = Layout(); var first = cache.Get(layout);
        Assert.True(cache.IsPopulated); Assert.Same(first, cache.Get(layout));
        Assert.True(cache.TryLocate(layout, first.Locations[0].Item.Object.Id, out var location));
        Assert.Same(first.Locations[0].Item, location.Item);
        Assert.False(cache.TryLocate(layout, null, out _)); Assert.Same(first, cache.Get(layout));
    }
    [Fact]
    public void ChangedLayoutDropsOldIndexWithoutEagerlyBuildingAReplacement()
    {
        var cache = new VisualObjectIndexCache(); var firstLayout = Layout(); var first = cache.Get(firstLayout);
        var replacement = Layout(); Assert.False(cache.TryLocate(replacement, null, out _));
        Assert.False(cache.IsPopulated); var next = cache.Get(replacement); Assert.NotSame(first, next);
        Assert.False(next.TryLocate(first.Locations[0].Item.Object.Id, out _));
    }
    [Fact]
    public void ClearReleasesTheIndexAndSubsequentQueriesRebuild()
    {
        var cache = new VisualObjectIndexCache(); var layout = Layout(); var first = cache.Get(layout);
        cache.Clear(); Assert.False(cache.IsPopulated); Assert.NotSame(first, cache.Get(layout));
    }
    [Fact]
    public void NullSelectionWarmPathHasNoManagedAllocations()
    {
        var cache = new VisualObjectIndexCache(); var layout = Layout();
        for (var i = 0; i < 10000; i++) cache.TryLocate(layout, null, out _);
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var found = false;
        for (var i = 0; i < 10000; i++) found |= cache.TryLocate(layout, null, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Assert.False(found); Assert.False(cache.IsPopulated); Assert.Equal(0, allocated);
    }
}
