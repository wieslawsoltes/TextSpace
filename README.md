<div align="center">

# TextSpace

### A writing room built with Uno Platform and SkiaSharp

Rich text · Paginated paper · Familiar office workflows · Reusable .NET components

[![Build](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml)
[![Pages](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

[Open the browser app](https://wieslawsoltes.github.io/TextSpace/) · [Architecture](docs/ARCHITECTURE.md) · [Sections & fields](docs/SECTIONS-AND-FIELDS.md) · [Continuous sections](docs/CONTINUOUS-SECTIONS.md) · [Typography](docs/TYPOGRAPHY.md) · [Performance](docs/PERFORMANCE.md) · [Bookmarks](docs/BOOKMARKS.md) · [Publishing](docs/PUBLISHING.md) · [Compatibility](docs/COMPATIBILITY.md)

</div>

---

TextSpace is an independent, local-first word processor implemented in **C#**, **Uno Platform**, **SkiaSharp**, and **HarfBuzz**. Its ribbon, editing surface, navigation panes, review tools and file workflows are assembled from reusable libraries—not an embedded web editor or a screenshot of another application.

> **Development preview: 0.5.0-alpha.1.** This is not Microsoft Word. Complete feature parity, identical pagination, pixel-identical appearance and lossless DOCX round-tripping are not implemented. Keep file copies of important documents. See the compatibility ledger before using interchange formats.

## The workspace

The custom ribbon groups commands into Home, Insert, Design, Layout, References, Mailings, Review, View and Help. Contextual table and picture tabs expose object-specific workflows. File backstage provides templates, file operations and local version history.

| Area | Implemented scope |
| :--- | :--- |
| Editing | Rich runs, paragraph splitting/joining, grapheme-safe selection, atomic transactions, bounded undo/redo and native input integration |
| Typography | Font metadata, size, emphasis, super/subscript, colors, highlighting, styles, paragraph spacing and alignment; aligned tab stops and leaders; discretionary/nonbreaking hyphens |
| Layout | Mixed-size sections, next/odd/even-page and column breaks, per-section numbering, linked first/even/default headers and footers; widow/orphan and keep-paragraph controls |
| Fields and contents | Live metadata, dates, sequences, bookmark/page references, F9 update, field locks, and linked table-of-contents entries |
| Tables and pictures | Rectangular merged cells, split-to-grid, nested tables, repeated headers, vertical alignment, row pagination, row/column edits, in-cell/in-flow pictures |
| Navigation | Heading outline, page previews, search/replace, bookmarks, internal hyperlinks and safe external link navigation |
| Review | Anchored comments/replies, resolved threads, local tracked-text edits and guarded rejection |
| Files | Native `.textspace`, bounded DOCX interchange, PDF/PNG rendering, HTML/plain-text export |
| Mail merge | CSV recipients, `«field»` substitution, preview and bounded DOCX batch export |
| Recovery | Browser IndexedDB, atomic desktop files, bounded snapshot history and preservation of failed recovery bytes |

The document is rendered by SkiaSharp. Edits pass through a UI-independent session; Uno supplies the native text-input bridge. Unsupported commands are disabled rather than showing simulated results.

### Bookmarks and links

Select a range and choose **Insert → Navigation → Bookmark**. Add, move, rename, delete, or navigate named ranges. Bookmarks follow text and table edits; undo restores their state. Renaming updates matching internal links atomically. Use an address such as `#TechnicalDetails` in the Link dialog, then **Open Link** or Control/Command-click the rendered link.

Bookmarks are preserved in native files and exported as real WordprocessingML bookmark markers in DOCX. HTML output contains named targets, safe links, escaped font-family CSS, validated colors and a restrictive content-security policy. [Read the bookmark contract](docs/BOOKMARKS.md).

### Continuous sections and responsive navigation

Continuous and next-column section starts now retain their native and DOCX semantics. Same-paper sections can share a page with independent column geometry, region-relative fields and rulers, and constrained paragraph-only column balancing. Section start changes are undoable through Section Settings. Physical-paper/grid incompatibilities produce layout notices rather than silently rewriting the requested break kind. See [Continuous sections](docs/CONTINUOUS-SECTIONS.md) for header/footer ownership and balancing limits.

Repaints query the visible page interval instead of scanning every page. Workbench word counts, selection statistics and column rulers reuse explicit snapshots until the document changes; no hidden cache assumes the publicly mutable document model is immutable. The [performance report](docs/PERFORMANCE.md) includes capture costs, raw measurements and limitations.

### Sections, fields, and live contents

Use **Layout → Section Break** to separate portrait, landscape, or differently sized parts of the same document. Page Setup applies to the current section. **Section Settings** controls numbering restarts, Roman/alphabetic formats, first/even-page variants, and links to previous headers/footers. The editor, ruler, previews, PDF, and PNG share the same variable-size page geometry.

**Insert → Field** creates live cached results such as `TITLE`, `PAGE`, `SEQ Figure`, `REF Details \h`, or `PAGEREF Details \h`. **F9** updates results and pagination atomically; **Manage Fields** edits instructions, locks/unlocks, navigates, or unlinks results. Changing text inside a field converts it to ordinary editable text.

**References → Table of Contents** now creates linked `REF`/`PAGEREF` entries. F9 refreshes their text and pages; Update Table rebuilds the heading list. DOCX preserves supported field instructions and section properties rather than flattening every field and section. [Detailed API, workflow, and interoperability contract](docs/SECTIONS-AND-FIELDS.md).

### Typography and pagination

**Layout → Tabs** configures left, center, right, decimal and bar stops with dot, hyphen, line, heavy-line or middle-dot leaders. Set or clear stops, then Apply to selected paragraphs. Right-edge stops follow the current column width. The document-wide default interval is editable. **Layout → Pagination** exposes widow/orphan control, Keep with next, Keep lines together, and Page break before; the Paragraph dialog exposes the same flags.

Generated live contents now use right-aligned dot leaders, without inserting dot characters into the document text. **Insert → Special Characters** inserts tabs, nonbreaking spaces/hyphens, optional hyphens and zero-width break opportunities. Formatting boundaries inside words no longer create artificial line-break opportunities. Optional hyphens become visible only when used for a break. [Typography and interoperability details](docs/TYPOGRAPHY.md).

The renderer now reuses bounded paragraph geometry and native shaped-text blobs, indexes caret intervals, and shares existing pagination with PNG export/printing. Benchmarks, raw results, invalidation rules, and remaining hot paths are documented in [Performance engineering](docs/PERFORMANCE.md). Engine microbenchmarks are not browser frame-rate guarantees.

### Merged and nested tables

**Table Layout → Merge Cells** merges a selected logical rectangle (or opens explicit row/column bounds). **Split Cell** restores the covered grid slots while retaining all content in the top-left cell. Editing and Tab navigation skip covered slots rather than duplicating their text. Row/column insertion and deletion adjust crossing spans and remap bookmarks, comments and live fields through surviving paragraph identities.

**Nested Table**, **Vertical Align**, and **Row Options** expose recursive table editing, top/middle/bottom alignment, minimum row heights, row splitting and repeated first-header-row rendering. DOCX exports real `gridSpan`/`vMerge` cells; HTML exports actual row/column spans. [Table model, editing API, layout and normalization contract](docs/TABLES.md).

## Ten reusable libraries

| Library | Responsibility |
| :--- | :--- |
| `TextSpace.Core` | Rich document model, typography, blocks, tables, images, review/bookmark anchors and text indexing |
| `TextSpace.Documents` | Native serialization/validation, templates, safe HTML output, CSV merge and offline writing checks |
| `TextSpace.Editing` | Atomic editing, selections, rich formatting, structure, bounded history, bookmarks and review |
| `TextSpace.Layout` | Renderer-independent line geometry, pagination, caret positioning and hit testing |
| `TextSpace.Skia` | HarfBuzz metrics, font registration, page rendering, PDF and PNG output |
| `TextSpace.OpenXml` | Bounded DOCX package import/export; Open XML SDK is a validation-only test dependency |
| `TextSpace.Storage` | Recovery contracts and atomic-file implementation |
| `TextSpace.Controls` | Extensible ribbon, galleries, menus, icons, palettes, dialogs, panes and scrolling |
| `TextSpace.Editor` | Embeddable paginated editor, ruler, native input bridge and previews |
| `TextSpace.Workbench` | Composable office shell, commands, backstage, navigation and review workflows |

`TextSpace.App` contains browser and native desktop hosts. Successful Build runs attach ten NuGet packages under **TextSpace-packages**. Building packages does not automatically publish them to nuget.org.

### Engine embedding

```csharp
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.OpenXml;
using TextSpace.Skia;

var document = DocumentJson.FromText("Introduction\nDetails", "Example");
var editor = new EditorSession(document);
editor.SetSelection(13, 20);
editor.SetBookmark("Details");
editor.SetSelection(0, 12);
editor.FormatText("Link", style => style with
{
    Hyperlink = "#Details",
    Underline = true
});
File.WriteAllBytes("Example.docx", new DocxWriter().Write(document));

using var renderer = new DocumentRenderer();
// Register the required font faces before layout for predictable typography.
var layout = renderer.Layout(document);
File.WriteAllBytes("Example.pdf", renderer.ExportPdf(document, layout));
```

### Uno embedding

```csharp
var session = new EditorSession(document);
var surface = new TextSpace.Editor.DocumentSurface(session);
var workbench = new TextSpace.Workbench.WordWorkbench(session, workspaceHost);
```

`IWorkspaceHost` supplies file picking/saving, clipboard, printing, URI launching and recovery. Dispose the surface or workbench when its owner closes. The UI libraries do not own your platform services.

Ribbon hosts can insert their own lazy groups with `RibbonBar.InsertGroup(tabName, index, factory)`. Office button rest colors and foreground overrides are dependency properties, so XAML bindings and runtime updates refresh immediately.

## Build and run

The repository pins .NET SDK **10.0.401** and Uno SDK **6.7.30** in `global.json`. The renderer uses Uno-compatible SkiaSharp **3.119.2**. Do not independently upgrade native Skia binaries without checking the selected Uno renderer's ABI.

```sh
git clone https://github.com/wieslawsoltes/TextSpace.git
cd TextSpace
python3 scripts/fetch-assets.py

dotnet test tests/TextSpace.Tests -c Release

dotnet run --project src/TextSpace.App \
  -f net10.0-desktop -p:TextSpaceDesktopOnly=true
```

Browser publication:

```sh
dotnet workload install wasm-tools
dotnet publish src/TextSpace.App -f net10.0-browserwasm -c Release \
  -p:WasmShellWebAppBasePath=/TextSpace/ -o artifacts/publish
python3 scripts/collect-site.py artifacts/publish site
python3 scripts/serve-site.py site --port 4173
```

Open `http://127.0.0.1:4173/TextSpace/`. Browser builds require HTTP/HTTPS; opening `index.html` with `file://` is not supported.

## Validation and delivery

**Build** runs the engine suite, native desktop builds on Windows/Linux/macOS, real-input Chromium acceptance, and NuGet packaging. **Pages** deploys only a successful main-branch Build artifact after checking its source SHA, then repeats acceptance against the public URL. Runtime-module HTTP preflight rejects broken asset paths and HTML fallback responses.

Tests cover atomic notifications, rollback, bounded history, selection boundaries, table structural anchors, Unicode graphemes, bookmark transforms, DOCX schema validation and round-tripping, HTML link/CSS handling, and malicious XML rejection. Browser suites use actual mouse/keyboard events and read-only diagnostics, including explicit native-input readiness. Deliberate storage fault injection tests that failed recovery is not overwritten.

The tagged **Release** workflow validates source, builds browser and NuGet artifacts, writes checksums, and creates a GitHub release. NuGet publication is an explicit workflow choice requiring the configured secret; neither a package build nor a workflow file claims a public NuGet release.

## Compatibility boundary

**Native `.textspace` is the complete editable format for TextSpace's model.** DOCX remains an interchange subset. Import limits compressed and expanded package sizes, disables XML DTDs, and never downloads external relationships.

Word revision markup is normalized to visible text; local revision history is retained in native files but not exported as Word revisions. Multiple sections use one page-settings model, floating pictures become in-flow blocks, and merged/nested tables have simplified layout. Footnotes, endnotes, equations, advanced fields, floating shapes/text boxes, citation databases, embedded objects, collaboration, macros and add-ins remain outside the implemented scope.

HarfBuzz shapes text, but exhaustive mixed-direction layout, complex-script caret behavior, IME combinations, screen-reader document semantics and exact Word line/page breaking remain unverified or incomplete. The offline Editor checks repetition, spacing and long sentences; it is not a full spelling or AI grammar service.

[Detailed compatibility ledger](docs/COMPATIBILITY.md) · [Publishing and input architecture](docs/PUBLISHING.md)

## Privacy, recovery and fonts

Documents are processed locally. No account, analytics, document upload or AI service is required. AutoSave writes to this device, not cloud storage. Browser quotas or clearing can remove local data, and debounced keystrokes may not survive abrupt process termination. Download copies of important documents.

Corrupt startup recovery is reported without replacing it with sample content. Recovery history is bounded and is not a substitute for a saved file.

Font acquisition retrieves Inter, Carlito, Tinos and Cousine with their upstream license files and writes a source/SHA-256 manifest. Proprietary font names remain document metadata; open-source substitutes do not guarantee Aptos/Calibri metrics or Word pagination. Microsoft font files and branding assets are not redistributed.

## License and contribution

Source code: [MIT](LICENSE). Third-party dependencies and fonts retain their respective licenses. Report reproducible issues with the browser/OS, a minimal non-sensitive document, and the relevant build commit.

Microsoft Word and Microsoft 365 are trademarks of Microsoft. TextSpace is not affiliated with or endorsed by Microsoft.
