# WYSIWYG objects, tables and equations

TextSpace 0.6.0-alpha.1 adds directly editable retained objects to the same Uno/Skia paper used for body text. This is a supported feature set, not a claim of complete Microsoft Word behavior or pixel-identical typography. The authoritative deployment version is the published `build-info.json` for a successful main Build and Pages run.

## Editing on the paper

| Content | Pointer interaction | Keyboard and contextual commands |
| --- | --- | --- |
| Body text | Click/drag to position the caret or select; double-click selects a word | Existing rich-run formatting, grapheme navigation, paragraph editing and document undo/redo |
| Pictures | Select, move, eight resize handles, rotation handle; crop mode changes the source rectangle without rewriting image bytes | Arrow keys nudge; Shift changes the step; Ctrl+D duplicates; Delete removes; Picture Format exposes crop/reset and geometry |
| Shapes and text boxes | Select/move/resize/rotate; double-click or Edit Text opens a detached text editor beside the object | Shape Format exposes size, placement, fill/stroke, emphasis, alignment and duplication; Done/Ctrl+Enter applies, Escape discards |
| Equations | Select and transform the equation frame; double-click/Edit Equation opens measured editable slots | Fraction, radical, scripts, delimiters, matrix, large operators and accents; Tab/Shift+Tab and arrow navigation; local undo/redo |
| Tables | Drag internal column boundaries or row bottoms; Alt-drag a cell rectangle | Table Layout selects cells/rows/columns/table, merges a selected rectangle, splits anchors and distributes supported row/column geometry |

Objects expose explicit inline or in-front-of-text placement. Floating objects do not participate in square/tight text wrapping. Objects inside table cells remain in flow. Moving changes the selected object's local anchor offsets; cross-page reanchoring is a separate document-order operation, not arbitrary Word anchor dragging.

During a drag, the editor paints a detached `VisualEditDraft` or table preview. The document revision, AutoSave and undo history are not changed for every pointer event. Releasing a moved gesture commits one document transaction. Escape, lost capture, disposal or a conflicting document revision cancels the draft. The original document remains authoritative throughout the gesture.

Rotation-aware hit testing and handle coordinates use the same geometry helpers as the adorners. Guides and modifier constraints are presentation aids; they do not claim exact Word snapping behavior. The UI retains original vector icons and custom Uno templates rather than proprietary Office assets.

## Equation editing

`EquationEditorControl` edits a detached, validated presentation tree. A real Uno native text input is placed over the active measured leaf; it is not a hidden code-only equation replacement. Fractions, radicals, scripts, matrices, delimiters and operators have separately editable slots. Placeholder geometry is used for empty leaves.

**Tab / Shift+Tab** traverses slots. **Up / Down** selects a measured neighboring slot. Left/right at a leaf boundary moves into the adjacent slot. Native text editing still handles ordinary characters and selection within a leaf. Structural insertion snaps to grapheme boundaries, so a combining sequence or emoji is not split between nodes.

The editor's **Matrix Layout** menu inserts a row below or column to the right of the active matrix cell, or removes its row/column. Matrices are bounded to ten rows and ten columns. The final row or column cannot be removed; explicit structure deletion replaces the enclosing structure with an editable empty slot.

**Structure → Convert Structure to Linear Text** retains the visible arguments as editable linear text. **Delete Current Structure**, also Ctrl+Shift+Backspace, is an explicit deletion. These changes are undoable in the equation's local history before committing. They do not change the document history until **Done / Ctrl+Enter** applies the entire draft. **Escape / Cancel** discards it. Invalid pending leaf text blocks application instead of silently falling back to the previous valid text.

Built-in expressions include a quadratic formula, Pythagoras and mass–energy examples. They are templates, not a computer algebra system. There is no evaluator, macro execution, arbitrary LaTeX parser or automatic symbolic simplification.

### Supported mathematical structures

`EquationNode` supports Text, Row, Fraction, Radical, Superscript, Subscript, SubSuperscript, Delimited, Matrix, Nary and Accent. The shared validator enforces unique nodes and IDs, correct arities, supported symbols, Unicode-safe text, depth/character/node limits and matrix dimensions.

The layout engine emits immutable glyph, rule and slot geometry. Editor interaction, Skia display, printing and PDF/PNG export use that geometry. This implementation does not implement all OpenType MATH metrics, every Office Math construct, TeX layout, complete bidirectional mathematics or exact Cambria Math substitution. Font availability still affects appearance.

## Text inside shapes

The supported text-box model is a multiline string with a single `TextStyle`, padding and vertical alignment. It is independently editable without replacing or selecting body text. It is **not** a rich paragraph story with nested tables, fields and mixed run formatting. Imported rich shape text is normalized to the supported style and accompanied by an import note.

