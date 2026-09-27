# Compatibility and validation boundaries

Native `.textspace` is the most complete format for the current model. TextSpace is not a complete Word implementation; disabled commands and import warnings are intentional boundaries.

| Area | Remaining boundary |
| --- | --- |
| Visual fidelity | Original office-style controls; no pixel-identical Word baseline has been established |
| Typography | HarfBuzz and explicit substitutes; exact Aptos/Calibri metrics and Word pagination are not guaranteed |
| Unicode and IME | Grapheme editing is tested; mixed-direction layout and comprehensive IME composition remain incomplete |
| Tables | Basic cells and row/column edits; merged cells, nested layout, repeated headers and advanced sizing are simplified |
| Review | Local text revisions; full Word revision markup and formatting-preserving rejection are not implemented |
| Sections | Multiple DOCX sections normalize to one settings model; separate first/even/odd and per-section header stories are absent |
| References | Editable contents/caption text; full fields, notes, bibliography and maintained cross-references remain absent |
| Drawing objects | In-flow raster pictures; floating shapes, text boxes, equations and wrapping are absent |
| Accessibility | Named keyboard-operable controls; a complete document text automation provider and assistive-technology matrix are not validated |
| Collaboration | No multiplayer, cloud sync, Microsoft identity, macro or add-in runtime |
| Comparison | Paragraph-text based; not yet an ordered, formatting-aware document diff |
| Recovery | Debounced local saves may lose newest edits on abrupt shutdown; download independent file copies |

DOCX import accepts a bounded transitional WordprocessingML subset. DTDs are prohibited and external relationships are not downloaded. Legacy binary .doc and macro-enabled formats are unsupported. These checks are not a comprehensive hostile-document fuzzing claim.

Save a native copy before conversion. DOCX retains supported visible content, not every local review detail. PDF preserves generated appearance, not full editability or a complete tagged-accessibility structure.

For evidence, inspect the Build/Pages run for the exact commit. Unit tests are not complete interoperability certification. Native compilation is not native interaction coverage. Chromium acceptance does not establish Safari/Firefox/mobile/IME parity. Report deployment as verified only after artifact provenance and public browser checks succeed.
