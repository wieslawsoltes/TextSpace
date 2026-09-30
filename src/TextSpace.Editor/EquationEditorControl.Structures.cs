using TextSpace.Core;

namespace TextSpace.Editor;

public sealed partial class EquationEditorControl
{
    private void AddStructureCommands(StackPanel toolbar)
    {
        var matrix = new OfficeMenu()
            .Add("Insert Matrix Row Below", () => EditMatrix(EquationMatrixOperation.InsertRowBelow))
            .Add("Insert Matrix Column Right", () => EditMatrix(EquationMatrixOperation.InsertColumnRight))
            .Separator().Add("Delete Matrix Row", () => EditMatrix(EquationMatrixOperation.DeleteRow))
            .Add("Delete Matrix Column", () => EditMatrix(EquationMatrixOperation.DeleteColumn));
        var matrixButton = new OfficeButton { Content = "Matrix Layout", FontSize = 11, Padding = new(6, 4), Height = 28, Flyout = matrix.AsFlyout() };
        AutomationProperties.SetName(matrixButton, "Matrix layout"); toolbar.Children.Add(matrixButton);
        var structure = new OfficeMenu().Add("Convert Structure to Linear Text", () => EditStructure(EquationOperations.ConvertStructureToText))
            .Add("Delete Current Structure", () => EditStructure(EquationOperations.DeleteStructure));
        var structureButton = new OfficeButton { Content = "Structure", FontSize = 11, Padding = new(6, 4), Height = 28, Flyout = structure.AsFlyout() };
        AutomationProperties.SetName(structureButton, "Equation structure operations"); toolbar.Children.Add(structureButton);
    }
    private void EditMatrix(EquationMatrixOperation operation) => EditStructure((root, slot) => EquationOperations.EditMatrix(root, slot, operation));
    private void EditStructure(Func<EquationNode, string, EquationEditResult> operation)
    {
        try
        {
            if (!CaptureInput() || _active is null) return;
            var result = operation(_root, _active); Remember(); _root = result.Root; _active = result.ActiveSlotId;
            Rebuild(); FocusSlot(); DraftChanged?.Invoke();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }
}
