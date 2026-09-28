namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private void InitializeTableTools()
    {
        Ribbon.InsertGroup("Table Layout", 1, () => Group("Merge",
            Tool("table", "Merge Cells", "merge-cells", true), Tool("columns", "Split Cell", "split-cell", true)));
        Ribbon.InsertGroup("Table Layout", 3, () =>
        {
            var picker = new TablePicker(); picker.TableSelected += (rows, columns) => RunEdit("Nested table", () => Session.InsertTable(rows, columns));
            return Group("Cell Layout", new RibbonButton("table", "Nested Table", large: true, dropdown: true) { Flyout = picker.AsFlyout() },
                MenuButton("align-center", "Vertical Align", new OfficeMenu()
                    .Add("Align Top", () => RunEdit("Align top", () => Session.SetCellVerticalAlignment(CellVerticalAlignment.Top)))
                    .Add("Align Middle", () => RunEdit("Align middle", () => Session.SetCellVerticalAlignment(CellVerticalAlignment.Center)))
                    .Add("Align Bottom", () => RunEdit("Align bottom", () => Session.SetCellVerticalAlignment(CellVerticalAlignment.Bottom)))),
                Tool("row", "Row Options", "row-options", true));
        });
    }
    private async Task MergeCellsAsync()
    {
        var table = Session.CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        if (!Session.Selection.IsEmpty)
        {
            var index = Session.Index; var grid = new TableGrid(table);
            if (ReferenceEquals(index.At(Session.Selection.Start).Table, table)
                && ReferenceEquals(index.At(Session.Selection.End).Table, table)
                && !ReferenceEquals(grid.RegionOf(index.At(Session.Selection.Start).Paragraph).Cell, grid.RegionOf(index.At(Session.Selection.End).Paragraph).Cell))
            { Session.MergeSelectedTableCells(); return; }
        }
        var cell = Session.CurrentCell;
        var dialog = new OfficeDialog("Merge table cells", "Merge", 470);
        dialog.AddDescription("Choose a rectangular range using 1-based logical rows and columns. Text, pictures, nested tables, bookmarks and comments are retained. Existing merged cells must be selected in full.");
        var row = dialog.AddField("Start row", (cell.Row + 1).ToString()); var column = dialog.AddField("Start column", (cell.Column + 1).ToString());
        var rows = dialog.AddField("Rows to merge", Math.Min(2, table.Rows.Count - cell.Row).ToString());
        var columns = dialog.AddField("Columns to merge", Math.Min(2, table.Rows[0].Cells.Count - cell.Column).ToString());
        static int Integer(string text)
        {
            if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 1)
                throw new InvalidOperationException("Enter positive whole row and column numbers.");
            return value;
        }
        if (await ShowDialogAsync(dialog)) Session.MergeTableCells(Integer(row.Text) - 1, Integer(column.Text) - 1, Integer(rows.Text), Integer(columns.Text));
    }
    private async Task TableRowOptionsAsync()
    {
        var table = Session.CurrentTable ?? throw new InvalidOperationException("Place the cursor in a table first.");
        var cell = Session.CurrentCell; var row = table.Rows[cell.Row];
        var dialog = new OfficeDialog("Table row options", "Apply", 480);
        var height = dialog.AddField("Minimum row height (points)", N(row.MinimumHeight));
        var split = new OfficeCheckBox("Allow row to split across pages", row.AllowSplit);
        var repeat = new OfficeCheckBox("Repeat first header row", table.HeaderRow && table.RepeatHeaderRow);
        dialog.Body.Children.Add(OfficeTheme.Column(split, repeat));
        dialog.AddDescription("Options apply to the current logical row. Repeating headers are presentation copies, not duplicate text. A header spanning into a body row is not repeated. Over-height rows must split to make progress.");
        if (!await ShowDialogAsync(dialog)) return;
        var value = ParseNumber(height.Text);
        if (value is < 0 or > 4000) throw new InvalidOperationException("Minimum row height must be between 0 and 4,000 points.");
        Session.Execute("Table row options", () =>
        {
            row.MinimumHeight = value; row.AllowSplit = split.IsChecked;
            table.RepeatHeaderRow = repeat.IsChecked; if (repeat.IsChecked) table.HeaderRow = true;
        });
    }
}
