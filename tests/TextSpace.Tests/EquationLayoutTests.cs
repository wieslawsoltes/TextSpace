using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class EquationLayoutTests
{
    [Theory]
    [InlineData("fraction")] [InlineData("radical")] [InlineData("superscript")] [InlineData("subscript")] [InlineData("subsuperscript")]
    [InlineData("parentheses")] [InlineData("brackets")] [InlineData("matrix")] [InlineData("sum")] [InlineData("integral")]
    [InlineData("bar")] [InlineData("vector")] [InlineData("quadratic")] [InlineData("pythagoras")] [InlineData("identity")] [InlineData("blank")]
    public void TemplatesHaveFiniteLayoutAndEditableSlots(string name)
    {
        var root = EquationTemplates.Create(name); var layout = new EquationLayouter(new MonospaceTextMetrics()).Layout(root);
        Assert.True(double.IsFinite(layout.Width + layout.Height)); Assert.True(layout.Width > 0); Assert.True(layout.Height > 0);
        Assert.Equal(root.DescendantsAndSelf().Count(n => n.Kind == EquationKind.Text), layout.Slots.Count);
        Assert.All(layout.Slots, slot => { Assert.True(slot.Bounds.Width > 0); Assert.True(slot.Bounds.Height > 0); Assert.Equal(slot.Id, layout.HitTest(slot.Bounds.X + slot.Bounds.Width / 2, slot.Bounds.Y + slot.Bounds.Height / 2)!.Id); });
    }
    [Fact]
    public void FractionNavigationFindsDenominatorBelowNumerator()
    {
        var root = EquationNode.Structure(EquationKind.Fraction, EquationNode.Leaf("a+b"), EquationNode.Leaf("c"));
        var layout = new EquationLayouter(new MonospaceTextMetrics()).Layout(root);
        Assert.Equal(root.Children[1].Id, layout.VerticalNeighbor(root.Children[0].Id, true)!.Id);
        Assert.Equal(root.Children[0].Id, layout.VerticalNeighbor(root.Children[1].Id, false)!.Id); Assert.Single(layout.Rules);
    }
    [Fact]
    public void EquationsRoundtripAsStructureNotPictures()
    {
        var equation = new EquationBlock { Root = EquationTemplates.Create("quadratic") };
        var doc = new DocumentModel { Blocks = [equation, new Paragraph("body")] };
        var clone = Assert.IsType<EquationBlock>(DocumentJson.Clone(doc).Blocks[0]);
        Assert.Equal(equation.Root.ToLinearText(), clone.Root.ToLinearText()); Assert.Equal(equation.Root.Id, clone.Root.Id);
    }
    [Fact]
    public void TreeReplacementDoesNotAliasTheEditorDraft()
    {
        var root = EquationTemplates.Create("fraction"); var id = root.Children[0].Id; var replacement = EquationNode.Leaf("n");
        var result = EquationTemplates.Replace(root, id, replacement); replacement.Text = "mutated";
        Assert.Equal("n", result.Children[0].Text); Assert.Equal("", root.Children[0].Text);
    }
    [Fact]
    public void CyclesAndSharedNodesAreRejected()
    {
        var cycle = EquationNode.Row(); cycle.Children.Add(cycle); Assert.Throws<InvalidDataException>(() => EquationRules.Validate(cycle));
        var leaf = EquationNode.Leaf("x"); Assert.Throws<InvalidDataException>(() => EquationRules.Validate(EquationNode.Row(leaf, leaf)));
    }
    [Theory]
    [InlineData(EquationKind.Fraction)] [InlineData(EquationKind.Radical)] [InlineData(EquationKind.SubSuperscript)] [InlineData(EquationKind.Matrix)]
    public void InvalidArityIsRejected(EquationKind kind) => Assert.Throws<InvalidDataException>(() => EquationRules.Validate(new() { Kind = kind }));
    [Fact]
    public void InvalidUnicodeAndOversizedTreesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => EquationRules.Validate(EquationNode.Leaf("\ud800")));
        Assert.Throws<InvalidDataException>(() => EquationRules.Validate(EquationNode.Leaf(new string('x', EquationRules.MaximumCharacters + 1))));
        var root = EquationNode.Leaf("x"); for (var i = 0; i < 40; i++) root = EquationNode.Row(root);
        Assert.Throws<InvalidDataException>(() => EquationRules.Validate(root));
    }
    [Fact]
    public void ContentKeyChangesForStructureAndTextButNotIdentity()
    {
        var root = EquationTemplates.Create("quadratic"); Assert.Equal(EquationRules.ContentKey(root), EquationRules.ContentKey(root.Clone(true)));
        var copy = root.Clone(); copy.Children[0].Text = "y = "; Assert.NotEqual(EquationRules.ContentKey(root), EquationRules.ContentKey(copy));
    }
}
