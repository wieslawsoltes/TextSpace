# Merged and nested tables

TextSpace 0.4.0-alpha.1 implements a rectangular logical table grid with merged cells, recursive in-cell table/image layout and editable row policies. This is a supported subset, not identical Word table layout.

## Workbench

Place the caret in a table to expose **Table Design** and **Table Layout**. Use **Merge Cells** on a selection spanning two cells, or enter 1-based row, column, height and width in its dialog. The merge rectangle must contain every intersected merged cell in full. **Split Cell** restores the original logical slots covered by a merge; all existing content stays in the top-left cell and new cells are empty. It does not create arbitrary new subdivisions in an unmerged cell.

**Nested Table** inserts an editable table in the current cell. Tab/Shift+Tab visit anchor cells once, skipping covered slots. At the last cell, Tab appends a row. Top/middle/bottom **Vertical Align** uses the extra height available inside a cell.

**Row Options** edits the current logical row's minimum height and split policy. Repeating the first header row applies to the table. Row/column insertion extends spans crossing the insertion boundary. Deleting an anchor row/column retains the cell's content if part of that span survives; deleting a cell's entire extent removes its content under ordinary document undo.

## Reusable model and editor

`TableCell.RowSpan` and `ColumnSpan` are positive grid extents from a top-left anchor. `TableRow.Cells` remains a dense, equal-width logical grid for existing API consumers. Slots covered by another cell have unit spans and **empty `Blocks`**, not duplicate paragraph objects. `TableGrid` validates this topology and supplies O(1) coordinate-to-anchor lookup.

```csharp
var table = TableBlock.Create(4, 3);
var document = new DocumentModel { Blocks = [table, new Paragraph()] };
var editor = new EditorSession(document);
editor.MergeTableCells(row: 0, column: 0, rowCount: 2, columnCount: 2);
editor.SetCellVerticalAlignment(CellVerticalAlignment.Center);

var grid = new TableGrid(table);
var region = grid.At(1, 1); // Same top-left anchor as At(0, 0).
editor.SplitTableCell();   // Content stays in that anchor; original slots return.
editor.Undo();             // Restores the merged topology and selection.
```

A `TableGrid` is a topology snapshot: reconstruct it after mutation. Cells, blocks and runs must have independent mutable ownership. Covered slots with content, overlapping spans, aliased cells and out-of-grid rectangles are errors, not opportunities to silently discard text. The import/editor limits remain 200 rows, 20 columns and bounded nesting.

Merge moves the existing blocks and paragraph identities in logical row-major order. Bookmarks, comments, live field ranges and selection follow surviving identities rather than a fuzzy text diff. Any field whose paragraph or unchanged result range no longer survives is unlinked safely. Invalid edits roll back atomically. Merging an arbitrary rectangle changes text-index order; cross-cell range anchors collapse rather than being reversed if their endpoints reorder.

## Measured geometry and pagination

Each anchor cell is measured at the sum of its spanned column widths. Nested tables recursively receive the inner width and retain their own borders, paragraphs and images. Row-height constraints account for spanning cells; remaining height produces vertical-alignment offsets. Paragraph measurement reuses the existing bounded cache, while placement owns independent geometry.

Rows marked splittable use remaining column space. Nonsplittable rows or connected vertical-span groups move to the next column if they fit there. Over-height groups split at measured line/image boundaries to make progress. The cell frame is sliced without drawing a false horizontal border at a continuation boundary. Mixed-size lines in adjacent cells are assigned once, not duplicated or omitted at the cut.

Only a first header row that does not vertically span into a body row can repeat. Repeated header lines have `IsReplica = true`: they render and can navigate back to the original text, but do not duplicate the native text or change field/caret lookup to the last repeated copy. Header repetition is suppressed when it would prevent body progress. Header styling and the repeat flag are separate native settings.

## Interchange

DOCX emits `w:gridSpan`, `w:vMerge` restart/continuation, `w:vAlign`, `w:cantSplit`, `w:trHeight` with `atLeast`, and `w:tblHeader`. Continuation cells contain an empty required paragraph. Nested table widths and tab-stop export use the containing cell's available width. Open XML SDK schema tests validate merged and nested output. Native files preserve the complete supported span/row model; HTML uses actual `rowspan`/`colspan` and vertical alignment.

The DOCX reader counts grid extents before allocation and bounds recursive table reading. A merge continuation with meaningful content is retained as an independent cell with a warning. Unmatched continuations, legacy `hMerge`, skipped logical slots, extra header rows, exact heights and asymmetric margins have documented normalization notes rather than claims of lossless preservation.

The underlying OOXML semantics are documented by Microsoft in the Open XML SDK references for [GridSpan](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.gridspan), [VerticalMerge](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.verticalmerge), and [TableHeader](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.tableheader).

## Boundaries and validation

Not implemented: Word AutoFit's content negotiation, floating table wrap/positioning, arbitrary cell subdivisions that create new grid lines, exact-height clipping, multi-row or nested-table header repetition, cell-level forced page breaks, full nested-row keep semantics, separate per-edge cell margins/borders, and full Word compatibility-mode behavior. A line taller than a printable column must overflow or be clipped; the engine guarantees progress, not impossible geometry. Extreme narrow columns and deep nesting can also exceed a cell's practical minimum text width.

Regression tests cover spans, deletion/insertion across spans, canonical text uniqueness, merge/split undo, deterministic structural fuzzing, large anchor remaps, nested geometry/images, mixed-font/tall-row pagination, header replicas, malformed continuation preservation, and DOCX schema/round-trip validation. The browser suite exercises real controls and downloads; its opt-in diagnostic bridge only reads state. Engine rendering tests do not establish exhaustive native UI, screen-reader, bidi or mobile interaction parity.
