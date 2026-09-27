using TextSpace.Core;
using TextSpace.Documents;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    private string? _updatingField;
    public DocumentField? CurrentField => Document.Fields.FirstOrDefault(f => Selection.Active >= f.Start && Selection.Active < f.End)
        ?? Document.Fields.LastOrDefault(f => Selection.IsEmpty && f.End == Selection.Active);

    public string InsertField(string instruction, Func<DocumentModel, FieldContext>? contextFactory = null)
    {
        var parsed = FieldInstruction.Parse(instruction);
        if (!parsed.Supported) throw new ArgumentException("This instruction is preserved on import but cannot be created or executed by this field engine.", nameof(instruction));
        var id = Guid.NewGuid().ToString("N");
        Execute("Insert field", () =>
        {
            var start = Selection.Start; InsertText("\u200B");
            Document.Fields.Add(new() { Id = id, Start = start, End = start + 1, Instruction = instruction.Trim() });
            UpdateFields(contextFactory);
            var field = Document.Fields.Single(f => f.Id == id); Selection = new(field.End, field.End);
        });
        return id;
    }

    public void SetFieldInstruction(string id, string instruction)
    {
        var parsed = FieldInstruction.Parse(instruction);
        if (!parsed.Supported) throw new ArgumentException("Unsupported field instruction.", nameof(instruction));
        Execute("Edit field", () =>
        {
            var field = FindField(id); FieldInstruction? previous = null;
            try { previous = FieldInstruction.Parse(field.Instruction); } catch (FormatException) { }
            if (previous?.Hyperlink == true && !parsed.Hyperlink)
            {
                var selection = Selection; var typing = TypingStyle;
                Selection = new(field.Start, field.End);
                FormatText("Unlink field hyperlink", style => style.Hyperlink == "#" + previous.Argument ? style with { Hyperlink = null } : style);
                Selection = selection; TypingStyle = typing;
            }
            field.Instruction = instruction.Trim();
        });
    }
    public void LockField(string id, bool locked) => Execute(locked ? "Lock field" : "Unlock field", () => FindField(id).Locked = locked);
    public void UnlinkField(string id) => Execute("Unlink field", () => Document.Fields.Remove(FindField(id)));
    private DocumentField FindField(string id) => Document.Fields.FirstOrDefault(f => f.Id == id) ?? throw new InvalidOperationException("The field no longer exists.");

    /// <summary>Updates dependent cached results and pagination to a fixed point, or atomically rolls back after eight passes.</summary>
    public IReadOnlyList<FieldResult> UpdateFields(Func<DocumentModel, FieldContext>? contextFactory = null)
    {
        IReadOnlyList<FieldResult> results = [];
        Execute("Update fields", () =>
        {
            var selection = Selection; var typing = TypingStyle; var tracking = TrackChanges; TrackChanges = false;
            var now = DateTimeOffset.Now;
            try
            {
                for (var pass = 0; pass < 8; pass++)
                {
                    var context = (contextFactory?.Invoke(Document) ?? new FieldContext()) with { Now = now };
                    results = FieldEngine.Evaluate(Document, context); var changed = false;
                    foreach (var result in results.Where(r => r.Evaluated).OrderByDescending(r => FindField(r.Id).Start))
                    {
                        var field = FindField(result.Id); var before = Index.Text.Substring(field.Start, field.End - field.Start);
                        var parsed = FieldInstruction.Parse(field.Instruction);
                        if (before == result.Value)
                        {
                            if (parsed.Hyperlink)
                            {
                                Selection = new(field.Start, field.End);
                                FormatText("Field hyperlink", style => style with { Hyperlink = "#" + parsed.Argument, Underline = true });
                            }
                            continue;
                        }
                        var start = field.Start; var end = field.End; var length = result.Value.Length;
                        int Map(int position) => position <= start ? position : position >= end ? position + length - (end - start) : start + Math.Min(position - start, length);
                        selection = new(Map(selection.Anchor), Map(selection.Active));
                        TypingStyle = Index.At(start).Paragraph.StyleAt(start - Index.At(start).Start);
                        if (parsed.Hyperlink) TypingStyle = TypingStyle with { Hyperlink = "#" + parsed.Argument, Underline = true };
                        _updatingField = field.Id;
                        Replace(start, end - start, result.Value, "Update field");
                        field.Start = start; field.End = start + length;
                        _updatingField = null; changed = true;
                    }
                    if (!changed) return;
                }
                throw new InvalidOperationException("Field results and pagination did not converge after eight passes. The update was rolled back.");
            }
            finally
            {
                _updatingField = null; TrackChanges = tracking; TypingStyle = typing;
                Selection = new(Index.Snap(selection.Anchor), Index.Snap(selection.Active));
            }
        });
        return results;
    }

    private void TransformFieldAnchors(int start, int removed, int inserted)
    {
        var end = start + removed; var delta = inserted - removed;
        foreach (var field in Document.Fields.ToArray())
        {
            if (field.Id == _updatingField) continue;
            // Typing at either boundary is outside the field. Editing the interior converts the result to ordinary text.
            if (removed > 0 && start < field.End && end > field.Start || removed == 0 && start > field.Start && start < field.End)
                Document.Fields.Remove(field);
            else if (field.Start >= end) { field.Start += delta; field.End += delta; }
        }
    }
}
