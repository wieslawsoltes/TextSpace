# Object selection and crop geometry

## Selection pane

Open **Selection Pane** from Home, View, Shape Format, Picture Format or Equation. With the paper focused, **Alt+F10** opens the same task pane. Each row identifies the object type, its first physical page and an excerpt of its text or alternative text. Selecting a row reveals that object even when another object covers it. **Edit selected** opens the existing detached shape-text or equation editor; Apply and Cancel retain their normal transaction semantics.

**Tab / Shift+Tab** cycles objects while an object is selected on the paper. Ordinary text/table Tab behavior is unchanged when no object is selected. Navigation does not change document text, formatting, geometry or undo history. Open object drafts and active drag gestures are not discarded by Next/Previous Object commands.

The pane includes pictures, shapes and equations in nested table cells, but excludes repeated-header presentation replicas. Multi-page occurrences of one object have one navigation row, referring to its first canonical placement. Search covers the optional object name, type, page, full text/alternative text and ID. The rendered result list has 100 rows per page with explicit Previous/Next result controls; all matches remain accessible. Search scans the object metadata when its filter or layout changes, not on every pointer movement. Selection-only updates reuse existing rows. Closing or displacing the pane releases object-location references, including source image data retained by the old document.

This is single-object navigation, not full Word Selection Pane parity. It does not introduce grouping, multi-selection, show/hide flags, separate stacking commands or drag-reordering. Existing Move Before/After commands change document flow and are not presented as z-order controls. The next/previous sequence follows canonical layout order, not a new z-order model.

Object names are edited explicitly using **Object name** and **Rename selected**. They are separate from visible shape text and picture alternative text. See [Object names](OBJECT-NAMES.md) for undo, Unicode limits and interchange behavior.

## One hit-test policy

`VisualObjectIndex` captures per-page object candidates and an ordinal ID-to-location map for a particular `DocumentLayout`. Pointer selection and double-click use the same policy: floating objects are examined before inline objects, and each layer is examined in reverse painting order. This corrects the earlier mismatch where double-click could edit an inline object covered by a floating object.

Queries reuse their indexed arrays, allocate no result collections, and do not walk unrelated pages. Rotated objects are tested in their local frame. Rebuild the index after changing layout; do not treat it as an automatic observer of an externally mutated layout. This is a CPU query/allocation improvement, not a measured browser frame-rate claim.

`VisualObjectIndexCache` creates the index lazily for a single layout owner. A null-selection paint query does not construct an object index. When layout changes, even null-selection lookups release the old index without eagerly scanning the replacement document. Explicit navigation, hit testing or opening the Selection Pane requests the index. Regression tests check this ownership and the zero-allocation warmed lookup path.

## Flipped picture cropping

`VisualCropGeometry.Drag` transforms the pointer delta out of the picture rotation, then maps each visible handle to its unflipped source edge. A visible left-edge drag on a horizontally flipped picture changes the source **right** crop; a visible top-edge drag on a vertically flipped picture changes the source **bottom** crop. Combined flips and arbitrary finite rotations use the same calculation. The image bytes are never resampled or rewritten.

The helper validates incoming geometry and crop fractions before calculation. Existing near-limit imported crops cannot produce a negative clamp maximum. Cropping remains a detached gesture followed by one document transaction, with existing undo, native persistence and DOCX crop export.

## Validation scope

Engine regressions cover overlap painter order, rotation, replica exclusion, navigation wrap, split-object identity, image fallback, immutable result lists, lazy cache ownership, crop flips/rotation, near-limit source rectangles and invalid queries. The additional browser suite uses real mouse, keyboard, filechooser imports and downloads. Its larger object documents are generated as ordinary native-file fixtures and opened through the application; diagnostics are read-only.

Crop acceptance waits for the requested transform to reach the diagnostic geometry before using a handle. It then verifies actual pointer capture, an unchanged revision during the draft, exactly one edit on release, unchanged frame placement/size, correct source-edge changes and byte-identical image data. This prevents a stale pre-rotation coordinate from accidentally testing a move gesture. Recovery acceptance similarly waits for an arranged viewport before acquiring real paper input focus after reload.

All prior suites remain required, including startup recovery protection. Successful CI is scoped to its exact commit. Native builds establish compilation and browser interactions are Chromium-specific. No full Word UI, mobile/touch, bidi or accessibility parity is implied.
