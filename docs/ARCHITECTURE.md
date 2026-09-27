# Architecture and contracts

## Boundaries

Core has no renderer or UI dependency. Documents adds serialization and helpers. Editing uses validated native snapshots for transactions. Layout consumes only Core and `ITextMetrics`; Skia implements metrics and paints its geometry. OpenXml handles OPC packages. Storage supplies recovery contracts. Controls and Editor are Uno libraries. Workbench assembles the application and consumes `IWorkspaceHost`; App implements browser and native services.

## Geometry and identities

Model geometry uses typographic points; the editor converts at 96/72 multiplied by zoom. Persistent paragraph IDs survive structural changes. Selections use UTF-16 offsets into flattened paragraph text separated by structural newlines. Carets remain on full graphemes, and replacement validates its original range before snapping either endpoint.

## Transactions

`EditorSession.Execute` is synchronous and single-writer. Nested commands form one outer undo unit. Observers see only committed or fully restored state, never partial compound edits. Subscriber exceptions after commit do not roll back committed state. Undo/redo and rollback may replace model objects: re-read Session.Document and resolve persistent IDs afterwards.

History is bounded by snapshot count and estimated UTF-16 JSON payload size. This is not a total process-memory cap. A snapshot larger than the configured budget is not retained. No-op edits preserve redo history.

Structural edits remap comments, revisions and selection through surviving paragraph identities. Local offsets are retained within surviving paragraphs. Anchors inside removed cells collapse to the next surviving paragraph or the previous end. A tracked edit whose mapped range no longer matches its inserted text is marked unsafe to reject.

## Rendering and input

ParagraphLayouter emits line/chunk geometry and caret positions through ITextMetrics. PageLayoutEngine flows paragraphs, breaks, tables and pictures into pages/columns. DocumentRenderer uses the same geometry for on-screen pages and PDF/PNG. The editor culls offscreen pages and uses an Uno text-input bridge for typing.

This is not yet Word's layout engine: continuous sections, mixed-direction text, hyphenation, floating-object wrapping and independent document stories remain incomplete. Matching layout requires matching font files, not merely matching family names.

## Hosting and verification

IWorkspaceHost abstracts file selection/download, clipboard, printing, external URL launch and recovery. Browser persistence uses IndexedDB; desktop recovery atomically replaces files. DOCX import bounds package sizes, disables DTDs and does not fetch external relationships.

The browser is tested with real mouse/keyboard input. Opt-in diagnostics expose read-only model summaries and control geometry, using Utf8JsonWriter rather than trimmed anonymous-type reflection. There is no test-only document mutation endpoint.

## Sections and field evaluation

`SectionBreakBlock` starts a new section without contributing text coordinates. The first section retains the version-1 root Page/Header/Footer properties; later sections contain page settings and nullable story variants. Null inherits and an empty string is explicitly blank. Layout resolves these values once per section and records a physical page index separately from the displayed page number. `DocumentLayout` caches cumulative page tops and centers pages in the maximum paper width, so hit testing, scrolling and exports do not assume uniform heights.

`DocumentField` references a non-overlapping, grapheme-aligned cached-text range in one paragraph. Instructions are data, not executable code. `FieldEngine` evaluates an explicit allowlist with bounded dependency recursion. `EditorSession.UpdateFields` computes and replaces results from right to left while remapping anchors, preserving selection and typing attributes, and suppressing tracked-text noise. It repeats with fresh layout until results stabilize; eight unsuccessful passes roll the entire update back. Unknown instructions retain cached text; DOCX export locks them. Native files remain the preservation format.

A live contents block is a transaction combining generated bookmark targets, styled paragraphs, and REF/PAGEREF fields. It is exported as standard linked fields, not as a claim to implement the entire Word TOC field grammar.
