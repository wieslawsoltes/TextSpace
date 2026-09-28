<div align="center">

# TextSpace

### A writing room built with Uno Platform and SkiaSharp

Rich text · Paginated paper · Familiar office workflows · Reusable .NET components

[![Build](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml)
[![Pages](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

[Open the browser app](https://wieslawsoltes.github.io/TextSpace/) · [Architecture](docs/ARCHITECTURE.md) · [Recovery](docs/RECOVERY.md) · [Performance](docs/PERFORMANCE.md) · [Publishing](docs/PUBLISHING.md) · [Compatibility](docs/COMPATIBILITY.md)

</div>

---

TextSpace is an independent, local-first word processor built in **C#**, **Uno Platform**, **SkiaSharp**, and **HarfBuzz**. Its ribbon, editing surface, navigation panes, review tools and file workflows are reusable controls—not an embedded web editor or a screenshot of another application.

> **Development preview: 0.5.1-alpha.1.** This is not Microsoft Word. Complete feature parity, identical pagination, pixel-identical appearance and lossless DOCX round-tripping are not implemented. Keep independent file copies of important documents. The live site's version is determined by its last successful Pages deployment, not this source version alone.

## Workspace

The custom ribbon groups commands into Home, Insert, Design, Layout, References, Mailings, Review, View and Help, with contextual table/picture tabs. File backstage provides templates, import/export and rolling local history. Hosts with protected archive support also expose a Recovery tab.

| Area | Implemented scope |
| :--- | :--- |
| Editing | Rich runs, paragraph splitting/joining, grapheme-safe selections, transactional editing, bounded undo/redo and native input |
| Typography | Font metadata, emphasis, colors, styles, paragraph spacing/alignment, aligned tab stops/leaders and discretionary/nonbreaking characters |
| Layout | Mixed-size sections, next/odd/even-page, continuous and next-column starts, per-section numbering, first/even/default plain-text headers/footers, paragraph keeps and widow controls |
| Fields and contents | 17 supported field types, cached results, F9 updates, locks, sequences, bookmark/page references and linked contents entries |
| Tables and pictures | Rectangular merges, split-to-grid, nested tables, first-header repetition, vertical alignment, row splitting, span-aware edits and in-cell/in-flow pictures |
| Navigation/review | Heading outline, page previews, search/replace, bookmarks, safe links, comments/replies and guarded local change rejection |
| Interchange | Native `.textspace`, bounded DOCX import/export, PDF/PNG rendering, HTML/plain text and CSV mail merge |
| Recovery | IndexedDB/native rolling snapshots, actionable startup recovery, explicit tab repair and non-evicting checksum-verified protected originals |

Unsupported commands are disabled rather than producing simulated results. SkiaSharp renders the document; a UI-independent C# editing session owns its content. Uno provides the platform text-input bridge.

### Recover a document without clearing its data

Invalid saved workspaces open the **Recovery Center**. Download the unchanged original first. **Preview tab-stop repair** reports invalid, duplicate and excess stops without writing anything. **Protect original and open repaired copy** commits a separate SHA-256-addressed original before creating an editable copy and enabling its AutoSave. Other explicit choices protect the original and start blank, retry, or restore an earlier valid snapshot.

Missing/null/empty legacy tab arrays mean no custom stops. A genuinely oversized array still fails strict validation: explicit repair retains the first 128 valid unique positions, discloses removals and may change tab layout. This is not a general-purpose corruption converter. Other document structures and supported metadata remain subject to their existing validators.

**Recovery → Open and Repair** applies the same workflow to native files. **Protected Originals** downloads retained originals after editing/reload. Valid UTF-8 file bytes, including a BOM, are retained; only the separate parsing copy removes the marker. Browser/native original transport uses generated JSON framing and byte-count/checksum verification so leading markers remain data across interop boundaries.

Protected archives are bounded and never automatically evicted. Failed/full protection blocks replacement of active recovery. Local archives can still be cleared by the browser/device owner and are not external backups. [Recovery API and workflow](docs/RECOVERY.md) · [Validation evidence](docs/RECOVERY-VALIDATION.md) · [Recovery/input performance](docs/PERFORMANCE-RECOVERY.md).

### Sections, fields and typography

**Layout → Section Break / Section Settings** controls page dimensions, orientation, columns, numbering and inherited header/footer variants. Compatible continuous sections share physical pages with region-aware fields, carets and rulers. Bounded paragraph-only bands can balance; incompatible geometry produces notices. Shared pages retain the first region's header/footer ownership. [Sections and fields](docs/SECTIONS-AND-FIELDS.md) · [Continuous sections](docs/CONTINUOUS-SECTIONS.md).

**Insert → Field**, **Manage Fields** and **F9** support metadata, date/time, sequence and reference workflows. Generated contents use hidden bookmarks and real REF/PAGEREF fields with geometric right-aligned dot leaders. Update Table rebuilds entries. This is not an enclosing native Word TOC field or the full field grammar.

**Layout → Tabs / Pagination** configures aligned stops, leaders, a default interval, paragraph keeps and widow controls. **Insert → Special Characters** exposes discretionary and nonbreaking characters. Rich-run boundaries inside words do not create artificial break opportunities. [Typography](docs/TYPOGRAPHY.md).

### Tables, bookmarks and links

**Table Layout → Merge Cells / Split Cell / Nested Table / Row Options** exposes supported rectangular spans, recursive editing, vertical alignment and row pagination. Structural edits preserve surviving paragraph identities and remap review/bookmark/field anchors. DOCX uses real gridSpan/vMerge; HTML uses row/column spans. [Tables](docs/TABLES.md).

Create, rename, move, delete and navigate bookmarks through Insert. Renaming updates matching internal hyperlinks atomically. DOCX contains real bookmark markers; HTML contains safe named targets and escaped CSS under a restrictive content-security policy. [Bookmarks](docs/BOOKMARKS.md).

## Ten reusable libraries

| Library | Responsibility |
| :--- | :--- |
| `TextSpace.Core` | Rich document model, typography, blocks, tables, review/bookmark anchors and text indexing |
| `TextSpace.Documents` | Native serialization/validation, explicit repair plans, templates, HTML, CSV merge and offline writing checks |
| `TextSpace.Editing` | Atomic editing, selections, formatting, structure, history and revision-bound presentation text |
| `TextSpace.Layout` | Renderer-independent geometry, pagination, caret queries, section regions and hit testing |
| `TextSpace.Skia` | HarfBuzz metrics, font registration, page rendering and PDF/PNG export |
| `TextSpace.OpenXml` | Bounded DOCX import/export; Open XML SDK is a validation-only test dependency |
| `TextSpace.Storage` | Recovery contracts, atomic files, protected archives and checksum-verified transport |
| `TextSpace.Controls` | Custom ribbon, galleries, menus, icons, palettes, dialogs, panes and scrolling |
| `TextSpace.Editor` | Embeddable paginated editor, native input, rulers and previews |
| `TextSpace.Workbench` | Composable office shell, commands, backstage, recovery, navigation and review |

`TextSpace.App` supplies browser and native desktop hosts. `IWorkspaceHost` abstracts files, clipboard, printing, URI launching and recovery. Dispose owned surfaces/workbenches when their hosts close. Hosts implementing `IRecoveryArchiveStore` can enable recovery tools.

```csharp
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.OpenXml;
using TextSpace.Skia;

var document = DocumentJson.FromText("Introduction\nDetails", "Example");
var editor = new EditorSession(document);
editor.SetSelection(13, 20);
editor.SetBookmark("Details");
File.WriteAllBytes("Example.docx", new DocxWriter().Write(document));

using var renderer = new DocumentRenderer();
// Register required faces before layout for predictable typography.
var layout = renderer.Layout(document);
File.WriteAllBytes("Example.pdf", renderer.ExportPdf(document, layout));
```

Embed `new TextSpace.Editor.DocumentSurface(session)` or `new TextSpace.Workbench.WordWorkbench(session, workspaceHost)` in an Uno application. Ribbon hosts can insert lazy groups with `RibbonBar.InsertGroup`; office-button appearance properties support XAML binding.

## Build and run

The repository pins .NET SDK **10.0.401** and Uno SDK **6.7.30** in `global.json`, with Uno-compatible SkiaSharp **3.119.2**. Native Skia updates require compatibility checks against the chosen Uno renderer.

```sh
git clone https://github.com/wieslawsoltes/TextSpace.git
cd TextSpace
python3 scripts/fetch-assets.py
dotnet test tests/TextSpace.Tests -c Release
dotnet run --project src/TextSpace.App -f net10.0-desktop -p:TextSpaceDesktopOnly=true
```

```sh
dotnet workload install wasm-tools
dotnet publish src/TextSpace.App -f net10.0-browserwasm -c Release \
  -p:WasmShellWebAppBasePath=/TextSpace/ -o artifacts/publish
python3 scripts/collect-site.py artifacts/publish site
python3 scripts/serve-site.py site --port 4173
```

Open `http://127.0.0.1:4173/TextSpace/`. Browser builds require HTTP/HTTPS; `file://` is unsupported.

## Validation and delivery

**Build** runs engine tests, Windows/Linux/macOS compilation, real-input Chromium acceptance and ten-library NuGet packaging. **Pages** consumes only a successful main Build artifact, verifies source provenance, deploys, and repeats browser acceptance against the public URL. Runtime-module preflight rejects missing assets and HTML fallback responses.

Browser diagnostics expose read-only state/geometry. Tests use actual mouse/keyboard/filechooser input; storage mutation is limited to deliberate persisted-input/failure fixtures. Native compilation does not establish exhaustive native interaction, and Chromium results do not establish Safari/Firefox/mobile/IME/accessibility parity.

**Release** consumes a successful main browser build for the exact commit, packages libraries and produces versioned archives/checksums. NuGet upload is an explicit opt-in requiring the configured secret. Package creation is not publication. [Publishing](docs/PUBLISHING.md).

## Performance and compatibility

Bounded paragraph/glyph caches, caret/table interval indexes, visible-page queries, canonical-run fast paths and print pagination reuse reduce repeated work. `SessionTextProjection` reuses presentation text during input synchronization/selection events. It is not a live model index: raw external mutations require document notification or explicit invalidation/relayout. Transaction indexing and snapshot history remain unchanged.

[Performance](docs/PERFORMANCE.md) and [recovery/input measurements](docs/PERFORMANCE-RECOVERY.md) provide harnesses, raw evidence, allocation costs and limitations. Validation/query timings are not browser FPS or end-to-end typing guarantees.

**Native `.textspace` represents TextSpace's full model, not arbitrary Word structures.** DOCX remains an interchange subset. Rich independent header/footer stories, footnotes/endnotes, equations, floating drawings, full Word revision and field/TOC semantics, advanced table cases, bibliography databases, embedded objects, collaboration, macros and add-ins remain unfinished. HarfBuzz shaping alone does not establish full bidi layout. The offline Editor checks repetition/spacing/long sentences, not full spelling or AI grammar. [Compatibility ledger](docs/COMPATIBILITY.md).

## Privacy, fonts and license

Documents are processed locally: no account, analytics, document upload or AI service is required. AutoSave is device-local, and abrupt termination can lose debounced edits. Keep independent backups. Imports bound compressed/expanded package sizes, prohibit XML DTDs and never fetch external document relationships.

Font acquisition retrieves Inter, Carlito, Tinos and Cousine with their licenses and a source/SHA-256 manifest. Proprietary family names remain metadata; substitutes do not guarantee Word font metrics/pagination. Microsoft fonts and proprietary branding assets are not redistributed.

Source: [MIT](LICENSE). Third-party dependencies/fonts retain their licenses. Microsoft Word and Microsoft 365 are Microsoft trademarks. TextSpace is not affiliated with or endorsed by Microsoft.
