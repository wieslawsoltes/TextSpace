namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private TextBox? _objectNameEditor;
    private OfficeButton? _objectRenameButton;
    private string? _objectNameOwnerId, _objectNameSnapshot;

    private void AddObjectNameEditor(OfficeTaskPane pane)
    {
        _objectNameOwnerId = _objectNameSnapshot = null;
        var field = OfficeTheme.Field("Object name", "");
        _objectNameEditor = field;
        field.MaxLength = VisualNameRules.MaximumLength;
        field.PlaceholderText = "Name the selected object";
        _objectRenameButton = new OfficeButton("Rename selected", () =>
        {
            if (!CanRenameSelectedObject() || Surface.SelectedObjectId is not { } id || id != _objectNameOwnerId) return;
            var name = field.Text;
            RunEdit("Rename object", () => Session.RenameVisual(id, name));
            RefreshObjectSelectionPane();
            Surface.FocusEditor();
        });
        field.TextChanged += (_, _) => UpdateObjectRenameButton();
        pane.Body.Children.Add(field);
        pane.Body.Children.Add(_objectRenameButton);
    }

    private bool CanRenameSelectedObject() => !Session.IsReadOnly && !Surface.IsObjectEditorOpen
        && !Surface.IsVisualGestureActive && Surface.SelectedObject is not null;

    private void UpdateObjectRenameButton()
    {
        if (_objectRenameButton is not null)
            _objectRenameButton.IsEnabled = CanRenameSelectedObject() && _objectNameEditor is { } field
                && VisualNameRules.IsValid(field.Text) && field.Text != Surface.SelectedObject!.Name;
    }

    private void RefreshObjectNameEditor()
    {
        if (_objectNameEditor is not { } field) return;
        var selected = Surface.SelectedObject;
        // Do not overwrite a name being typed on selection-only redraws. An
        // actual selection change, undo or external model update refreshes it.
        if (_objectNameOwnerId != selected?.Id || _objectNameSnapshot != selected?.Name)
        {
            _objectNameOwnerId = selected?.Id;
            _objectNameSnapshot = selected?.Name;
            field.Text = selected?.Name ?? "";
        }
        field.IsEnabled = CanRenameSelectedObject();
        UpdateObjectRenameButton();
    }
}