Shape drafts expose explicit application and cancellation. The document cannot receive unintended body typing while an object editor owns input. Changing the underlying document invalidates an outstanding draft; it is never committed over a newer revision.

## Interchange

| Format | What is preserved | Qualification |
| --- | --- | --- |
| Native `.textspace` | Visual model, source pictures and crops, shape styles/text, equation tree/slot identities, local placement and history metadata | Older TextSpace releases do not understand new visual block discriminators. Keep the original native file. |
| DOCX | Editable DrawingML/WPS shapes and text boxes; source pictures and crop rectangles; actual Office Math expression structures; rotation/flips and supported placement | Requires Office 2010-compatible drawing support. Equations are editable OMML inside a transparent Word text-box frame. This is not a raster or an opaque native JSON replacement. |
| HTML | Safe SVG shapes/pictures, escaped shape text and presentation MathML | Static browser output; browser fonts, floating placement and MathML layout are not guaranteed to match Skia pagination. |
| PDF / PNG / print | Current Skia visual appearance | Static output, not an editable Word object model. |

Standard DrawingML coordinates take precedence when an external editor changes the standard placement. A narrowly scoped drawing extension records native anchor-relative offsets and the standard export snapshot; it is used only while that snapshot still matches. It is not a substitute for standard Word geometry and does not make arbitrary third-party documents lossless.

Inline Office Math or drawings between body runs are normalized into separate editable blocks while retaining surrounding text order. Unsupported math structures retain their visible text with a warning; unsupported shape presets normalize to a rectangle with editable text and a warning. Complex fields spanning such normalization boundaries, floating wrap modes, grouped/freeform shapes, shape effects, connectors and rich drawing stories are not fully supported. Always retain original/native copies before conversion and review import notes.

## Reusable APIs

The existing ten packages remain the delivery units; no application-only engine fork was introduced.

```csharp
using TextSpace.Core;
using TextSpace.Editing;
using TextSpace.Layout;
using TextSpace.OpenXml;

var session = new EditorSession(new DocumentModel());
var root = EquationTemplates.Create("fraction", EquationNode.Leaf("a+b"));
root.Children[1].Text = "c";

// Engine-only structural manipulation returns a new tree without mutating root.
var matrix = EquationTemplates.Create("matrix");
var edit = EquationOperations.EditMatrix(
    matrix, matrix.Children[0].Id, EquationMatrixOperation.InsertColumnRight);

// Real presentation math; the receiver can edit the fraction's arguments.
var omml = OfficeMathCodec.Write(root);
var restored = OfficeMathCodec.Read(omml);

// Renderer-independent typography for a host or a headless analysis tool.
var cache = new VisualLayoutCache(new MonospaceTextMetrics());
var geometry = cache.GetEquation(new EquationBlock { Root = restored });
var leaf = geometry.HitTest(20, 10);
```

`DocumentSurface` exposes object selection, `BeginObjectEditor`, `ActiveEquationEditor`, cell selection and gesture state. `VisualEditDraft`, `VisualGeometry`, `EquationOperations`, `EquationLayout` and `VisualLayoutCache` are reusable independently of the application shell.

## Performance and ownership

Visual typography uses an LRU bounded by both entry count and estimated managed payload. Equation keys include structure, text, slot IDs and font size; shape keys include text style, story, dimensions and padding/alignment. Font-metric revisions invalidate both. Moving, rotating, flipping or recoloring an unchanged equation does not force layout; changing a shape's wrapping width does. In-place text/tree changes are checked rather than assuming that object reference equality means unchanged content.

Published cache display lists are read-only snapshots. Mutable page placement is not retained in a typography cache. Selected-object location is indexed per layout instance; hit testing gathers the current page's objects rather than scanning every page. Equation slot queries use stable, allocation-free nearest selection rather than LINQ ordering. Private equation-history snapshots are immutable by ownership, eliminating a redundant clone on each accepted leaf edit.

The byte budget is an estimate, not a process RSS or native bitmap budget. Cold long-token shaping, full-document indexing and snapshot document undo remain separate performance boundaries. These changes are not a browser FPS guarantee. See the committed visual-query benchmark harness for reproducible measurements and their scope.

## Validation

The engine suites exercise structure rules, geometry, detached editing, cache ownership/invalidation, Unicode, matrix operations, native persistence, schema-valid OMML/DrawingML, source-order preservation and safe HTML. The ninth browser suite performs actual mouse drags, keyboard input, slot editing, crop operations, table boundary edits, filechooser imports and downloads, and reload recovery. Diagnostic APIs observe state and geometry only; they cannot perform edits.

Successful native compilation does not establish exhaustive native interactive behavior. Browser acceptance is Chromium-specific; Firefox/Safari, touch-only authoring, screen readers and complete IME/bidirectional text matrices remain validation work. A successful PR run is not a completed public deployment: main Build and Pages acceptance are separate gates.
