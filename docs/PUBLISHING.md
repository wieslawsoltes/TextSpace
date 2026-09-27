# Build, validation, and GitHub Pages

The public application is the real Uno WebAssembly build at https://wieslawsoltes.github.io/TextSpace/. It is not an HTML mock-up or an embedded third-party editor.

## Publication contract

`Build` validates the engine, native desktop builds, and browser input before `Pages` can deploy a main-branch artifact. Pages checks workflow identity, branch, successful conclusion and `build-info.json` provenance, then runs the same browser acceptance against the public URL. A browser artifact attached to a failed build is diagnostic output, not approval to deploy.

The application base path is `/TextSpace/`. The publication collector and static HTTP checks verify the actual runtime module and reject HTML fallback responses. Preserve the selected Uno bootstrap/runtime fingerprinting settings when updating dependencies; the runtime module paths must agree with the generated bootstrap.

## Input regression fixed in September 2026

Managed template/layout notifications can precede native input focus. Acceptance waits for the real `uno-input` element to have the expected text and focus before sending keyboard input. It does not call a test-only editing API.

The Skia TextBox post-key handler can propose its own edit after TextSpace handles a routed command. The editor intercepts commands at PreviewKeyDown, owns the input buffer for the synchronous dispatch, cancels redundant BeforeTextChanging proposals, and restores text/selection after post-key processing. Ordinary typing and native composition remain on the native input route.

## Browser suites

`npm run test:browser` runs two independent Chromium contexts. The baseline suite covers startup, static runtime/provenance, typing, paragraph breaks, formatting, undo/redo, native download, recovery, DOCX export and compact layout. The feature suite covers bookmark creation/rename/delete/navigation, hyperlink updates, DOCX/HTML interchange, table editing, and preservation of deliberately corrupted recovery data. Mouse/keyboard input drives all feature operations; IndexedDB fault injection is used only to test failure handling.

Reports and screenshots are attached to Build and Pages runs. `?test=1` enables read-only document/control diagnostics; production sessions do not publish that diagnostics object.
