using TextSpace.Core;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualCropGeometryTests
{
    [Theory]
    [InlineData(false, false)][InlineData(true, false)][InlineData(false, true)][InlineData(true, true)]
    public void VisibleCornerMapsToCorrectSourceEdges(bool flipX, bool flipY)
    {
        var source = new ImageCrop(); var result = VisualCropGeometry.Drag(source, VisualHandle.NorthWest, 20, 10, 200, 100,
            new() { FlipHorizontal = flipX, FlipVertical = flipY });
        Assert.Equal(flipX ? 0 : 0.1, result.Left, 10); Assert.Equal(flipX ? 0.1 : 0, result.Right, 10);
        Assert.Equal(flipY ? 0 : 0.1, result.Top, 10); Assert.Equal(flipY ? 0.1 : 0, result.Bottom, 10); Assert.Equal(new ImageCrop(), source);
    }
    [Theory]
    [InlineData(0)][InlineData(90)][InlineData(180)][InlineData(270)][InlineData(37)]
    public void RotationIsUndoneBeforeSourceMapping(double angle)
    {
        var delta = VisualGeometry.Rotate(25, 0, angle);
        var crop = VisualCropGeometry.Drag(new(), VisualHandle.West, delta.X, delta.Y, 100, 100, new() { Rotation = angle, FlipHorizontal = true });
        Assert.Equal(0.25, crop.Right, 10); Assert.Equal(0, crop.Left);
    }
    [Theory]
    [InlineData(VisualHandle.East, false)][InlineData(VisualHandle.West, true)]
    public void OppositeHorizontalHandlesExpandAndContractWithoutRewritingPixels(VisualHandle handle, bool flip)
    {
        var original = new ImageCrop { Left = 0.1, Right = 0.2, Top = 0.1 };
        var dx = handle == VisualHandle.East ? -10 : 10;
        var result = VisualCropGeometry.Drag(original, handle, dx, 0, 100, 100, new() { FlipHorizontal = flip });
        Assert.Equal(0.27, result.Right, 10); Assert.Equal(original.Left, result.Left); Assert.Equal(original.Top, result.Top);
    }
    [Fact]
    public void NearLimitImportedCropDoesNotPassANegativeClampMaximum()
    {
        var result = VisualCropGeometry.Drag(new() { Right = 0.985 }, VisualHandle.West, 10, 0, 100, 100, new());
        Assert.Equal(0, result.Left); Assert.Equal(0.985, result.Right); Assert.True(result.Left + result.Right < 0.99);
    }
    [Theory]
    [InlineData(VisualHandle.None)][InlineData(VisualHandle.Move)][InlineData(VisualHandle.Rotate)]
    public void NoncropHandlesLeaveTheSourceUnchanged(VisualHandle handle)
    {
        var source = new ImageCrop { Left = 0.2 }; Assert.Same(source, VisualCropGeometry.Drag(source, handle, 10, 10, 100, 100, new()));
    }
    [Fact]
    public void InvalidGeometryIsRejectedBeforeCalculation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VisualCropGeometry.Drag(new(), VisualHandle.West, double.NaN, 0, 100, 100, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => VisualCropGeometry.Drag(new(), VisualHandle.West, 1, 0, 0, 100, new()));
        Assert.Throws<ArgumentException>(() => VisualCropGeometry.Drag(new() { Left = 0.9, Right = 0.1 }, VisualHandle.West, 1, 0, 100, 100, new()));
    }
    [Fact]
    public void RandomDragsRetainAValidVisibleSourceRegion()
    {
        var random = new Random(601);
        for (var i = 0; i < 1000; i++)
        {
            var crop = new ImageCrop { Left = random.NextDouble() * 0.3, Right = random.NextDouble() * 0.3, Top = random.NextDouble() * 0.3, Bottom = random.NextDouble() * 0.3 };
            var result = VisualCropGeometry.Drag(crop, VisualHandle.SouthEast, random.NextDouble() * 2000 - 1000, random.NextDouble() * 2000 - 1000, 200, 100,
                new() { Rotation = random.NextDouble() * 360, FlipHorizontal = (i & 1) != 0, FlipVertical = (i & 2) != 0 });
            Assert.True(result.Left >= 0 && result.Right >= 0 && result.Top >= 0 && result.Bottom >= 0);
            Assert.True(result.Left + result.Right < 0.99 && result.Top + result.Bottom < 0.99);
        }
    }
}
