# Browser validation and native picker interception

The browser suites exercise the published Uno application with real pointer and keyboard input. The optional `?test=1` bridge exposes read-only model and geometry snapshots. It cannot issue commands or modify a document. File imports go through the application's file input and its normal parser; exports are actual downloads.

## Keyboard-triggered file choosers

The typography suite keeps an observation-only `page.on('filechooser', ...)` listener installed before navigation. It also waits for the specific chooser around Ctrl+O, selects the exported DOCX through that chooser, and checks that exactly one chooser event occurred. The persistent observer supplies no files and invokes no application function.

Playwright 1.63 updates its underlying protocol subscriptions asynchronously when the first listener is added or the last listener is removed. Installing the first one-shot file-chooser wait immediately before raw keyboard input can race that update. Pre-arming keeps interception ready before any user interaction rather than adding an arbitrary delay or replacing the keyboard command with a direct input upload.

### Diagnosis recorded for PR #6

The failing run was Build `36404437933`, head `222d971331f65c6a631ef589cbe911ba732bc736`. Its published artifact was reused unchanged in diagnostic run `36407286652`.

The trace showed a focused native Uno textarea receiving trusted Control+O, followed synchronously by creation/click of the application's file input with active transient user activation. No page error or application modal was present, but Playwright did not deliver a chooser event. Reacquiring focus alone did not fix the failure.

Diagnostic run `36407811691` compared persistent interception with a separate experimental picker invocation change. Pre-arming the listener passed all nine original typography scenarios against the unmodified publication. The alternative experiment failed and was not adopted. This supports an interception-timing diagnosis, not a broken document-open command or proof of a general browser focus defect.

Only the test subscription is changed for this issue. Application picker code, keyboard routing, DOCX parsing, assertions and security/activation requirements are retained. The temporary diagnostic workflow and its publication modifications are not part of the product or release workflow.

## Release gates

A complete Build must pass the engine/publication tests, Windows/Linux/macOS compilation, every browser suite and package creation. Pages consumes a successful main-branch Build, verifies commit provenance, then tests the public app. An artifact uploaded by a failed Build is diagnostic material, not a release.

Native compilation is not exhaustive native interaction coverage. Chromium browser results do not establish Safari/Firefox, assistive technology, or every IME behavior. See `COMPATIBILITY.md` for the remaining product boundaries.

## Primary references

- Playwright file chooser API: https://playwright.dev/docs/api/class-filechooser
- Playwright 1.63 subscription implementation: https://github.com/microsoft/playwright/blob/v1.63.0/packages/playwright-core/src/client/channelOwner.ts
