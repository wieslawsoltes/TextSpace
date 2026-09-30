using TextSpace.Core;
using TextSpace.OpenXml;
using Xunit;

namespace TextSpace.Tests;

public sealed class EquationOperationTests
{
    private static EquationNode Matrix() => new() { Kind = EquationKind.Matrix, Columns = 2, Children = [EquationNode.Leaf("a"), EquationNode.Leaf("b"), EquationNode.Leaf("c"), EquationNode.Leaf("d")] };
    [Fact]
    public void InsertRowPreservesContentAndSelectsNewCell()
    {
        var root = Matrix(); var result = EquationOperations.EditMatrix(root, root.Children[1].Id, EquationMatrixOperation.InsertRowBelow);
        Assert.Equal(4, root.Children.Count); Assert.Equal(6, result.Root.Children.Count); Assert.Equal(2, result.Root.Columns);
        Assert.Equal(new[] { "a", "b", "", "", "c", "d" }, result.Root.Children.Select(n => n.Text));
        Assert.Equal(result.Root.Children[3].Id, result.ActiveSlotId);
    }
    [Fact]
    public void InsertColumnPreservesRowMajorOrder()
    {
        var root = Matrix(); var result = EquationOperations.EditMatrix(root, root.Children[2].Id, EquationMatrixOperation.InsertColumnRight);
        Assert.Equal(3, result.Root.Columns); Assert.Equal(new[] { "a", "", "b", "c", "", "d" }, result.Root.Children.Select(n => n.Text));
        Assert.Equal(result.Root.Children[4].Id, result.ActiveSlotId);
    }
    [Theory]
    [InlineData(EquationMatrixOperation.DeleteRow, "a", "b")]
    [InlineData(EquationMatrixOperation.DeleteColumn, "a", "c")]
    public void DeletesSelectedDimensionWithValidSurvivingSlot(EquationMatrixOperation operation, string first, string last)
    {
        var root = Matrix(); var result = EquationOperations.EditMatrix(root, root.Children[3].Id, operation);
        Assert.Equal(new[] { first, last }, result.Root.Children.Select(n => n.Text));
        Assert.Contains(result.Root.DescendantsAndSelf(), n => n.Id == result.ActiveSlotId && n.Kind == EquationKind.Text); EquationRules.Validate(result.Root);
    }
    [Fact]
    public void NestedMatrixUsesNearestAncestor()
    {
        var nested = Matrix(); var outer = Matrix(); outer.Children[0] = EquationTemplates.Create("fraction", nested);
        var result = EquationOperations.EditMatrix(outer, nested.Children[0].Id, EquationMatrixOperation.InsertColumnRight);
        Assert.Equal(2, result.Root.Columns);
        Assert.Equal(3, result.Root.DescendantsAndSelf().First(n => n.Id == nested.Id).Columns);
        Assert.Equal(2, nested.Columns);
    }
    [Fact]
    public void MatrixDimensionBoundariesAreStrict()
    {
        var matrix = new EquationNode { Kind = EquationKind.Matrix, Columns = 1, Children = [EquationNode.Leaf()] };
        Assert.Throws<InvalidOperationException>(() => EquationOperations.EditMatrix(matrix, matrix.Children[0].Id, EquationMatrixOperation.DeleteRow));
        Assert.Throws<InvalidOperationException>(() => EquationOperations.EditMatrix(matrix, matrix.Children[0].Id, EquationMatrixOperation.DeleteColumn));
        for (var i = 1; i < 10; i++) matrix = EquationOperations.EditMatrix(matrix, matrix.Children[0].Id, EquationMatrixOperation.InsertColumnRight).Root;
        Assert.Throws<InvalidOperationException>(() => EquationOperations.EditMatrix(matrix, matrix.Children[0].Id, EquationMatrixOperation.InsertColumnRight));
        Assert.Equal(10, matrix.Columns);
    }
    [Fact]
    public void LinearConversionDoesNotDiscardOtherArguments()
    {
        var fraction = EquationNode.Structure(EquationKind.Fraction, EquationNode.Leaf("a+b"), EquationNode.Leaf("c"));
        var root = EquationNode.Row(EquationNode.Leaf("x="), fraction, EquationNode.Leaf("+1"));
        var result = EquationOperations.ConvertStructureToText(root, fraction.Children[0].Id);
        Assert.Equal("x=(a+b)/(c)+1", result.Root.ToLinearText());
        Assert.DoesNotContain(result.Root.DescendantsAndSelf(), n => n.Kind == EquationKind.Fraction);
        Assert.Contains(root.DescendantsAndSelf(), n => n.Kind == EquationKind.Fraction);
    }
    [Fact]
    public void ExplicitStructureDeletionLeavesEditableSlot()
    {
        var fraction = EquationTemplates.Create("fraction"); var result = EquationOperations.DeleteStructure(fraction, fraction.Children[0].Id);
        Assert.Equal(EquationKind.Text, result.Root.Kind); Assert.Equal("", result.Root.Text); Assert.Equal(result.Root.Id, result.ActiveSlotId);
    }
    [Theory]
    [InlineData("A😀B", 2, 0, 1, 0)]
    [InlineData("Ae\u0301B", 2, 1, 1, 2)]
    [InlineData("A👩‍💻B", 2, 1, 1, 5)]
    public void StructuralInsertionCannotSplitGraphemes(string text, int start, int length, int expectedStart, int expectedLength)
    {
        Assert.Equal((expectedStart, expectedLength), EquationOperations.GraphemeRange(text, start, length));
    }
    [Fact]
    public void EditedMatrixRoundTripsAsRealOfficeMath()
    {
        var root = Matrix(); root = EquationOperations.EditMatrix(root, root.Children[0].Id, EquationMatrixOperation.InsertColumnRight).Root;
        root = EquationOperations.EditMatrix(root, root.Children[0].Id, EquationMatrixOperation.InsertRowBelow).Root;
        var imported = OfficeMathCodec.Read(OfficeMathCodec.Write(root));
        Assert.Equal(root.ToLinearText(), imported.ToLinearText()); Assert.Equal(3, imported.Columns); Assert.Equal(9, imported.Children.Count);
    }
}
