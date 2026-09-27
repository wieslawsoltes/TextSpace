namespace TextSpace.Core;

/// <summary>
/// A live field over a single paragraph's cached result. Instructions are inert data;
/// only the explicitly supported field evaluator may interpret them.
/// Manual edits inside the cached result unlink the field, retaining the edited text.
/// </summary>
public sealed class DocumentField
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Start { get; set; }
    public int End { get; set; }
    public string Instruction { get; set; } = "TITLE";
    public bool Locked { get; set; }
}

public readonly record struct FieldPageInfo(int PageNumber, int PageCount, int SectionNumber, int SectionPages, PageNumberStyle NumberStyle);
