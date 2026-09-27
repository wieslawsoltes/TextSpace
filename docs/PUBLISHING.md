# Build, validation, and GitHub Pages

The public application is the real Uno WebAssembly build at https://wieslawsoltes.github.io/TextSpace/. It is not an HTML mock-up or an embedded third-party editor.

## Publication contract

`Build` validates the engine, native desktop builds, browser input and ten reusable packages before `Pages` can deploy a main-branch artifact. Pages checks workflow identity, branch, successful conclusion and `build-info.json` provenance, then runs the browser acceptance suites against the public URL. A browser artifact attached to a failed build is diagnostic output, not approval to deploy.

The application base path is `/TextSpace/`. The publication collector and static HTTP checks verify the actual runtime module and reject HTML fallback responses. Preserve the selected Uno bootstrap/runtime fingerprinting settings when updating dependencies; the runtime module paths must agree with the generated bootstrap.

## Native input ownership

Managed template/layout notifications can precede native input focus. Acceptance waits for the real `uno-input` element to have the expected text and focus before sending keyboard input. It does not call a test-only editing API.

The Skia TextBox post-key handler can propose its own edit after TextSpace handles a routed command. The editor intercepts document commands at PreviewKeyDown, owns the input buffer during that dispatch, cancels redundant BeforeTextChanging proposals, and restores the selection on key release or deferred post-key completion. Ordinary character keys are not synchronized from stale document snapshots.

Native TextChanging commits incoming text synchronously; asynchronous TextChanged is retained as an idempotent fallback. Programmatic synchronization is guarded to avoid reentrant document edits. This prevents rapid character bursts or a later focus update from discarding text that has not reached an asynchronous notification yet. Flyout commands restore editor focus only after open popups finish their own focus restoration.

## Browser suites

`npm run test:browser` runs three isolated Chromium suites. All suites execute to retain diagnostics, but a failure in any suite fails the build.

- Baseline: startup, static runtime/provenance, typing, paragraph breaks, formatting, undo/redo, native download, recovery, DOCX export and compact layout.
- Keyboard: zero-delay physical character bursts, typing immediately after handled commands, single-application deletion, selection deletion, emoji grapheme deletion, and independent document undo/redo.
- Document features: bookmark creation/rename/delete/navigation, hyperlink updates, DOCX/HTML interchange, table editing and preservation of deliberately corrupted recovery data.

Mouse/keyboard input drives feature operations. IndexedDB fault injection is used only to test failure handling. Reports and screenshots are attached to Build and Pages runs. `?test=1` enables read-only document/control diagnostics; production sessions do not publish that diagnostics object.

These checks do not establish exhaustive IME, native desktop interaction, assistive-technology, Safari, Firefox or mobile parity.
