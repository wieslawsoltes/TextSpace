using TextSpace.Core;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class ParagraphPaginationTests
{
    private static readonly TextStyle Font = new() { FontSize = 10 };
    private static Paragraph Lines(int count, ParagraphFormat? format = null) => new(string.Join('\u2028', Enumerable.Repeat("Text", count)), Font,
        format ?? new() { SpaceAfter = 0, LineSpacing = 1, WidowControl = false });
    private static DocumentLayout Layout(params Block[] blocks) => new PageLayoutEngine(new MonospaceTextMetrics()).Layout(new()
    { Page = new() { Width = 200, Height = 144, MarginTop = 51, MarginBottom = 51, MarginLeft = 36, MarginRight = 36 }, Blocks = [.. blocks] });
    private static int[] PerPage(DocumentLayout layout, Paragraph p) => layout.Pages.Select(page => page.Lines.Count(line => line.ParagraphId == p.Id)).ToArray();

    [Fact] public void DisabledWidowControlAllowsSingleLeadingLine()
    {
        var p = Lines(3); Assert.Equal(new[] { 1, 2 }, PerPage(Layout(Lines(3), p), p));
    }
    [Fact] public void EnabledWidowControlMovesLeadingOrphanToNextPage()
    {
        var p = Lines(3); p.Format = p.Format with { WidowControl = true }; Assert.Equal(new[] { 0, 3 }, PerPage(Layout(Lines(3), p), p));
    }
    [Fact] public void EnabledWidowControlDoesNotLeaveSingleTrailingLine()
    {
        var p = Lines(4); p.Format = p.Format with { WidowControl = true }; Assert.Equal(new[] { 2, 2 }, PerPage(Layout(Lines(1), p), p));
    }
    [Fact] public void DisabledWidowControlAllowsSingleTrailingLine()
    {
        var p = Lines(4); Assert.Equal(new[] { 3, 1 }, PerPage(Layout(Lines(1), p), p));
    }
    [Fact] public void KeepLinesTogetherMovesAParagraphThatFitsNextPage()
    {
        var p = Lines(3); p.Format = p.Format with { KeepLinesTogether = true }; Assert.Equal(new[] { 0, 3 }, PerPage(Layout(Lines(2), p), p));
    }
    [Fact] public void KeepWithNextUsesActualFollowingLineHeights()
    {
        var heading = Lines(1); heading.Format = heading.Format with { KeepWithNext = true };
        var body = Lines(2); body.Format = body.Format with { WidowControl = true };
        var result = Layout(Lines(3), heading, body); Assert.Equal(0, PerPage(result, heading)[0]); Assert.Equal(1, PerPage(result, heading)[1]); Assert.Equal(new[] { 0, 2 }, PerPage(result, body));
    }
    [Fact] public void KeepWithNextHonorsConsecutiveHeadingChain()
    {
        var first = Lines(1); first.Format = first.Format with { KeepWithNext = true };
        var second = Lines(1); second.Format = second.Format with { KeepWithNext = true }; var body = Lines(1);
        var result = Layout(Lines(2), first, second, body); Assert.Equal(new[] { 0, 1 }, PerPage(result, first)); Assert.Equal(new[] { 0, 1 }, PerPage(result, second));
    }
    [Fact] public void OverHeightKeepLinesAndKeepChainAlwaysProgress()
    {
        var huge = Lines(12); huge.Format = huge.Format with { KeepLinesTogether = true, KeepWithNext = true, WidowControl = true };
        var result = Layout(huge, Lines(1)); Assert.Equal(4, result.Pages.Count); Assert.Equal(12, result.Lines.Count(l => l.ParagraphId == huge.Id)); Assert.All(result.Pages, page => Assert.NotEmpty(page.Lines));
    }
    [Fact] public void OversizedSingleLineCannotCausePaginationLoop()
    {
        var huge = new Paragraph("Oversized", new() { FontSize = 100 }, new() { KeepLinesTogether = true, SpaceAfter = 0, LineSpacing = 1 });
        var result = Layout(huge, Lines(1)); Assert.InRange(result.Pages.Count, 1, 10); Assert.NotEmpty(result.Lines);
    }
    [Fact] public void PageBreakBeforeWinsOverKeepWithNext()
    {
        var first = Lines(1); first.Format = first.Format with { KeepWithNext = true }; var next = Lines(1); next.Format = next.Format with { PageBreakBefore = true };
        var result = Layout(first, next); Assert.Equal(new[] { 1, 0 }, PerPage(result, first)); Assert.Equal(new[] { 0, 1 }, PerPage(result, next));
    }
}
