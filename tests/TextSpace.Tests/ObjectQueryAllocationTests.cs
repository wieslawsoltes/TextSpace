using TextSpace.Core;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class ObjectQueryAllocationTests
{
    [Fact]
    public void IndexedQueriesMatchStablePainterOrderForRotatedObjects()
    {
        var random = new Random(913); var page = new LayoutPage(0, new());
        for (var i = 0; i < 80; i++) page.Objects.Add(new(new ShapeBlock { Placement = new() { Floating = i % 3 == 0, Rotation = random.NextDouble() * 360 } },
            new(random.NextDouble() * 400, random.NextDouble() * 600, 20 + random.NextDouble() * 160, 20 + random.NextDouble() * 120)) { IsReplica = i % 11 == 0 });
        var index = new VisualObjectIndex(new(new(), [page]));
        for (var i = 0; i < 1000; i++)
        {
            var x = random.NextDouble() * 612; var y = random.NextDouble() * 792;
            bool Contains(LayoutObject item)
            {
                var p = VisualGeometry.Local(item.Bounds, x, y, item.Object.Placement.Rotation);
                return p.X >= 0 && p.Y >= 0 && p.X <= item.Bounds.Width && p.Y <= item.Bounds.Height;
            }
            var expected = page.Objects.Where(o => !o.IsReplica && o.Object.Placement.Floating).Reverse().FirstOrDefault(Contains)
                ?? page.Objects.Where(o => !o.IsReplica && !o.Object.Placement.Floating).Reverse().FirstOrDefault(Contains);
            var found = index.TryHitTest(0, x, y, out var result);
            Assert.Equal(expected is not null, found); if (found) Assert.Same(expected, result.Item);
        }
    }
    [Fact]
    public void WarmHitLocationAndCyclingQueriesAllocateNoManagedPayload()
    {
        var a = new ShapeBlock(); var b = new ShapeBlock { Placement = new() { Floating = true } };
        var page = new LayoutPage(0, new()); page.Objects.Add(new(a, new(20, 20, 80, 60))); page.Objects.Add(new(b, new(20, 20, 80, 60)));
        var index = new VisualObjectIndex(new(new(), [page]));
        int Query()
        {
            var result = 0;
            for (var i = 0; i < 1000; i++)
            {
                if (index.TryHitTest(0, 40, 40, out var hit)) result += hit.Item.Object.Id.Length;
                if (index.TryLocate(a.Id, out var location)) result += location.Item.Object.Id.Length;
                result += index.Next(b.Id)!.Value.Item.Object.Id.Length;
            }
            return result;
        }
        for (var i = 0; i < 10; i++) Query();
        var before = GC.GetAllocatedBytesForCurrentThread(); var checksum = Query(); var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(checksum > 0); Assert.Equal(0, bytes);
    }
}
