<div align="center">

# TextSpace

### A writing room built with Uno Platform and SkiaSharp

Rich text · Paginated paper · Familiar office workflows · Reusable .NET components

[![Build](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml)
[![Pages](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

[Browser deployment](https://wieslawsoltes.github.io/TextSpace/) · [Architecture](docs/ARCHITECTURE.md) · [Compatibility](docs/COMPATIBILITY.md) · [Issues](https://github.com/wieslawsoltes/TextSpace/issues)

</div>

---

TextSpace is an independent, local-first word processor implemented in **C#**, **Uno Platform**, **SkiaSharp**, and **HarfBuzz**. Its office-style ribbon, editing surface, navigation panes, review tools and file workflows are assembled from reusable libraries—not an embedded web editor or a screenshot of another application.

> **Development preview: 0.1.0-alpha.1.** This is not Microsoft Word. Complete feature parity, identical pagination, pixel-identical appearance and lossless DOCX round-tripping are not implemented. Keep file copies of important documents. The Pages badge reports deployment status; a target link alone does not establish that deployment succeeded.

## The workspace

The custom ribbon groups commands into Home, Insert, Design, Layout, References, Mailings, Review, View and Help. Contextual table and picture tabs expose object-specific workflows. File backstage provides document templates, file operations and local version history.

| Area | Implemented scope |
| :--- | :--- |
| Editing | Rich runs, paragraph splitting/joining, grapheme-safe selection, formatting, atomic transactions and bounded undo/redo |
| Typography | Font metadata, size, emphasis, super/subscript, colors, highlights, styles, paragraph spacing and alignment |
| Layout | Paper dimensions, margins, breaks, columns, repeated header/footer text, page placeholders, rulers and previews |
| Tables and pictures | Editable cells, row/column operations, shading, in-flow PNG/JPEG/GIF pictures, sizing and alternative text |
| Review | Anchored comments/replies, resolved threads, local tracked-text edits and guarded rejection |
| Files | Native `.textspace`, bounded DOCX interchange, PDF/PNG rendering, HTML/plain-text export |
| Mail merge | CSV data, `«field»` substitution, preview and bounded DOCX batch export |
| Recovery | Browser IndexedDB, atomic desktop files and bounded snapshot history |

The document itself is rendered by SkiaSharp. Edits go through a UI-independent session; Uno supplies the native text-input bridge. Unsupported commands are disabled. [Compatibility notes](docs/COMPATIBILITY.md) distinguish tested behavior from remaining advanced capabilities.

## Ten reusable libraries

| Library | Responsibility |
| :--- | :--- |
| `TextSpace.Core` | Rich document model, typography, blocks, tables, images, review anchors and text indexing |
| `TextSpace.Documents` | Native serialization/validation, templates, HTML output, CSV merge and offline writing checks |
| `TextSpace.Editing` | Atomic editing, selections, rich formatting, structure, history and review |
| `TextSpace.Layout` | Renderer-independent line geometry, pagination, caret positioning and hit testing |
| `TextSpace.Skia` | HarfBuzz metrics, font registration, page rendering, PDF and PNG output |
| `TextSpace.OpenXml` | Bounded DOCX package import/export; Open XML SDK is a validation-only test dependency |
| `TextSpace.Storage` | Recovery contracts and atomic-file implementation |
| `TextSpace.Controls` | Ribbon, galleries, menus, icons, palettes, table picker, dialogs, panes and scrolling |
| `TextSpace.Editor` | Embeddable paginated editor, ruler, input bridge and previews |
| `TextSpace.Workbench` | Composable office shell, commands, backstage, navigation and review workflows |

`TextSpace.App` contains browser and native desktop hosts. Packable projects are not automatically published NuGet packages.

### Engine integration

```csharp
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.OpenXml;
using TextSpace.Skia;

var editor = new EditorSession(
    DocumentJson.FromText("Hello TextSpace", "Example"),
    maximumHistoryEntries: 100,
    maximumHistoryBytes: 64L * 1024 * 1024);

editor.SetSelection(0, 5);
editor.ToggleBold();

File.WriteAllText("Example.textspace", DocumentJson.Save(editor.Document));
File.WriteAllBytes("Example.docx", new DocxWriter().Write(editor.Document));

using var renderer = new DocumentRenderer();
// Register your application's licensed typefaces before layout for consistent metrics.
var layout = renderer.Layout(editor.Document);
File.WriteAllBytes("Example.pdf", renderer.ExportPdf(editor.Document, layout));
```

Undo, redo and rollback may replace the model instance: read `editor.Document` after mutations rather than retaining an earlier reference.

### Uno integration

```csharp
var session = new TextSpace.Editing.EditorSession(document);
var surface = new TextSpace.Editor.DocumentSurface(session);
// Place surface in your Uno view and dispose it with its owning view.

var workbench = new TextSpace.Workbench.WordWorkbench(session, workspaceHost);
// IWorkspaceHost supplies your application's file, clipboard, print and recovery services.
```

## Build and run

The repository pins **.NET SDK 10.0.401**, **Uno SDK 6.7.30** and the matching **SkiaSharp 3.119.2** rendering stack. Do not upgrade managed/native Skia independently. Python 3 acquires pinned open-font assets; Node.js 22+ runs browser acceptance.

```sh
git clone https://github.com/wieslawsoltes/TextSpace.git
cd TextSpace
python3 scripts/fetch-assets.py

dotnet test tests/TextSpace.Tests -c Release

dotnet run --project src/TextSpace.App \
  -f net10.0-desktop -p:TextSpaceDesktopOnly=true
```

For the browser:

```sh
dotnet workload install wasm-tools --skip-manifest-update
dotnet publish src/TextSpace.App -c Release -f net10.0-browserwasm \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/TextSpace/
python3 scripts/collect-site.py artifacts/publish site
python3 scripts/serve-site.py site --port 4173
```

Open `http://127.0.0.1:4173/TextSpace/`. Use HTTP/HTTPS, not `file://`. In a second terminal:

```sh
npm install --no-audit --no-fund
npx playwright install --with-deps chromium
TEXTSPACE_BASE_URL=http://127.0.0.1:4173/TextSpace/ npm run test:browser
```

## Validation and deployment

Build runs engine/interchange tests, compiles the native host on Linux, Windows and macOS, publishes the actual WebAssembly application, and exercises real mouse/keyboard editing in Chromium. The static acceptance server checks that served runtime bytes match the publication. Browser diagnostics are read-only and trimming-safe.

Pages consumes only a successful, non-PR `main` Build, verifies source-commit provenance, deploys the browser artifact, and repeats acceptance against the public URL. Compilation or artifact upload alone is not deployment verification. Inspect the first failed step and captured browser console/request logs before retrying publication.

Engine regression coverage includes Unicode graphemes and reversed selections, range validation, transaction rollback and observer ordering, memory-bounded history, read-only formatting, rich-run case conversion, table structure, review-anchor mapping, DOCX schema validation, malicious XML rejection and Skia exports.

## Privacy and licensing

Documents are processed locally. TextSpace has no document-upload service, collaboration backend, account requirement or analytics integration. Runtime/font loading and hosting still involve ordinary network requests. External links open through explicit user actions.

AutoSave is local recovery, not cloud synchronization. Quotas, cleared storage, denied permissions and abrupt termination can prevent recovery; keep downloaded `.textspace` copies.

Font acquisition uses pinned upstream revisions and retains original license files and SHA-256 manifests. Inter, Carlito, Tinos and Cousine supply UI/text faces and substitutions. Proprietary font-family names remain document metadata; substitutions do not guarantee Microsoft font metrics. No Microsoft font binaries, logos or application assets are redistributed.

Source: [MIT License](LICENSE). Third-party dependencies retain their own licenses. Microsoft Word and Microsoft 365 are trademarks of Microsoft. TextSpace is not affiliated with or endorsed by Microsoft.
