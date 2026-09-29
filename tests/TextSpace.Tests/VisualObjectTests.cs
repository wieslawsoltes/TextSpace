using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Layout;
using Xunit;

namespace TextSpace.Tests;

public sealed class VisualObjectTests
{
    [Theory]
    [InlineData(ShapeKind.Rectangle)] [InlineData(ShapeKind.RoundedRectangle)] [InlineData(ShapeKind.Ellipse)]
    [InlineData(ShapeKind.Diamond)] [InlineData(ShapeKind.Triangle)] [InlineData(ShapeKind.Line)] [InlineData(ShapeKind.Arrow)] [InlineData(ShapeKind.TextBox)]
    public void ShapesHaveNativeIdentityAndGeometry(ShapeKind kind)
    {
        var session = new EditorSession(new DocumentModel()); var id = session.InsertShape(kind);
        var clone = DocumentJson.Clone(session.Document); var shape = Assert.IsType<ShapeBlock>(BlockTree.Find(clone.Blocks, id)!.Value.Block);
        Assert.Equal(kind, shape.Kind); Assert.Equal(180, shape.Width); Assert.Equal(id, shape.Id);
        Assert.Equal(1, session.UndoCount); session.Undo(); Assert.Null(session.FindVisual(id)); session.Redo(); Assert.NotNull(session.FindVisual(id));
    }
    [Fact]
    public void DetachedGestureCommitsOneHistoryEntry()
    {
        var session = new EditorSession(new DocumentModel()); var id = session.InsertShape(ShapeKind.Rectangle); var before = session.UndoCount;
        using var draft = new VisualEditDraft(session, id);
        for (var i = 0; i < 100; i++) draft.Value.Width = 200 + i;
        Assert.Equal(180, session.FindVisual(id)!.Width); Assert.Equal(before, session.UndoCount);
        draft.Commit("Resize object"); Assert.Equal(before + 1, session.UndoCount); Assert.Equal(299, session.FindVisual(id)!.Width);
        session.Undo(); Assert.Equal(180, session.FindVisual(id)!.Width);
        Assert.Throws<InvalidOperationException>(() => draft.Commit("again"));
    }
    [Fact]
    public void CancelledGestureIsNotAnEdit()
    {
        var session = new EditorSession(new DocumentModel()); var id = session.InsertShape(ShapeKind.Ellipse); session.MarkSaved(); var undo = session.UndoCount;
        using (var draft = new VisualEditDraft(session, id)) draft.Value.Placement = new() { X = 42, Rotation = 30 };
        Assert.False(session.IsDirty); Assert.Equal(undo, session.UndoCount); Assert.Equal(0, session.FindVisual(id)!.Placement.X);
    }
    [Fact]
    public void StaleAndReadOnlyGesturesCannotOverwriteNewerWork()
    {
        var session = new EditorSession(new DocumentModel()); var id = session.InsertShape(ShapeKind.Rectangle);
        using var stale = new VisualEditDraft(session, id); session.InsertText("newer text");
        Assert.False(stale.IsCurrent); Assert.Throws<InvalidOperationException>(() => stale.Commit("stale"));
        using var locked = new VisualEditDraft(session, id); session.IsReadOnly = true;
        Assert.Throws<InvalidOperationException>(() => locked.Commit("locked"));
        Assert.Throws<InvalidOperationException>(() => session.DeleteVisual(id));
    }
    [Fact]
    public void DuplicateOwnsIndependentMathNodeIdentities()
    {
        var session = new EditorSession(new DocumentModel()); var id = session.InsertEquation("quadratic"); var copyId = session.DuplicateVisual(id);
        var first = Assert.IsType<EquationBlock>(session.FindVisual(id)); var second = Assert.IsType<EquationBlock>(session.FindVisual(copyId));
        Assert.Equal(first.Root.ToLinearText(), second.Root.ToLinearText());
        Assert.Empty(first.Root.DescendantsAndSelf().Select(n => n.Id).Intersect(second.Root.DescendantsAndSelf().Select(n => n.Id)));
        second.Root.Children[0].Text = "changed"; Assert.NotEqual(first.Root.ToLinearText(), second.Root.ToLinearText());
    }
    [Fact]
    public void InvalidReplacementLeavesModelAndHistoryUntouched()
    {
        var session = new EditorSession(new DocumentModel()); var id = session.InsertShape(ShapeKind.TextBox); var json = DocumentJson.Save(session.Document); var undo = session.UndoCount;
        Assert.Throws<InvalidDataException>(() => session.EditVisual(id, "bad", b => b.Width = double.NaN));
        Assert.Equal(json, DocumentJson.Save(session.Document)); Assert.Equal(undo, session.UndoCount);
    }
    [Fact]
    public void SourceCropIsNonDestructiveAndValidated()
    {
        var image = new ImageBlock { Data = [1, 2, 3], Crop = new() { Left = 0.1, Bottom = 0.2 } };
        var clone = Assert.IsType<ImageBlock>(BlockTree.CloneVisual(image)); Assert.Equal(image.Data, clone.Data); Assert.NotSame(image.Data, clone.Data);
        clone.Crop = new() { Left = 0.7, Right = 0.4 }; Assert.Throws<InvalidDataException>(() => VisualBlockRules.Validate(clone));
        Assert.Equal(0.1, image.Crop.Left);
    }
    [Fact]
    public void LegacyImagePropertiesKeepTheirDefaults()
    {
        var doc = DocumentJson.Load("{\"formatVersion\":1,\"blocks\":[{\"$type\":\"image\",\"data\":\"\"}]}");
        var image = Assert.IsType<ImageBlock>(doc.Blocks[0]); Assert.Equal(360, image.Width); Assert.Equal(240, image.Height);
        Assert.Equal(TextAlignment.Center, image.Alignment); Assert.False(image.Placement.Floating); Assert.Equal(0, image.Crop.Left);
    }
    [Fact]
    public void NestedObjectsParticipateInTableLayout()
    {
        var table = TableBlock.Create(2, 2); var equation = new EquationBlock { Root = EquationTemplates.Create("quadratic") };
        var shape = new ShapeBlock { Text = "nested" }; table.Rows[0].Cells[0].Blocks.Add(equation); table.Rows[1].Cells[1].Blocks.Add(shape);
        var document = new DocumentModel { Blocks = [table, new Paragraph("after")] }; DocumentJson.Validate(document);
        var layout = new PageLayoutEngine(new MonospaceTextMetrics()).Layout(document);
        Assert.Single(layout.Pages.SelectMany(p => p.Objects).Where(o => o.Object.Id == equation.Id && !o.IsReplica));
        Assert.Single(layout.Pages.SelectMany(p => p.Objects).Where(o => o.Object.Id == shape.Id && !o.IsReplica));
        Assert.All(layout.Pages.SelectMany(p => p.Cells), cell => { Assert.True(cell.Row >= 0); Assert.True(cell.Column >= 0); });
    }
    [Fact]
    public void FloatingObjectDoesNotConsumeParagraphFlow()
    {
        var first = new Paragraph("first"); var second = new Paragraph("second"); var shape = new ShapeBlock { Placement = new() { Floating = true, X = 24, Y = 24 } };
        var doc = new DocumentModel { Blocks = [first, shape, second] }; var engine = new PageLayoutEngine(new MonospaceTextMetrics());
        var floating = engine.Layout(doc).Lines.First(l => l.ParagraphId == second.Id).Y;
        shape.Placement = new(); var inline = engine.Layout(doc).Lines.First(l => l.ParagraphId == second.Id).Y;
        Assert.True(inline > floating);
    }
    [Theory]
    [InlineData(VisualHandle.NorthWest)] [InlineData(VisualHandle.North)] [InlineData(VisualHandle.NorthEast)]
    [InlineData(VisualHandle.East)] [InlineData(VisualHandle.SouthEast)] [InlineData(VisualHandle.South)] [InlineData(VisualHandle.SouthWest)] [InlineData(VisualHandle.West)]
    public void EveryResizeHandlePreservesLockedAspect(VisualHandle handle)
    {
        var result = VisualGeometry.Resize(200, 100, handle, 20, 10, true);
        Assert.Equal(2, result.Width / result.Height, 8); Assert.InRange(result.Width, 6, 4000); Assert.InRange(result.Height, 6, 4000);
    }
    [Fact]
    public void InverseRotationMapsBackToObjectSpace()
    {
        var bounds = new RectD(30, 40, 200, 100); var p = VisualGeometry.Rotate(-100, -50, 37);
        var local = VisualGeometry.Local(bounds, 130 + p.X, 90 + p.Y, 37); Assert.Equal(0, local.X, 8); Assert.Equal(0, local.Y, 8);
    }
    [Fact]
    public void AdjacentColumnResizePreservesTotalAndOtherColumns()
    {
        var result = EditorSession.ResizeTableBoundary([100, 100, 100], 1, 150);
        Assert.Equal(188, result[0]); Assert.Equal(12, result[1]); Assert.Equal(100, result[2]); Assert.Equal(300, result.Sum());
    }
    [Fact]
    public void ColumnAndRowChangesAreUndoable()
    {
        var session = new EditorSession(new DocumentModel()); session.InsertTable(2, 2); var table = session.CurrentTable!; var id = table.Id; var count = session.UndoCount;
        session.SetTableColumnWidths(id, [60, 180]); session.SetTableRowHeight(id, 1, 80);
        Assert.Equal(count + 2, session.UndoCount); session.Undo(); Assert.Equal(0, session.FindTable(id)!.Rows[1].MinimumHeight);
        session.Undo(); Assert.Equal([0.5, 0.5], session.FindTable(id)!.ColumnWidths);
    }
    [Fact]
    public void TableRectangleExpandsToFullMergedCell()
    {
        var session = new EditorSession(new DocumentModel()); session.InsertTable(3, 3); var id = session.CurrentTable!.Id;
        session.MergeTableCells(0, 0, 2, 2); var selection = session.SelectTableRectangle(id, 1, 1, 2, 2);
        Assert.Equal(new TableSelection(id, 0, 0, 3, 3), selection); session.MergeTableSelection(selection);
        Assert.Single(new TableGrid(session.FindTable(id)!).Regions);
    }
}
