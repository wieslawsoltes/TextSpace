using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class TypographyLayoutTests
{
    private static readonly TextStyle Font = new() { FontSize = 10 };
    private static Paragraph Paragraph(string text, ParagraphFormat? format = null) => new(text, Font, format ?? new() { LineSpacing = 1, SpaceAfter = 0 });
    private static List<LayoutLine> Layout(Paragraph p, double width = 200, double interval = 36) => new ParagraphLayouter(new MonospaceTextMetrics()).Layout(p, width, 0, interval);
    private static string Visible(LayoutLine line) => string.Concat(line.Chunks.Select(c => c.DisplayText ?? c.Text));

    [Theory]
    [InlineData(TabAlignment.Left, 100)]
    [InlineData(TabAlignment.Right, 89)]
    [InlineData(TabAlignment.Center, 94.5)]
    public void AlignsFollowingText(TabAlignment alignment, double expected)
    {
        var p = Paragraph("\tAB", new() { TabStops = [new() { Position = 100, Alignment = alignment }] });
        var line = Assert.Single(Layout(p)); Assert.Equal(expected, line.Chunks[1].X, 8);
    }
    [Theory]
    [InlineData("12.34", '.', 89)]
    [InlineData("12,34", ',', 89)]
    [InlineData("123", '.', 83.5)]
    public void AlignsDecimalCharacterOrRightEdge(string number, char separator, double expected)
    {
        var p = Paragraph("\t" + number, new() { TabStops = [new() { Position = 100, Alignment = TabAlignment.Decimal, DecimalCharacter = separator }] });
        Assert.Equal(expected, Layout(p)[0].Chunks[1].X, 8);
    }
    [Fact] public void AlignmentMeasuresAcrossFormattingRuns()
    {
        var p = Paragraph(""); p.Runs = [new("\t", Font), new("A", Font), new("B", Font with { FontSize = 20, Bold = true })];
        p.Format = p.Format with { TabStops = [new() { Position = 100, Alignment = TabAlignment.Right }] };
        var line = Assert.Single(Layout(p)); Assert.Equal(83.5, line.Chunks[1].X, 8); Assert.Equal(100, line.Chunks[^1].X + line.Chunks[^1].Width, 8);
    }
    [Fact] public void RightRelativeTabsFollowAvailableColumnEdge()
    {
        var p = Paragraph("\tAB", new() { RightIndent = 10, TabStops = [new() { Position = 5, RelativeToRightEdge = true, Alignment = TabAlignment.Right }] });
        Assert.Equal(124, Layout(p, 150)[0].Chunks[1].X, 8); Assert.Equal(224, Layout(p, 250)[0].Chunks[1].X, 8);
    }
    [Fact] public void DefaultStopsAreMeasuredFromColumnOrigin()
    {
        var p = Paragraph("\tAB", new() { LeftIndent = 8, FirstLineIndent = 5 });
        var line = Layout(p, interval: 40)[0]; Assert.Equal(13, line.Chunks[0].X); Assert.Equal(27, line.Chunks[0].Width); Assert.Equal(40, line.Chunks[1].X);
    }
    [Fact] public void BehindOrOverlappingCustomStopsDoNotMoveTextBackwards()
    {
        var p = Paragraph("AAAA\tBBBB", new() { TabStops = [new() { Position = 10 }, new() { Position = 30, Alignment = TabAlignment.Right }, new() { Position = 80 }] });
        var line = Layout(p)[0]; Assert.Equal(80, line.Chunks.Last(c => c.Text == "BBBB").X); Assert.All(line.Chunks, c => Assert.True(c.Width >= 0));
    }
    [Fact] public void EachTabMeasuresOnlyItsFollowingSegment()
    {
        var p = Paragraph("\tAB\tCD", new() { TabStops = [new() { Position = 60, Alignment = TabAlignment.Right }, new() { Position = 120, Alignment = TabAlignment.Right }] });
        var line = Assert.Single(Layout(p)); Assert.Equal(49, line.Chunks[1].X, 8); Assert.Equal(109, line.Chunks[3].X, 8);
    }
    [Fact] public void ParagraphCenteringDoesNotMoveExplicitStops()
    {
        var p = Paragraph("\tAB", new() { Alignment = TextAlignment.Center, TabStops = [new() { Position = 100 }] });
        Assert.Equal(100, Layout(p)[0].Chunks[1].X);
    }
    [Fact] public void LeadersAndBarStopsRemainGeometryNotEditableText()
    {
        var p = Paragraph("A\tB", new() { TabStops = [new() { Position = 100, Leader = TabLeader.Dot }, new() { Position = 150, Alignment = TabAlignment.Bar }] });
        var line = Assert.Single(Layout(p)); Assert.Equal(TabLeader.Dot, line.Chunks[1].TabLeader); Assert.Equal(new[] { 150d }, line.BarTabs); Assert.Equal(p.Text, string.Concat(line.Chunks.Select(c => c.Text)));
    }
    [Fact] public void TabMetadataAndBarArraysAreIndependentlyCached()
    {
        var cache = new ParagraphLayoutCache(new MonospaceTextMetrics());
        var p = Paragraph("A\tB", new() { TabStops = [new() { Position = 100, Leader = TabLeader.Dot }, new() { Position = 150, Alignment = TabAlignment.Bar }] });
        var first = cache.Layout(p, 200, 0); first[0].BarTabs[0] = 999;
        var second = cache.Layout(p, 200, 10); Assert.Equal(150, second[0].BarTabs[0]); Assert.Equal(TabLeader.Dot, second[0].Chunks[1].TabLeader); Assert.Equal(10, second[0].Start);
        p.Format = p.Format with { TabStops = [new() { Position = 120, Leader = TabLeader.Hyphen }] };
        Assert.Equal(120, cache.Layout(p, 200, 0)[0].Chunks[^1].X); Assert.Equal(2, cache.Misses);
    }
    [Fact] public void ChangingDefaultTabIntervalInvalidatesCachedLayout()
    {
        var cache = new ParagraphLayoutCache(new MonospaceTextMetrics()); var p = Paragraph("\tB");
        Assert.Equal(36, cache.Layout(p, 200, 0, 36)[0].Chunks[1].X); Assert.Equal(48, cache.Layout(p, 200, 0, 48)[0].Chunks[1].X);
        Assert.Equal(2, cache.Misses);
    }
    [Fact] public void FormattingBoundaryDoesNotBecomeWordBreak()
    {
        var p = Paragraph(""); p.Runs = [new("AA ", Font), new("BB", Font), new("CC", Font with { Bold = true })];
        var lines = Layout(p, 28); Assert.Equal(2, lines.Count); Assert.Equal("AA ", Visible(lines[0])); Assert.Equal("BBCC", Visible(lines[1]));
    }
    [Theory]
    [InlineData('\u00a0')]
    [InlineData('\u202f')]
    [InlineData('\u2011')]
    public void NonbreakingCharactersKeepAdjacentTextTogether(char separator)
    {
        var p = Paragraph("A B" + separator + "C"); var lines = Layout(p, 22);
        Assert.Equal("A ", Visible(lines[0])); Assert.Equal("B" + separator + "C", Visible(lines[1]));
    }
    [Fact] public void SoftHyphenIsInvisibleWhenNotBreaking()
    {
        var p = Paragraph("ab\u00adcd"); var line = Assert.Single(Layout(p));
        Assert.Equal("abcd", Visible(line)); Assert.Equal(p.Text, string.Concat(line.Chunks.Select(c => c.Text))); Assert.Equal(22, line.Width, 8);
        Assert.Equal(line.CaretX(2), line.CaretX(3)); Assert.Equal(5, line.End);
    }
    [Fact] public void SoftHyphenBecomesVisibleOnlyAtSelectedBreak()
    {
        var p = Paragraph("ab\u00adcd"); var lines = Layout(p, 17);
        Assert.Equal(2, lines.Count); Assert.Equal("ab-", Visible(lines[0])); Assert.Equal("cd", Visible(lines[1])); Assert.Equal(3, lines[0].End); Assert.Equal(3, lines[1].Start);
        Assert.Equal(16.5, lines[0].CaretX(3), 8); Assert.Equal(p.Text, string.Concat(lines.SelectMany(l => l.Chunks).Select(c => c.Text)));
    }
    [Fact] public void SoftHyphenAtStyleBoundaryPreservesCharacterMapping()
    {
        var p = Paragraph(""); p.Runs = [new("ab", Font), new("\u00ad", Font with { Bold = true }), new("cd", Font)];
        var lines = Layout(p, 17); Assert.Equal("ab-", Visible(lines[0])); Assert.Equal("cd", Visible(lines[1])); Assert.True(lines[0].Chunks[^1].Style.Bold);
    }
    [Fact] public void ZeroWidthSpaceOffersInvisibleBreakOpportunity()
    {
        var lines = Layout(Paragraph("ab\u200bcd"), 17); Assert.Equal(2, lines.Count); Assert.Equal("ab", Visible(lines[0])); Assert.Equal("cd", Visible(lines[1]));
    }
    [Fact] public void TerminalSoftBreakCreatesEditableEmptyLine()
    {
        var lines = Layout(Paragraph("A\u2028")); Assert.Equal(2, lines.Count); Assert.Equal(2, lines[1].Start); Assert.Equal(2, lines[1].End); Assert.True(lines[1].LastInParagraph);
    }
    [Fact] public void OversizedGraphemesAreNotSplitDuringEmergencyWrap()
    {
        var text = "👩‍💻👩‍💻"; var p = new Paragraph(text, Font with { FontSize = 30 }, new() { LeftIndent = 188 }); var lines = Layout(p, 200);
        Assert.All(lines.SelectMany(l => l.Chunks), c => Assert.Equal("👩‍💻", c.Text));
        Assert.Equal(text, string.Concat(lines.SelectMany(l => l.Chunks).Select(c => c.Text)));
    }
    [Fact] public void TabEditIsAtomicUndoableAndRejectsInvalidState()
    {
        var editor = new EditorSession(DocumentJson.FromText("First\nSecond")); editor.SelectAll();
        editor.SetTabStops([new() { Position = 80, Alignment = TabAlignment.Right, Leader = TabLeader.Dot }], 48);
        Assert.All(editor.Document.Paragraphs(), p => Assert.Single(p.Format.TabStops)); Assert.Equal(48, editor.Document.DefaultTabStop);
        editor.Undo(); Assert.All(editor.Document.Paragraphs(), p => Assert.Empty(p.Format.TabStops)); Assert.Equal(36, editor.Document.DefaultTabStop);
        editor.Redo(); var saved = DocumentJson.Save(editor.Document);
        Assert.Throws<InvalidDataException>(() => editor.SetTabStops([new() { Position = double.NaN }], 0)); Assert.Equal(saved, DocumentJson.Save(editor.Document));
        editor.IsReadOnly = true; Assert.Throws<InvalidOperationException>(() => editor.ClearTabStops());
    }
    [Fact] public void NativePersistencePreservesImmutableTypography()
    {
        var document = new DocumentModel { DefaultTabStop = 48, Blocks = [Paragraph("A\tB", new() { KeepLinesTogether = true, WidowControl = false, TabStops = [new() { RelativeToRightEdge = true, Leader = TabLeader.Dot, Alignment = TabAlignment.Right }] })] };
        var copy = DocumentJson.Clone(document); var format = copy.Paragraphs().First().Format;
        Assert.Equal(48, copy.DefaultTabStop); Assert.True(format.KeepLinesTogether); Assert.False(format.WidowControl); Assert.True(Assert.Single(format.TabStops).RelativeToRightEdge);
    }
    [Theory]
    [InlineData(0)] [InlineData(double.NaN)] [InlineData(721)]
    public void InvalidDocumentTabIntervalIsRejected(double value)
    {
        Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(new DocumentModel { DefaultTabStop = value }));
    }
    [Fact] public void DuplicateStopsAndExcessiveStopsAreRejected()
    {
        var d = new DocumentModel { Blocks = [Paragraph("", new() { TabStops = [new() { Position = 30 }, new() { Position = 30.001 }] })] };
        Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(d));
        d.Paragraphs().First().Format = new() { TabStops = [.. Enumerable.Range(0, 129).Select(i => new TabStop { Position = i })] };
        Assert.Throws<InvalidDataException>(() => DocumentJson.Validate(d));
    }
}
