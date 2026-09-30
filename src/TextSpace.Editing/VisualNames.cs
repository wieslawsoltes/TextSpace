using TextSpace.Core;

namespace TextSpace.Editing;

public sealed partial class EditorSession
{
    /// <summary>Renames one visual atomically without replacing its text, geometry, ID or image bytes.</summary>
    public void RenameVisual(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(name);
        EnsureWritable();
        if (!VisualNameRules.IsValid(name))
            throw new ArgumentException($"Object names must be single-line Unicode text of at most {VisualNameRules.MaximumLength} UTF-16 code units.", nameof(name));
        var visual = FindVisual(id) ?? throw new InvalidOperationException("The object no longer exists.");
        if (visual.Name == name) return;
        // Metadata-only mutation deliberately avoids CloneVisual's extra image
        // byte-array copy. Execute still supplies the normal bounded undo snapshot.
        Execute("Rename object", () => visual.Name = name);
    }
}
