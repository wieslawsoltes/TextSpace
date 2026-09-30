using TextSpace.Core;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualLayoutCacheTests
{
    private sealed class Metrics : IVersionedTextMetrics
    {
        private readonly MonospaceTextMetrics _inner = new();
        public long MetricsVersion { get; set; }
        public int Calls { get; private set; }
        public TextMeasurement Measure(string text, TextStyle style) { Calls++; return _inner.Measure(text, style); }
        public double[] CaretPositions(string text, TextStyle style) => _inner.CaretPositions(text, style);
    }
    [Fact]
    public void EquationTransformsReuseOwnedMeasurement()
    {
        var metrics = new Metrics(); var cache = new VisualLayoutCache(metrics);
        var block = new EquationBlock { Root = EquationTemplates.Create("quadratic") };
        var first = cache.GetEquation(block); var calls = metrics.Calls;
        block.Placement = block.Placement with { X = 20, Y = 30, Rotation = 45, FlipHorizontal = true };
        block.Width += 20; block.Height += 10; block.Color = "#123456";
        Assert.Same(first, cache.GetEquation(block)); Assert.Equal(calls, metrics.Calls); Assert.Equal(1, cache.Hits);
    }
    [Fact]
    public void InPlaceEquationTextAndSlotIdentityChangesInvalidate()
    {
        var cache = new VisualLayoutCache(new Metrics()); var block = new EquationBlock { Root = EquationNode.Leaf("x") };
        var first = cache.GetEquation(block); block.Root.Text = "changed"; var next = cache.GetEquation(block);
        Assert.NotSame(first, next); Assert.Equal("changed", next.Slots[0].Text);
        block.Root.Id = "new-slot"; var third = cache.GetEquation(block);
        Assert.Equal("new-slot", third.Slots[0].Id); Assert.Equal(3, cache.Misses);
    }
    [Fact]
    public void EquivalentDetachedDraftRetainsSlotGeometry()
    {
        var cache = new VisualLayoutCache(new Metrics()); var block = new EquationBlock { Root = EquationTemplates.Create("fraction") };
        var first = cache.GetEquation(block); block.Root = block.Root.Clone(); Assert.Same(first, cache.GetEquation(block));
    }
    [Fact]
    public void InvalidInPlaceMutationIsNotServedFromCache()
    {
        var cache = new VisualLayoutCache(new Metrics()); var block = new EquationBlock(); cache.GetEquation(block);
        block.Root.Children.Add(block.Root);
        Assert.Throws<InvalidDataException>(() => cache.GetEquation(block));
    }
    [Fact]
    public void ShapeTransformAndFillDoNotRemeasureText()
    {
        var metrics = new Metrics(); var cache = new VisualLayoutCache(metrics); var shape = new ShapeBlock { Text = "A text box" };
        var first = cache.GetShape(shape); var count = metrics.Calls;
        shape.Fill = "#FF0000"; shape.Placement = new() { Rotation = 90, X = 100 };
        Assert.Same(first, cache.GetShape(shape)); Assert.Equal(count, metrics.Calls);
    }
    [Fact]
    public void ShapeStoryAndBoundsDoRemeasure()
    {
        var cache = new VisualLayoutCache(new Metrics()); var shape = new ShapeBlock { Text = "First" };
        var first = cache.GetShape(shape); shape.Text = "Second"; var second = cache.GetShape(shape);
        Assert.NotSame(first, second); Assert.Equal("Second", second[0].Text);
        shape.Width -= 10; Assert.NotSame(second, cache.GetShape(shape));
        var before = cache.Misses; shape.TextStyle = shape.TextStyle with { Bold = true }; cache.GetShape(shape); Assert.Equal(before + 1, cache.Misses);
    }
    [Fact]
    public void FontRevisionInvalidatesBothKinds()
    {
        var metrics = new Metrics(); var cache = new VisualLayoutCache(metrics);
        var equation = new EquationBlock(); var shape = new ShapeBlock { Text = "Text" };
        var firstMath = cache.GetEquation(equation); var firstShape = cache.GetShape(shape);
        metrics.MetricsVersion++;
        Assert.NotSame(firstMath, cache.GetEquation(equation)); Assert.NotSame(firstShape, cache.GetShape(shape)); Assert.Equal(2, cache.Count);
    }
    [Fact]
    public void BoundedCacheEvictsLeastRecentlyUsedWithoutClearingEverything()
    {
        var cache = new VisualLayoutCache(new Metrics(), maximumEntries: 2);
        var a = new EquationBlock(); var b = new EquationBlock(); var c = new EquationBlock();
        var first = cache.GetEquation(a); cache.GetEquation(b); cache.GetEquation(a); cache.GetEquation(c);
        Assert.Same(first, cache.GetEquation(a)); Assert.Equal(2, cache.Count);
        cache.GetEquation(b); Assert.Equal(4, cache.Misses);
        cache.Invalidate(a.Id); Assert.InRange(cache.Count, 0, 2); cache.Clear(); Assert.Equal(0, cache.EstimatedBytes);
    }
    [Fact]
    public void OversizedEntriesAreNotRetained()
    {
        var cache = new VisualLayoutCache(new Metrics(), maximumBytes: 1); cache.GetEquation(new());
        Assert.Equal(0, cache.Count); Assert.Equal(0, cache.EstimatedBytes);
    }
    [Fact]
    public void CachedDisplayListsCannotBeMutatedByCallers()
    {
        var cache = new VisualLayoutCache(new Metrics()); var layout = cache.GetEquation(new());
        var list = Assert.IsAssignableFrom<IList<EquationSlot>>(layout.Slots);
        Assert.Throws<NotSupportedException>(() => list.Clear());
        var shapes = cache.GetShape(new() { Text = "Text" });
        Assert.Throws<NotSupportedException>(() => ((IList<VisualTextLine>)shapes).Clear());
    }
    [Fact]
    public void SlotQueriesMatchStableNearestSelection()
    {
        var layout = new EquationLayouter(new MonospaceTextMetrics()).Layout(EquationTemplates.Create("quadratic"), 18);
        var random = new Random(27);
        static double Distance(RectD r, double x, double y)
        {
            var dx = Math.Max(r.X - x, Math.Max(0, x - r.Right)); var dy = Math.Max(r.Y - y, Math.Max(0, y - r.Bottom)); return dx * dx + dy * dy;
        }
        for (var i = 0; i < 1000; i++)
        {
            var x = random.NextDouble() * layout.Width; var y = random.NextDouble() * layout.Height;
            Assert.Same(layout.Slots.OrderBy(s => Distance(s.Bounds, x, y)).First(), layout.HitTest(x, y));
        }
        foreach (var source in layout.Slots)
            foreach (var down in new[] { false, true })
            {
                var x = source.Bounds.X + source.Bounds.Width / 2; var y = source.Bounds.Y + source.Bounds.Height / 2;
                var expected = layout.Slots.Where(s => s.Id != source.Id && (down ? s.Bounds.Y + s.Bounds.Height / 2 > y + 1 : s.Bounds.Y + s.Bounds.Height / 2 < y - 1))
                    .OrderBy(s => Math.Abs(s.Bounds.Y + s.Bounds.Height / 2 - y) + Math.Abs(s.Bounds.X + s.Bounds.Width / 2 - x) * 2).FirstOrDefault();
                Assert.Same(expected, layout.VerticalNeighbor(source.Id, down));
            }
        Assert.Null(layout.HitTest(double.NaN, 1));
    }
}
