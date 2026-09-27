# Changelog

## Unreleased

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
