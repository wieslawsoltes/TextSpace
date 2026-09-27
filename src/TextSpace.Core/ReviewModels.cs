namespace TextSpace.Core;

public sealed class CommentThread
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Start { get; set; }
    public int End { get; set; }
    public string Author { get; set; } = "You";
    public string Text { get; set; } = "";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public bool Resolved { get; set; }
    public List<string> Replies { get; set; } = [];
}

/// <summary>A local tracked text edit. Overlapping later edits make individual rejection unsafe.</summary>
public sealed class TrackedEdit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Start { get; set; }
    public string Removed { get; set; } = "";
    public string Inserted { get; set; } = "";
    public string Author { get; set; } = "You";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public bool CanReject { get; set; } = true;
}
