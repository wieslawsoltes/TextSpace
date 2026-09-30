# Compatibility ledger

TextSpace is an independent development preview, not Microsoft Word. Implemented workflows do not imply identical layout, complete semantics or lossless interchange. Source version 0.6.0-alpha.1 adds retained objects and structural equations; the live version is the last successfully deployed build, not the version in this document.

| Area | Current implementation | Boundary |
| --- | --- | --- |
| Native documents | Rich blocks, styles, tables, pictures/crops, shapes, equation trees, comments, bookmarks, sections, fields and local tracked edits; sparse defaults and missing/null tab compatibility | Format version 1 represents TextSpace's model, not arbitrary Word structures; older releases cannot read new block types; malformed values remain invalid |
| DOCX | Rich paragraphs, basic styles/numbering, tables, pictures, supported editable DrawingML shapes and OMML equations, comments, bookmarks, mixed sections, inherited plain-text stories and cached fields | Unsupported structures may be normalized or omitted with import notes; not lossless; inline drawings/math become separate ordered blocks |
| Editing | Atomic transactions/notifications, bounded snapshot history, validated replacements, grapheme-safe selection/deletion, formatting and detached visual drafts | Not a persistent rope or operation-transform system |
| Native input | Synchronous model capture, deferred presentation, command ownership, key-release/flyout focus restoration and revision-bound text projection; object selection blocks body text edits | Chromium coverage is not exhaustive IME/mobile/native parity; native textarea flags alone do not establish managed TextBox state |
| Tables | Rectangular spans, split-to-grid, span-aware edits, nested geometry, vertical alignment, minimum heights, splittable rows, first-header repetition, boundary drags and Alt-drag cell rectangles | No arbitrary new-grid subdivision, Word AutoFit/floating tables, cell page-break/keep semantics, multi-row/nested repeating headers or exact-height clipping; extreme objects may overflow |
| Bookmarks | Named points/ranges, create/update/rename/delete/navigation, edit tracking and atomic internal-link rename | REF/PAGEREF supported; story bookmarks and arbitrary nested field expressions unsupported |
| Links/HTML | Safe navigation, named targets, escaped font CSS, validated colors, restrictive CSP, SVG objects and presentation MathML | Static browser layout is not Skia pagination; no active HTML import or arbitrary external content download |
| Local review | Comments/replies/resolution and guarded text-change rejection | Word revisions normalize to visible text; local history is not exported as Word revisions |
| Layout | Points, margins, columns, explicit breaks, pagination, plain-text stories, shaping and supported visual blocks | Exact Word pagination, rich independent stories, notes and unsupported fields remain incomplete |
| Typography | Left/center/right/decimal/bar tabs, six leaders, default interval, edge-relative stops, rich-run-aware wrapping and discretionary/nonbreaking characters | No full UAX #14/bidi, dictionary hyphenation, rich tab ruler dragging or Word locale/compatibility-mode equivalence; DOCX decimal separator defaults to period |
| Paragraph pagination | Widow/orphan, Keep lines together, measured Keep with next chains and Page break before | Main-flow paragraphs; impossible keeps degrade; mixed-object keeps remain limited |
| Performance | Bounded paragraph/glyph/visual LRUs, indexed caret/table/viewport/object queries, canonical normalization, binary anchor remapping, print pagination reuse and cached statistics | Cold long-token prefix shaping, snapshot history and live index reconstruction remain; indexed geometry must not be mutated; warm-cache microbenchmarks are not FPS claims |
| Sections | Mixed-size next/odd/even-page, continuous bands, compatible next-column starts, region-aware fields/rulers, numbering and inherited variants | Incompatible paper/grid promotes to a new page with a notice; shared pages use the first region's stories; paragraph-only, not mixed-object, bands balance |
| Fields | 17 allowlisted types, dependencies, sequences, F9, locks, cached rich text and single-paragraph DOCX simple/complex fields | No arbitrary external fields/formulas/IF, full date pictures, nested or cross-paragraph live fields; unsupported codes remain inert and export locked |
| Contents | Hidden bookmarks, live linked REF/PAGEREF entries and right-aligned dot leaders; rebuild via Update Table | Not an enclosing native Word TOC field; native edge-relative stops export as absolute positions |
| Drawing | Pictures, nondestructive crop, supported vector shapes/text boxes, eight resize handles, rotation/flips, duplication, nudge and inline/in-front placement | No tight/square wrapping, grouped/freeform shapes, connectors, effects, rich shape stories, arbitrary cross-page reanchoring or embedded objects |
| Equations | Editable leaf slots, fractions, radicals, scripts, matrices, delimiters, n-ary operators and accents; matrix row/column edits, local history, linear conversion, OMML and MathML | Presentation mathematics only; no CAS, arbitrary LaTeX parser, full OpenType MATH typography or complete Office Math coverage; matrices bounded to 10 by 10 |
| Recovery | Rolling IndexedDB/native history, startup Recovery Center, previewed tab repair, protected originals/downloads and earlier-snapshot restoration | Narrow repair may change layout; failed/full protection blocks replacement; local archives are not independent backups; abrupt termination can lose debounced edits |
| Delivery | Exact-commit CI, real-input browser checks, ten NuGet libraries, single-file desktop executables, Pages validation and tagged release workflow | Desktop executables are not code-signed; source commits do not publish packages or create a release |

