using System.Text.RegularExpressions;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class ViewportStatisticsTests
{
    [Theory]
    [InlineData(-100, -1, 0, 0)]
    [InlineData(-100, 0, 0, 1)]
    [InlineData(0, 100, 0, 1)]
    [InlineData(100, 100, 0, 1)]
    [InlineData(100.1, 123.9, 1, 1)]
    [InlineData(124, 124, 1, 2)]
    [InlineData(124, 324, 1, 2)]
    [InlineData(324.1, 347.9, 2, 2)]
    [InlineData(348, 350, 2, 3)]
    [InlineData(0, 1000, 0, 3)]
    [InlineData(900, 1000, 0, 0)]
    public void VisiblePageRangeMatchesGeometry(double top, double bottom, int start, int end)
    {
        var layout = new DocumentLayout(new(), [new(0, new() { Height = 100 }), new(1, new() { Height = 200 }), new(2, new() { Height = 300 })]);
        Assert.Equal(new VisiblePageRange(start, end), layout.VisiblePages(top, bottom));
    }

    [Fact]
    public void RandomViewportQueriesEqualBruteForceOnMixedPaper()
    {
        var random = new Random(4545); var pages = Enumerable.Range(0, 500).Select(i => new LayoutPage(i, new() { Height = random.Next(144, 1200) })).ToArray();
        var layout = new DocumentLayout(new(), pages);
        for (var i = 0; i < 500; i++)
        {
            var top = random.NextDouble() * (layout.Height + 2000) - 1000;
            var bottom = top + random.NextDouble() * 2500;
            var expected = pages.Where(p => layout.PageTop(p.Index) <= bottom && layout.PageTop(p.Index) + p.Settings.Height >= top).Select(p => p.Index).ToArray();
            var actual = layout.VisiblePages(top, bottom);
            Assert.Equal(expected, Enumerable.Range(actual.Start, actual.Count));
        }
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(100, 99)]
    public void InvalidViewportBoundsFailExplicitly(double top, double bottom)
    {
        var layout = new DocumentLayout(new(), [new(0, new())]);
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.VisiblePages(top, bottom));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("hello world", 2)]
    [InlineData("isn't O’Connor x-ray", 3)]
    [InlineData("Zażółć gęślą jaźń", 3)]
    [InlineData("😀 123\n漢字", 2)]
    public void SnapshotWordCountsMatchDocumentSemantics(string text, int expected)
    {
        var snapshot = new DocumentTextSnapshot(text);
        Assert.Equal(expected, snapshot.WordCount); Assert.Equal(expected, DocumentTextSnapshot.CountWords(text));
        Assert.Equal(text.Count(c => !char.IsWhiteSpace(c)), snapshot.CharactersWithoutSpaces);
        Assert.Equal(expected, snapshot.WordsIn(new(0, text.Length)));
    }

    [Fact]
    public void SnapshotRetainsCapturedTextAfterSourceMutation()
    {
        var document = DocumentJson.FromText("original text"); var first = new DocumentTextSnapshot(document);
        document.Paragraphs().First().Runs[0].Text = "changed";
        Assert.Equal("original text", first.Text); Assert.Equal(2, first.WordCount);
        Assert.Equal("changed", new DocumentTextSnapshot(document).Text);
        Assert.Equal(1, document.WordCount);
    }

    [Fact]
    public void ReversedSelectionUsesOrderedRangeAndValidatesBounds()
    {
        var snapshot = new DocumentTextSnapshot("one two three");
        Assert.Equal(2, snapshot.WordsIn(new(7, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.WordsIn(new(-1, 3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.WordsIn(new(0, 100)));
    }
}
