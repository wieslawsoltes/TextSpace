# Changelog

## 0.5.0-alpha.1 — Continuous sections and interaction performance

- Preserve continuous and next-column section kinds through native/DOCX editing and interchange; change starts with atomic section settings.
- Add physical-page section regions, independent numbering/field context, column-aware rulers, explicit layout notices and bounded paragraph-band balancing.
- Keep page, table, image, repeated-header and parity behavior under shared-region flow with progress and ownership tests.
- Reuse named HTML pages for same-paper continuous flow without a forced page break.
- Query visible page ranges logarithmically and reuse revision-scoped workbench statistics/ruler data; avoid materialized word matches.
- Add engine/interchange/fuzz tests, a seventh real-input Chromium suite and reproducible query microbenchmarks with capture costs. Full Word parity is not claimed.

## 0.4.0-alpha.1 — Merged tables and structural editing

- Add validated rectangular cell spans, identity-preserving merges and split-to-grid, span-aware row/column edits and merged-cell navigation.
- Measure nested tables and in-cell pictures at their own widths; support vertical alignment, minimum row heights, row splitting and presentation-only first-header repetition.
- Read/write DOCX gridSpan/vMerge, table row controls and vertical alignment; emit real HTML rowspan/colspan. Preserve malformed continuation content with import notes.
- Add custom Uno Merge Cells, Split Cell, Nested Table, Vertical Align and Row Options workflows.
- Avoid re-allocating canonical paragraph runs during validation; index structural anchor remapping and table page-slice interval queries. Add a reproducible editing benchmark with raw timings and allocations.
- Add topology, nested layout, interchange, randomized structural/history regression tests and a sixth real-input browser suite. This remains a documented Word-compatibility subset.

## 0.3.0-alpha.1 — Typography and performance

- Complete the previously uncommitted performance work: bounded paragraph-layout LRU, font-version invalidation, caret interval index, allocation-reduced hit testing, and shared pagination for printing/PNG export.
- Add bounded native glyph-blob reuse with independent resource ownership and raster-equivalence tests.
- Add left/center/right/decimal/bar tab stops, leaders, default intervals, right-edge anchoring, and complete set/clear dialogs.
- Add widow/orphan, keep-lines, keep-next and page-break-before controls with measured paragraph-chain pagination and over-height progress guarantees.
- Preserve word-break behavior across rich-run formatting boundaries; support discretionary hyphens, nonbreaking spaces/hyphens and zero-width breaks.
- Generate live contents with right-aligned dot leaders; preserve supported typography in native files and standard WordprocessingML.
- Add typography/interop, cache-invalidation and pagination regression tests plus real-input browser scenarios. Full Word compatibility remains explicitly out of scope for this release.


## 0.2.0-alpha.1 — Sections and fields

- Added mixed-paper next/odd/even-page sections and explicit column breaks with nonprinting boundary markers.
- Added current-section page setup, numbering restart/continuation and decimal/Roman/alphabetic display formats.
- Added nullable inherited versus explicitly blank first/even/default header and footer variants, plain-text wrapping and page/section/metadata placeholders.
- Added 17 supported live field types, bounded dependency and sequence evaluation, update/lock/unlink/edit UI, and F9.
- Replaced static contents entries with live linked bookmark-text and page-reference fields, built as one undo unit.
- Added schema-validated DOCX sections, story references, simple/complex single-paragraph fields, and cached-result preservation.
- Updated variable-size scrolling, hit testing, ruler, previews, PDF, PNG, print CSS, HTML sections, and section-relative table/picture sizing.
- Preserved unaffected live fields and remapped bookmarks during CSV mail merge.
- Expanded engine regression and real-input browser acceptance coverage. See the workflow for results of the exact commit.

## 0.1.0-alpha.1 — Publishing and editor reliability

### Added
- Named points/ranges with create, move, rename, delete and navigation UI.
- Bookmark-aware editing, Unicode boundary validation and table structural remapping.
- Native and DOCX bookmark round-tripping, actual OOXML bookmark markers and HTML anchor links.
- Safe internal/external hyperlink navigation and atomic link updates on bookmark rename.
- Lazy ribbon group insertion API and bindable office-button color overrides.
- Browser feature acceptance for bookmarks, links, tables and corrupted recovery.
- Ten reusable NuGet package artifacts from successful Build runs.

### Fixed
- GitHub Pages deployment blockers caused by native input readiness and duplicate TextBox post-key edits.
- Double paragraph breaks and competing native/document editing for handled command keys.
- Clipped third-row ribbon commands.
- Corrupt startup recovery silently falling back to sample content.
- HTML link handling, CSS font-string escaping, combined text decoration and unsafe color input.
- Collapsed/reversed grapheme selections, formatting no-ops and atomic notification boundaries.
- Table insertion/deletion and review-anchor movement through structural edits.

### Changed
- TextIndex uses binary paragraph lookup, identity lookup and cached grapheme boundaries per snapshot.
- Snapshot undo history is bounded by configurable entry and byte limits.
- Pages verifies artifact provenance and runs real-input checks against the deployed public application.

## 0.1.0-alpha.1

Initial Uno/Skia document engine and office-style workbench with ten reusable libraries, native/browser hosts, rich editing, pagination, tables, images, comments, local review, native/DOCX interchange, PDF/PNG/HTML export, mail merge and local recovery.
