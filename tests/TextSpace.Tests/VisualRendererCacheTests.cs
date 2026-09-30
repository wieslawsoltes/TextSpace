using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Skia;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualRendererCacheTests
{
    [Fact]
    public void UnrelatedBodyEditAndRepaginationRetainVisualTypography()
    {
        using var renderer = new DocumentRenderer();
        var equation = new EquationBlock { Root = EquationTemplates.Create("fraction") };
        var shape = new ShapeBlock { Text = "Unchanged text box" };
        var paragraph = new Paragraph("Before");
        var document = new DocumentModel { Blocks = [paragraph, shape, equation] };
        renderer.Layout(document);
        var math = renderer.MeasureEquation(equation);
        var text = renderer.VisualLayouts.GetShape(shape);
        var misses = renderer.VisualLayouts.Misses;

        paragraph.Runs = [new("After: different body text and paragraph geometry", paragraph.DefaultStyle)];
        renderer.Layout(document);

        Assert.Same(math, renderer.MeasureEquation(equation));
        Assert.Same(text, renderer.VisualLayouts.GetShape(shape));
        Assert.Equal(misses, renderer.VisualLayouts.Misses);
    }

    [Fact]
    public void CommittedTransformDoesNotEvictEquationOrShapeMeasurements()
    {
        using var renderer = new DocumentRenderer();
        var equation = new EquationBlock { Root = EquationTemplates.Create("quadratic") };
        var shape = new ShapeBlock { Text = "Position is not typography" };
        var document = new DocumentModel { Blocks = [equation, shape, new Paragraph()] };
        renderer.Layout(document);
        var math = renderer.MeasureEquation(equation);
        var text = renderer.VisualLayouts.GetShape(shape);

        equation.Placement = equation.Placement with { Rotation = 45, X = 20, Y = 12 };
        shape.Placement = shape.Placement with { Rotation = 30, X = 18, Y = 10 };
        renderer.Layout(document);

        Assert.Same(math, renderer.MeasureEquation(equation));
        Assert.Same(text, renderer.VisualLayouts.GetShape(shape));
    }

    [Fact]
    public void EquivalentNativeSnapshotReusesMeasurementsWithStableSlotIds()
    {
        using var renderer = new DocumentRenderer();
        var equation = new EquationBlock { Root = EquationTemplates.Create("matrix") };
        var document = new DocumentModel { Blocks = [equation, new Paragraph()] };
        renderer.Layout(document);
        var before = renderer.MeasureEquation(equation);
        var snapshot = DocumentJson.Load(DocumentJson.Save(document));
        Assert.NotSame(document, snapshot);
        Assert.Equal(document.Id, snapshot.Id);
        renderer.Layout(snapshot);
        Assert.Same(before, renderer.MeasureEquation(Assert.Single(snapshot.Blocks.OfType<EquationBlock>())));
    }

    [Fact]
    public void ShapeWrappingChangeInvalidatesOnlyThatShape()
    {
        using var renderer = new DocumentRenderer();
        var equation = new EquationBlock();
        var shape = new ShapeBlock { Text = "Text whose wrapping width changes" };
        var document = new DocumentModel { Blocks = [equation, shape, new Paragraph()] };
        renderer.Layout(document);
        var math = renderer.MeasureEquation(equation);
        var before = renderer.VisualLayouts.GetShape(shape);
        shape.Width = 80;
        renderer.Layout(document);
        Assert.NotSame(before, renderer.VisualLayouts.GetShape(shape));
        Assert.Same(math, renderer.MeasureEquation(equation));
    }

    [Fact]
    public void NewDocumentIdentityDropsPreviousVisualPayload()
    {
        using var renderer = new DocumentRenderer();
        var equation = new EquationBlock();
        renderer.Layout(new DocumentModel { Blocks = [equation, new Paragraph()] });
        var before = renderer.MeasureEquation(equation);
        // Reuse the object intentionally to prove that document ownership, not
        // an incidental content-key miss, clears the previous cache.
        renderer.Layout(new DocumentModel { Blocks = [equation, new Paragraph()] });
        Assert.Equal(0, renderer.VisualLayouts.Count);
        Assert.NotSame(before, renderer.MeasureEquation(equation));
    }

    [Fact]
    public void DisposingRendererReleasesVisualPayload()
    {
        var renderer = new DocumentRenderer();
        var cache = renderer.VisualLayouts;
        cache.GetEquation(new EquationBlock());
        cache.GetShape(new ShapeBlock { Text = "Release retained text" });
        Assert.Equal(2, cache.Count);
        renderer.Dispose();
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.EstimatedBytes);
    }
}
