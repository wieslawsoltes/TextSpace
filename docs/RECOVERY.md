# Native recovery and Open and Repair

TextSpace 0.5.1 adds an actionable recovery center instead of a reload-only startup error. It specifically handles the native tab-metadata failure reported in `DocumentJson.ValidateTypography -> DocumentJson.Load -> App.OnLaunched` without silently clearing user storage.

## Legacy null arrays and strict limits

A missing or explicit JSON `null` tab-stop array means no custom stops and loads as an empty immutable array. A closed converter supplies this behavior with source-generated TabStop metadata, including the trimmed browser publication. Actual arrays exceeding 128 entries remain invalid. The decoder rejects excess elements before allocating their tail; validation still checks finite geometry, supported enums and distinct position/edge pairs at twip precision. An uninitialized model array now has a distinct diagnostic rather than the misleading count-limit message.

The submitted stack trace alone cannot distinguish the former `IsDefault` case from an actual oversized collection. Regression coverage therefore includes both cases; no undocumented diagnosis of the user's unseen native document is asserted.

## Startup choices

When saved native recovery cannot be loaded, the recovery center offers:

- **Download original recovery:** export the original JSON without trying to parse or repair it.
- **Preview tab-stop repair:** inspect a proposal; no recovery storage is changed by previewing.
- **Protect original and open repaired copy:** after a successful preview, commit the unchanged original into protected storage, then open a new document identity and save the repaired copy.
- **Protect original and start blank:** explicitly preserve the unreadable document before starting another one.
- **Show previous recovery versions:** choose a valid rolling snapshot. The current unreadable original is protected before the older document opens.
- **Retry saved workspace:** reload without clearing or overwriting recovery.

An unreadable storage backend cannot be overwritten through these actions: replacement requires a readable original and successful protection. If protection fails, including quota exhaustion, the current saved recovery remains unchanged. Font/runtime startup failures are not mislabeled as repairable document corruption.

## What tab repair changes

Repair is an explicit native-JSON operation, not an automatic import coercion. It traverses paragraphs, including nested tables, and proposes removal of invalid, duplicate or excess tab entries. It keeps the first 128 valid unique stops in source order. Duplicate identity includes both position and relative edge; rounding is the same twip identity used by strict validation.

The preview reports paragraph identifiers and retained, invalid, duplicate and excess counts. Removing tab stops can change layout. Text, comments, bookmarks, fields and other JSON properties are not edited by the repair transform. The resulting document must pass the complete existing validator: unrelated structural corruption is not guessed at or silently discarded. Non-array tab metadata is rejected because its meaning cannot be inferred. A plan is bounded by the file-byte, block/depth and 100,000-examined-stop limits.

`DocumentRecovery.PrepareTabRepair` returns an immutable plan and performs no I/O. Applications should review the plan, protect the source with `IRecoveryArchiveStore.ProtectAsync`, and only then adopt `plan.CreateDocument()`.

## Recovery ribbon

The application adds **Recovery > Open and Repair** for a selected `.textspace` file, and **Protected Originals** for downloading previous originals. The file workflow does not modify the source file, and ordinary unsaved-document confirmation is retained. DOCX already has its separate, warning-based importer; this repair does not claim arbitrary DOCX salvage.

Embedding applications can use `RecoveryWorkspaceHost` to compose any existing `IWorkspaceHost` with an `IRecoveryArchiveStore`, then call `WordWorkbench.EnableRecoveryTools()`.

## Protected originals

Protected originals are separate from the twelve-entry rolling AutoSave history. Their identifier is the SHA-256 digest of the UTF-8 original; reads verify it. Repeated protection of identical content is idempotent. Originals are not automatically evicted. The bounded store accepts at most 16 originals, 32 MB per original and 64 MB total; a full store refuses replacement instead of removing earlier data.

Browser originals and their metadata are written in one IndexedDB transaction under separate `workspace` keys. No database-version upgrade is required. Listing reads metadata only. Desktop originals use atomic, non-overwriting file publication under `TextSpace/ProtectedOriginals`, flushed file contents and a cross-process publication lock.

Protection preserves the native JSON text available to the application, not a damaged file's original byte encoding. Download important originals: browser storage may still be cleared by its owner or the browser, device files can be lost, and protected storage is not an off-device backup. This version provides downloads but no automatic original deletion.

## Performance and validation

Tab validation returns immediately for empty collections and uses stack storage for up to eight stops; larger validated collections use a pre-sized hash set. There is no per-paragraph HashSet allocation on the common empty-tab path. Regression tests measure thread-managed allocations after warmup rather than claiming a browser frame-rate improvement.

The eighth Chromium suite uses real pointer/keyboard input, file choosers and downloads. Direct storage writes are limited to deliberate persisted-input and failure fixtures (null/oversized tabs, malformed JSON, full archives, older snapshots). The diagnostic recovery-state bridge is read-only and never contains the original document payload. No test-only editing API is exposed.

This change does not implement full Word parity. Rich independent stories, notes, equations, floating objects, advanced table cases, full revision/field semantics, complex-script input/accessibility, collaboration and add-ins remain tracked in the compatibility ledger.
