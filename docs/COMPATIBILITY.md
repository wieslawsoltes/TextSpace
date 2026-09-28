# Compatibility ledger

TextSpace is an independent development preview, not Microsoft Word. Implemented UI workflows do not imply identical layout, complete semantics or lossless interchange.

| Area | Current implementation | Boundary |
| --- | --- | --- |
| Native documents | Rich blocks, styles, tables, pictures, comments, bookmarks, section definitions, live field ranges, local tracked edits | Native format version 1 represents TextSpace's model, not arbitrary Word structures |
| DOCX | Rich paragraphs, basic styles/numbering, tables, supported pictures, comments, bookmarks/internal anchors, mixed sections, inherited first/even/default stories, cached field instructions and results | Unsupported structures may be normalized or omitted with import notes; not lossless |
| Editing | Atomic transactions/notifications, bounded snapshot history, validated replacements, grapheme-safe selection/deletion, rich formatting | History is snapshot-based, not a persistent rope or operation-transform system |
| Native input | Synchronous model capture, deferred presentation, command ownership, key-release restoration and flyout focus restoration | Chromium regression coverage does not establish exhaustive IME/mobile/native desktop parity |
| Tables | Rectangular row/column spans, split-to-grid, span-aware row/column edits, nested table/image geometry, top/center/bottom alignment, minimum row heights, splittable rows, first-header repetition, identity-based anchor remapping | No arbitrary new-grid cell subdivision, Word auto-fit/floating tables, cell-level page-break/keep semantics, multi-row/nested repeating headers or exact-height clipping; oversized line/image cases may overflow |
| Bookmarks | Named points/ranges, update/rename/delete/navigation, edit tracking, rename updates to internal hyperlinks | REF/PAGEREF are supported; section-story bookmarks and arbitrary nested field expressions remain unsupported |
| Links and HTML | Safe internal/external navigation; HTML named targets, links, escaped font CSS, validated colors, restrictive CSP | No arbitrary external content download or active HTML import |
| Local review | Comments/replies/resolution and guarded text-change rejection | Word revision markup is normalized to visible text; local history is not exported as Word revisions |
| Layout | Points, margins, columns, explicit breaks, basic pagination, headers/footers, text shaping | Exact Word pagination, continuous sections, rich independent stories, footnotes/endnotes, equations and unsupported field types remain incomplete |
| Typography | Left/center/right/decimal/bar tabs, six leader styles, default interval, right-edge positions, rich-run-aware word wrapping, discretionary and nonbreaking characters | No full UAX #14 or bidi implementation, automatic dictionary hyphenation, rich tab ruler dragging, or Word locale/compatibility-mode equivalence; decimal separators default to period on DOCX import |
| Pagination controls | Widow/orphan control, Keep lines together, measured Keep with next paragraph chains, Page break before | Main-flow paragraphs; impossible keep constraints degrade; table row split controls are supported separately; full multi-object keep semantics are not implemented |
| Performance | Bounded paragraph/glyph LRUs, indexed caret and table-slice queries, canonical-run normalization fast path, binary structural-anchor remapping and print pagination reuse | Cold long-token prefix shaping, snapshot history and full text-index reconstruction remain; public layout intervals must not be mutated after indexing |
| Sections | Mixed-size next/odd/even-page sections, numbering restarts/formats, inherited and explicitly blank first/even/default stories, column breaks | Continuous/next-column section starts normalize to next-page with a warning; stories are plain text, not rich independent document trees |
| Fields | 17 allowlisted field types, dependency evaluation, sequence counters, F9 updates, locks, cached rich text, single-paragraph DOCX simple/complex fields | No arbitrary external fields, formulas, IF expressions, full Word date-picture switches, nested or cross-paragraph live fields; unsupported codes remain inert and export locked |
| Contents | Generated hidden bookmarks plus live linked REF/PAGEREF entries and right-aligned dot leaders; Update Table rebuilds headings | Not a native enclosing Word TOC field; native edge-relative TOC stops become absolute on DOCX export |
| Drawing objects | In-flow raster pictures, sizing and alternative text | Floating text boxes/shapes, embedded objects and advanced drawing layout are not implemented |
| Recovery | IndexedDB and atomic desktop files, bounded snapshots; corrupt startup recovery is not replaced by sample content | Debounced edits can be lost on abrupt termination; recovery is not a file backup |
| Delivery | Exact-commit CI gates, real-input browser checks, ten packable libraries, Pages validation and tagged releases | Package creation is not nuget.org publication; NuGet upload is explicitly opt-in |

## Validation scope

Engine tests and browser reports record the tested commit. The browser suites exercise real pointer and keyboard input, including rapid physical typing, Unicode deletion, history, bookmarks, links, merged/nested tables, repeated headers, downloads, recovery, sections, fields, contents and deliberate recovery corruption. Tests do not mutate application state through a test-only command API; storage fault injection is limited to failure testing.

Native Windows/Linux/macOS compilation does not establish interactive behavior on those operating systems. Safari, Firefox, touch devices, screen-reader document semantics and the full composition/complex-script matrix require further validation. HarfBuzz shaping alone does not provide a complete mixed-direction paragraph layout engine.

## Other unimplemented Word areas

Cloud accounts and collaboration, macros, add-ins, bibliography databases, full spelling dictionaries/grammar services, unsupported field types and content controls, complete drawing objects, and exhaustive Word shortcut/UI parity are not implemented. The offline Editor panel performs specific repetition, spacing, punctuation-spacing and long-sentence checks only.

## Table interchange normalization

Native spans use a dense logical grid with text stored only on anchors. Invalid overlapping spans or nonempty covered slots are rejected rather than silently dropping content. DOCX supports rectangular `gridSpan`/`vMerge` structures; malformed or nonempty continuation content is retained independently with warnings. Legacy `hMerge`, skipped grid positions, asymmetric cell margins and exact row heights are normalized with import notes. Only one first header row repeats, and not if it spans into a body row. See [Tables](TABLES.md).

## Security and preservation

Import limits compressed and expanded package sizes, disables XML DTDs and never downloads external relationships. Macro-enabled and legacy binary Word files are unsupported. Keep original and native file copies before converting complex documents, and review import notes. Browser persistence can be cleared by the browser or device owner.
