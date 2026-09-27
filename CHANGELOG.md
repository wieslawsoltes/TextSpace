# Changelog

## Unreleased

### Editing correctness

- Collapsed/reversed selections respect complete Unicode graphemes.
- Replacement validates its original range before snapping and rejects overflow.
- Compound edits notify only after commit or full rollback.
- Undo/redo history is bounded by count and estimated snapshot payload size.
- Read-only mode covers collapsed-caret formatting; invalid font sizes are rejected.
- Case conversion retains rich runs, hyperlinks and selection direction.
- Replacing a selection with a structure is a single undo unit.
- Table changes preserve review anchors by paragraph identity.
- Added row-above, column-left/right and delete-column APIs.

### Delivery

- Recovered actual Uno app hosts that had not reached main.
- Added engine/native/browser validation and commit-verified Pages delivery.
- Pinned licensed font sources and repaired Tinos license acquisition.
- Added static runtime delivery checks and trimming-safe diagnostics.
- Added professional architecture/compatibility documentation and verified-artifact release packaging.

This changelog records source changes; pending builds and deployment results must be checked separately.