## Validation scope

Engine/browser reports identify the tested commit. Browser tests use real pointer, keyboard and filechooser input for editing, objects, equations, tables, history, bookmarks, fields, typography, sections, downloads and recovery. Synthetic storage fixtures cover corruption and archive-full failure. Diagnostic APIs observe state; no hidden command API performs edits.

Native Windows/Linux/macOS compilation is not exhaustive native interaction. Safari, Firefox, touch, screen-reader semantics and composition/complex-script matrices require additional validation. HarfBuzz shaping alone is not a complete mixed-direction paragraph engine. See [WYSIWYG objects](WYSIWYG-OBJECTS.md) for interaction and API details.

## Recovery and transport

Missing/null/empty tab collections mean no custom stops. Genuine collections above 128 remain invalid. Explicit repair retains the first 128 valid unique positions and discloses invalid/duplicate/excess removals. A readable original must be committed separately before an auto-saving repaired/replacement workbench opens. Protected originals are not evicted by rolling history; capacity or checksum failure blocks replacement.

Valid UTF-8 file originals retain BOMs and bytes. Parsing strips the marker only from a separate copy; framed source-generated JSON with byte-count/SHA-256 verification protects original transport across browser/native interop. It is not encryption or protection against an owner who clears or changes browser data. See [Recovery](RECOVERY.md), [transport](RECOVERY-TRANSPORT.md) and [validation](RECOVERY-VALIDATION.md).

`SessionTextProjection` is an owned presentation cache, not an authoritative mutable index. It invalidates on revision/model changes and document notifications and releases subscriptions on disposal. Raw external model writes require notification/invalidation. Live transaction indexing remains uncached.

## Drawing and equation normalization

DOCX writes standard DrawingML/WPS objects and editable Office Math inside transparent text-box frames. A narrow extension preserves native anchor-relative offsets only while standard position, size, floating mode and coordinate frame still match the export snapshot. Changed standard placement takes precedence. Unsupported coordinate systems generate a normalization warning; they do not silently become exact Word equivalents.

Supported math stays structural. Unsupported math retains visible text with a warning. Shape text currently has one character style; mixed formatting is normalized. Inline math/drawings split the surrounding paragraph into ordered editable blocks. Complex fields crossing those boundaries, Word wrap rules and rich drawing stories remain limitations.

## Table normalization

Native spans use a dense logical grid with content only on anchors. Overlapping spans or nonempty covered slots reject rather than silently dropping text. DOCX supports rectangular gridSpan/vMerge; malformed/nonempty continuations preserve content independently with warnings. Legacy hMerge, skipped grid positions, asymmetric margins and exact heights normalize with notes. Only one first header row repeats, and not when spanning into body rows. See [Tables](TABLES.md).

## Other unimplemented Word areas

Rich independent header/footer stories, footnotes/endnotes, advanced drawings and mathematics, cloud accounts/collaboration, macros, add-ins, bibliography databases, full spelling/grammar services, unsupported fields/content controls and exhaustive Word shortcut/UI parity remain unfinished. The offline Editor performs specific repetition/spacing/long-sentence checks.

## Security and preservation

Import bounds compressed/expanded package sizes, disables XML DTDs and never downloads external relationships. Macro-enabled and legacy binary Word files are unsupported. Keep original/native copies before conversion and review import notes. Device/browser owners can clear both rolling recovery and protected archives. Visual text validation rejects malformed Unicode and XML-invalid characters before serialization.
