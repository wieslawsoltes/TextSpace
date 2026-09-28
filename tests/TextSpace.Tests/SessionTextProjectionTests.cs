using TextSpace.Documents;
using TextSpace.Editing;
using Xunit;

namespace TextSpace.Tests;

public sealed class SessionTextProjectionTests
{
    [Fact]
    public void RepeatedPresentationReadsCaptureOnce()
    {
        var session = new EditorSession(DocumentJson.FromText("Hello world"));
        using var projection = new SessionTextProjection(session);
        var text = projection.Text;
        for (var i = 0; i < 1000; i++) Assert.Same(text, projection.Text);
        Assert.Equal(1, projection.Captures);
    }

    [Fact]
    public void SelectionViewAndSavedNotificationsDoNotRebuildText()
    {
        var session = new EditorSession(DocumentJson.FromText("Hello world"));
        using var projection = new SessionTextProjection(session);
        var text = projection.Text;
        session.SetSelection(0, 5); session.Notify(EditorChangeKind.View); session.MarkSaved();
        Assert.Same(text, projection.Text); Assert.Equal(1, projection.Captures);
    }

    [Fact]
    public void TypingUndoRedoAndLoadInvalidate()
    {
        var session = new EditorSession(DocumentJson.FromText("Original"));
        using var projection = new SessionTextProjection(session);
        Assert.Equal("Original", projection.Text);
        session.InsertText("New "); Assert.Equal("New Original", projection.Text);
        session.Undo(); Assert.Equal("Original", projection.Text);
        session.Redo(); Assert.Equal("New Original", projection.Text);
        session.Load(DocumentJson.FromText("Loaded")); Assert.Equal("Loaded", projection.Text);
        Assert.Equal(5, projection.Captures);
    }

    [Fact]
    public void ExplicitDocumentNotificationSupportsExternalModelEdits()
    {
        var session = new EditorSession(DocumentJson.FromText("Before"));
        using var projection = new SessionTextProjection(session);
        Assert.Equal("Before", projection.Text);
        session.Document.Paragraphs().First().Runs[0].Text = "After";
        session.Notify(EditorChangeKind.Document, "External update");
        Assert.Equal("After", projection.Text);
    }

    [Fact]
    public void RollbackCannotExposePartiallyMutatedText()
    {
        var session = new EditorSession(DocumentJson.FromText("Before"));
        using var projection = new SessionTextProjection(session);
        Assert.Equal("Before", projection.Text);
        Assert.Throws<InvalidOperationException>(() => session.Execute("Fail", () =>
        {
            session.Document.Paragraphs().First().Runs[0].Text = "Partial";
            throw new InvalidOperationException();
        }));
        Assert.Equal("Before", projection.Text);
    }

    [Fact]
    public void CaptureIsVisibleToSubscribersAfterACommittedChange()
    {
        var session = new EditorSession(DocumentJson.FromText("Before"));
        using var projection = new SessionTextProjection(session);
        _ = projection.Text;
        string? observed = null;
        session.Changed += (_, e) => { if (e.Kind == EditorChangeKind.Document) observed = projection.Text; };
        session.InsertText("Added ");
        Assert.Equal("Added Before", observed);
    }

    [Fact]
    public void DisposedProjectionUnsubscribesAndRefusesFurtherReads()
    {
        var session = new EditorSession(DocumentJson.FromText("Before"));
        var projection = new SessionTextProjection(session);
        _ = projection.Text; projection.Dispose(); projection.Dispose();
        session.InsertText("After ");
        Assert.Throws<ObjectDisposedException>(() => projection.Text);
    }
}
