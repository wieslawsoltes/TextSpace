# Object names

## Selection Pane workflow

Open **Selection Pane**, select an object, enter a label in **Object name**, and choose **Rename selected**. A name can identify a picture, shape or equation without changing its content. Name search is case-insensitive and works alongside type, page, full content and ID search. Objects inside supported nested tables use the same names and selection mechanism.

Typing in the name field is a local UI draft, not a document edit. Rename applies one document transaction. An unchanged name creates no undo entry and preserves redo history. Empty names restore the type/content fallback in the list. Switching to another object discards an uncommitted field value; ordinary selection-only refreshes retain it. Undo/redo updates the field from the actual model. Renaming is disabled in read-only sessions and while an object-text/equation draft or pointer gesture is active.

Names need not be unique. `VisualBlock.Id`, not `Name`, remains the identity used for selection and commands. Duplication retains the label but creates a different ID; changing the copy's name leaves the original unchanged.

## Reusable API

```csharp
session.RenameVisual(objectId, "Pump P-101 🧪");
session.Undo();
session.Redo();
```

`VisualBlock.Name` is an optional string, defaulting to empty. `VisualNameRules` validates single-line Unicode labels bounded to 256 UTF-16 code units. Controls, paragraph/line separators and malformed surrogate pairs are rejected. The bound is in UTF-16 units, not grapheme clusters. The document-wide character budget includes object names. Existing native documents that omit the property remain loadable.

`RenameVisual` mutates only the name within the existing snapshot transaction machinery. It preserves the visual instance, ID, geometry, shape text, equation tree and image byte-array reference. It avoids the additional image-array clone performed by general visual replacement; normal bounded document-history snapshots still apply. A rename changes the revision, invalidating any stale `VisualEditDraft` held by an external caller.

Shape and equation measurement-cache keys do not contain names, so unchanged typography can be reused through the normal repagination path. This is not a claim of zero-allocation renaming, constant-time document history, GPU acceleration or measured browser frame-rate improvement.

## Persistence and interchange

Native save/open, clone, undo/redo and local recovery retain names through the ordinary generated JSON model. Invalid native names are rejected rather than silently modified. This change does not disable strict validation, AutoSave recovery protection or original-document retention.

DOCX writes the object label to the standard `wp:docPr/@name`, and picture labels additionally to `pic:cNvPr/@name`. Picture alternative text remains in `descr` and is not replaced by the selection label. The reader uses the standard name, so an external editor's rename is authoritative. For unnamed objects the writer supplies a type-plus-ID label; reopening that DOCX therefore produces that generated label rather than the empty native value. Inline math forms without drawing properties have no drawing-object name to import.

External drawing names that exceed the bound or contain unsupported control/separator characters are normalized to a bounded single-line label and reported in import warnings. Truncation does not split a Unicode scalar; it is not a promise to preserve an entire extended grapheme cluster at the cutoff. Retain the source DOCX when exact external metadata is required. Export/reimport of explicit valid names is covered for inline and floating pictures, shapes and equation drawings.

HTML keeps an escaped `data-textspace-name` attribute on each visual wrapper. It does not replace visible content or accessible descriptions with the internal selection name. PDF/PNG remain visual outputs, not lossless metadata round-trip formats.

Standard-property reference: [Open XML DocProperties documentation](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.drawing.wordprocessing.docproperties). `Name` is explicitly non-unique and separate from `Description`.

## Validation and remaining scope

Engine tests cover all visual types, atomic history, no-op edits, read-only and missing targets, stale drafts, nested native storage, legacy documents, scalar boundaries, invalid names, copy independence, standard-name precedence, import warnings, HTML escaping and typography-cache retention. The real-input Chromium suite covers the name field, explicit commit, search, undo/redo, clearing, draft isolation, structural equations, source picture bytes, file pickers, DOCX export/reopen and local recovery.

Names do not introduce show/hide flags, grouping, multi-selection, independent stacking controls or full Word Selection Pane parity. Native CI builds establish compilation; exhaustive desktop input and assistive-technology validation remain separate work.
