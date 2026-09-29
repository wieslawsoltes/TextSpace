# Compatibility ledger

TextSpace is an independent development preview, not Microsoft Word. Implemented workflows do not imply identical layout, complete semantics or lossless interchange.

| Area | Current implementation | Boundary |
| --- | --- | --- |
| Native documents | Rich blocks, styles, tables, pictures, comments, bookmarks, sections, fields, local tracked edits; sparse defaults and missing/null tab compatibility | Format version 1 represents TextSpace's model, not arbitrary Word structures; unrelated malformed values remain invalid |
| DOCX | Rich paragraphs, basic styles/numbering, tables, pictures, comments, bookmarks, mixed sections, inherited first/even/default plain-text stories and cached fields | Unsupported structures may be normalized or omitted with import notes; not lossless |
| Editing | Atomic transactions/notifications, bounded snapshot history, validated replacements, grapheme-safe selection/deletion and formatting | Not a persistent rope or operation-transform system |
| Native input | Synchronous model capture, deferred presentation, command ownership, key-release/flyout focus restoration and revision-bound text projection | Chromium coverage is not exhaustive IME/mobile/native parity; raw external mutations require notification/invalidation |
| Tables | Rectangular spans, split-to-grid, span-aware edits, nested table/image geometry, vertical alignment, minimum heights, splittable rows, first-header repetition and identity-based anchor remapping | No arbitrary new-grid subdivision, Word AutoFit/floating tables, cell page-break/keep semantics, multi-row/nested repeating headers or exact-height clipping; extreme objects may overflow |
| Bookmarks | Named points/ranges, create/update/rename/delete/navigation, edit tracking and atomic internal-link rename | REF/PAGEREF supported; story bookmarks and arbitrary nested field expressions unsupported |
| Links/HTML | Safe navigation, named targets, escaped font CSS, validated colors and restrictive CSP | No arbitrary external content download or active HTML import |
| Local review | Comments/replies/resolution and guarded text-change rejection | Word revisions normalize to visible text; local history is not exported as Word revisions |
| Layout | Points, margins, columns, explicit breaks, pagination, plain-text stories and shaping | Exact Word pagination, rich independent stories, notes, equations and unsupported fields remain incomplete |
| Typography | Left/center/right/decimal/bar tabs, six leaders, default interval, edge-relative stops, rich-run-aware wrapping and discretionary/nonbreaking characters | No full UAX #14/bidi, dictionary hyphenation, rich tab ruler dragging or Word locale/compatibility-mode equivalence; DOCX decimal separator defaults to period |
| Paragraph pagination | Widow/orphan, Keep lines together, measured Keep with next chains and Page break before | Main-flow paragraphs; impossible keeps degrade; mixed-object keeps remain limited |
| Performance | Bounded paragraph/glyph LRUs, indexed caret/table/viewport queries, canonical normalization, binary anchor remapping, print pagination reuse, presentation text/statistics and small-tab fast paths | Cold long-token prefix shaping, snapshot history and live index reconstruction remain; indexed layout geometry must not be mutated; benchmarks are not FPS claims |
| Sections | Mixed-size next/odd/even-page, continuous bands, compatible next-column starts, region-aware fields/rulers, numbering and inherited variants | Incompatible paper/grid promotes to a new page with a notice; shared pages use the first region's stories; paragraph-only, not mixed-object, bands balance |
| Fields | 17 allowlisted types, dependencies, sequences, F9, locks, cached rich text and single-paragraph DOCX simple/complex fields | No arbitrary external fields/formulas/IF, full date pictures, nested or cross-paragraph live fields; unsupported codes remain inert and export locked |
| Contents | Hidden bookmarks, live linked REF/PAGEREF entries and right-aligned dot leaders; rebuild via Update Table | Not an enclosing native Word TOC field; native edge-relative stops export as absolute positions |
| Drawing | In-flow raster pictures, sizing and alternative text | Floating text boxes/shapes, embedded objects and advanced drawing layout unsupported |
| Recovery | Rolling IndexedDB/native history, actionable startup Recovery Center, previewed tab repair, protected originals/downloads and earlier-snapshot restoration | Narrow repair may change layout; failed/full protection blocks replacement; bounded local archives are not independent backups; abrupt termination can lose debounced edits |
| Delivery | Exact-commit CI, real-input browser checks, ten NuGet libraries published with Trusted Publishing on version tags, single-file desktop executables, Pages validation and tagged release workflow | Desktop executables are not code-signed yet |

## Validation scope

Engine/browser reports identify the tested commit. Browser tests use real pointer/keyboard/filechooser input for editing, history, bookmarks, links, tables, fields, typography, sections, downloads and recovery. Synthetic storage fixtures cover corruption and archive-full failure. No hidden command API performs edits.

Native Windows/Linux/macOS compilation is not exhaustive native interaction. Safari, Firefox, touch, screen-reader semantics and composition/complex-script matrices require additional validation. HarfBuzz shaping alone is not a complete mixed-direction paragraph engine.

## Recovery and transport

Missing/null/empty tab collections mean no custom stops. Genuine collections above 128 remain invalid. Explicit repair retains the first 128 valid unique positions and discloses invalid/duplicate/excess removals. A readable original must be committed separately before an auto-saving repaired/replacement workbench opens. Protected originals are not evicted by rolling history; capacity or checksum failure blocks replacement.

Valid UTF-8 file originals retain BOMs and bytes. Parsing strips the marker only from a separate copy; framed source-generated JSON with byte-count/SHA-256 verification protects original transport across browser/native interop. It is not encryption or protection against an owner who clears or changes browser data. See [Recovery](RECOVERY.md), [transport](RECOVERY-TRANSPORT.md) and [validation](RECOVERY-VALIDATION.md).

`SessionTextProjection` is an owned presentation cache, not an authoritative mutable index. It invalidates on revision/model changes and document notifications and releases subscriptions on disposal. Raw external model writes require notification/invalidation. Live transaction indexing remains uncached.

## Table normalization

Native spans use a dense logical grid with content only on anchors. Overlapping spans or nonempty covered slots reject rather than silently dropping text. DOCX supports rectangular gridSpan/vMerge; malformed/nonempty continuations preserve content independently with warnings. Legacy hMerge, skipped grid positions, asymmetric margins and exact heights normalize with notes. Only one first header row repeats, and not when spanning into body rows. See [Tables](TABLES.md).

## Other unimplemented Word areas

Rich independent header/footer stories, footnotes/endnotes, equations, advanced drawings, cloud accounts/collaboration, macros, add-ins, bibliography databases, full spelling/grammar services, unsupported fields/content controls and exhaustive Word shortcut/UI parity remain unfinished. The offline Editor performs specific repetition/spacing/long-sentence checks.

## Security and preservation

Import bounds compressed/expanded package sizes, disables XML DTDs and never downloads external relationships. Macro-enabled and legacy binary Word files are unsupported. Keep original/native copies before conversion and review import notes. Device/browser owners can clear both rolling recovery and protected archives.
