# Compatibility ledger

TextSpace is an independent development preview, not Microsoft Word. Implemented UI workflows do not imply identical layout, complete semantics or lossless interchange.

| Area | Current implementation | Boundary |
| --- | --- | --- |
| Native documents | Rich blocks, styles, tables, pictures, comments, bookmarks, local tracked edits | Native format version 1 represents TextSpace's model, not arbitrary Word structures |
| DOCX | Rich paragraphs, basic styles/numbering, tables, supported pictures, comments, bookmarks/internal anchors, repeated header/footer text, page settings | Unsupported structures may be normalized or omitted with import notes; not lossless |
| Editing | Atomic transactions/notifications, bounded snapshot history, validated replacements, grapheme-safe selection/deletion, rich formatting | History is snapshot-based, not a persistent rope or operation-transform system |
| Native input | Synchronous model capture, deferred presentation, command ownership, key-release restoration and flyout focus restoration | Chromium regression coverage does not establish exhaustive IME/mobile/native desktop parity |
| Tables | Cell editing/navigation, row and column insertion/deletion, rectangular normalization, identity-based review/bookmark remapping | Merged cells, advanced nested-table pagination and full Word table layout remain incomplete |
| Bookmarks | Named points/ranges, update/rename/delete/navigation, edit tracking, rename updates to internal hyperlinks | No complete Word cross-reference field engine or section-story bookmark support |
| Links and HTML | Safe internal/external navigation; HTML named targets, links, escaped font CSS, validated colors, restrictive CSP | No arbitrary external content download or active HTML import |
| Local review | Comments/replies/resolution and guarded text-change rejection | Word revision markup is normalized to visible text; local history is not exported as Word revisions |
| Layout | Points, margins, columns, explicit breaks, basic pagination, headers/footers, text shaping | Exact Word pagination, full section stories, footnotes/endnotes, equations and advanced fields remain incomplete |
| Drawing objects | In-flow raster pictures, sizing and alternative text | Floating text boxes/shapes, embedded objects and advanced drawing layout are not implemented |
| Recovery | IndexedDB and atomic desktop files, bounded snapshots; corrupt startup recovery is not replaced by sample content | Debounced edits can be lost on abrupt termination; recovery is not a file backup |
| Delivery | Exact-commit CI gates, real-input browser checks, ten packable libraries, Pages validation and tagged releases | Package creation is not nuget.org publication; NuGet upload is explicitly opt-in |

## Validation scope

Engine tests and browser reports record the tested commit. The browser suites exercise real pointer and keyboard input, including rapid physical typing, Unicode deletion, history, bookmarks, links, tables, downloads, recovery and deliberate recovery corruption. Tests do not mutate application state through a test-only command API; storage fault injection is limited to failure testing.

Native Windows/Linux/macOS compilation does not establish interactive behavior on those operating systems. Safari, Firefox, touch devices, screen-reader document semantics and the full composition/complex-script matrix require further validation. HarfBuzz shaping alone does not provide a complete mixed-direction paragraph layout engine.

## Other unimplemented Word areas

Cloud accounts and collaboration, macros, add-ins, bibliography databases, full spelling dictionaries/grammar services, advanced fields and content controls, complete drawing objects, and exhaustive Word shortcut/UI parity are not implemented. The offline Editor panel performs specific repetition, spacing, punctuation-spacing and long-sentence checks only.

## Security and preservation

Import limits compressed and expanded package sizes, disables XML DTDs and never downloads external relationships. Macro-enabled and legacy binary Word files are unsupported. Keep original and native file copies before converting complex documents, and review import notes. Browser persistence can be cleared by the browser or device owner.
