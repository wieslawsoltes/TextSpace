<div align="center">

# TextSpace

### A writing room built with Uno Platform and SkiaSharp

Rich text · Paginated paper · Familiar office workflows · Reusable .NET components

[![Build](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/build.yml)
[![Pages](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/TextSpace/actions/workflows/pages.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Core.svg?label=NuGet)](https://www.nuget.org/packages/TextSpace.Core)
[![Downloads](https://img.shields.io/nuget/dt/TextSpace.Core.svg)](https://www.nuget.org/packages/TextSpace.Core)

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

## Download

Every [release](https://github.com/wieslawsoltes/TextSpace/releases/latest) ships a self-contained, single-file desktop app — no .NET install needed:

| OS | x64 | Arm64 |
| --- | --- | --- |
| Windows | `TextSpace-<version>-win-x64.zip` | `TextSpace-<version>-win-arm64.zip` |
| macOS | `TextSpace-<version>-osx-x64.tar.gz` | `TextSpace-<version>-osx-arm64.tar.gz` |
| Linux | `TextSpace-<version>-linux-x64.tar.gz` | `TextSpace-<version>-linux-arm64.tar.gz` |

Extract and run `TextSpace` (`TextSpace.exe` on Windows). Builds are not code-signed yet: on macOS clear the quarantine flag with `xattr -d com.apple.quarantine TextSpace`; on Windows choose **More info → Run anyway** in SmartScreen. Verify downloads against `SHA256SUMS.txt`.

The libraries below are published to [NuGet.org](https://www.nuget.org/packages?q=TextSpace), e.g. `dotnet add package TextSpace.Core --prerelease`.

## NuGet packages

TextSpace ships as ten MIT-licensed packages, all versioned together. Seven target plain `net10.0` and have no UI dependency: `TextSpace.Core`, `Storage`, `Documents`, `Layout`, `Editing` and `OpenXml` are pure .NET, and `TextSpace.Skia` adds only SkiaSharp and HarfBuzz. The three Uno Platform packages (`Controls`, `Editor`, `Workbench`) target `net10.0-desktop` and `net10.0-browserwasm`. Symbols are published to nuget.org as `.snupkg` with SourceLink, so you can step into the library source while debugging.

```sh
dotnet add package TextSpace.Core --prerelease
```

| Package | Version | Downloads | Description |
| :--- | :--- | :--- | :--- |
| [TextSpace.Core](https://www.nuget.org/packages/TextSpace.Core) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Core.svg)](https://www.nuget.org/packages/TextSpace.Core) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Core.svg)](https://www.nuget.org/packages/TextSpace.Core) | UI-independent rich document model, typography, tables, sections, selections and text indexing |
| [TextSpace.Storage](https://www.nuget.org/packages/TextSpace.Storage) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Storage.svg)](https://www.nuget.org/packages/TextSpace.Storage) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Storage.svg)](https://www.nuget.org/packages/TextSpace.Storage) | Recovery contracts, atomic desktop storage, protected originals and checksum-verified transport |
| [TextSpace.Documents](https://www.nuget.org/packages/TextSpace.Documents) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Documents.svg)](https://www.nuget.org/packages/TextSpace.Documents) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Documents.svg)](https://www.nuget.org/packages/TextSpace.Documents) | Validated native serialization, repair plans, templates, fields, HTML export, CSV mail merge and writing checks |
| [TextSpace.Layout](https://www.nuget.org/packages/TextSpace.Layout) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Layout.svg)](https://www.nuget.org/packages/TextSpace.Layout) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Layout.svg)](https://www.nuget.org/packages/TextSpace.Layout) | Renderer-independent pagination, line breaking, tables, columns, section regions and caret hit testing |
| [TextSpace.Editing](https://www.nuget.org/packages/TextSpace.Editing) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Editing.svg)](https://www.nuget.org/packages/TextSpace.Editing) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Editing.svg)](https://www.nuget.org/packages/TextSpace.Editing) | Transactional rich-text editing, Unicode selections, formatting, structure, fields and bounded history |
| [TextSpace.OpenXml](https://www.nuget.org/packages/TextSpace.OpenXml) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.OpenXml.svg)](https://www.nuget.org/packages/TextSpace.OpenXml) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.OpenXml.svg)](https://www.nuget.org/packages/TextSpace.OpenXml) | Dependency-light, bounded DOCX import/export for paragraphs, tables, pictures, comments, headers and footers |
| [TextSpace.Skia](https://www.nuget.org/packages/TextSpace.Skia) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Skia.svg)](https://www.nuget.org/packages/TextSpace.Skia) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Skia.svg)](https://www.nuget.org/packages/TextSpace.Skia) | SkiaSharp + HarfBuzz text metrics, font registration, page rendering and PDF/PNG export |
| [TextSpace.Controls](https://www.nuget.org/packages/TextSpace.Controls) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Controls.svg)](https://www.nuget.org/packages/TextSpace.Controls) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Controls.svg)](https://www.nuget.org/packages/TextSpace.Controls) | Uno office ribbon, iconography, galleries, menus, palettes, dialogs, panes and scroll bars |
| [TextSpace.Editor](https://www.nuget.org/packages/TextSpace.Editor) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Editor.svg)](https://www.nuget.org/packages/TextSpace.Editor) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Editor.svg)](https://www.nuget.org/packages/TextSpace.Editor) | Embeddable Uno Skia paginated editor with native text input, rulers and page previews |
| [TextSpace.Workbench](https://www.nuget.org/packages/TextSpace.Workbench) | [![NuGet](https://img.shields.io/nuget/vpre/TextSpace.Workbench.svg)](https://www.nuget.org/packages/TextSpace.Workbench) | [![Downloads](https://img.shields.io/nuget/dt/TextSpace.Workbench.svg)](https://www.nuget.org/packages/TextSpace.Workbench) | Composable Word-style Uno workbench: ribbon commands, backstage, recovery, navigation and review |

Dependencies (from project references): `Core ← Documents ← Editing`, `Documents ← OpenXml`, `Core ← Layout ← Skia`, `Core ← Controls`; `Controls + Editing + Skia ← Editor`; `Editor + OpenXml + Storage ← Workbench`. `Storage` stands alone. `TextSpace.App` (not packaged) supplies the browser and native desktop hosts.

### TextSpace.Core

The document model every other package builds on: blocks (paragraphs, tables, pictures, page/section/column breaks), immutable character and paragraph formatting, page and section settings, comments, tracked edits, fields and bookmarks. Use it alone to build or inspect documents in code. No dependencies and no UI.

```sh
dotnet add package TextSpace.Core --prerelease
```

**Key types**

- `DocumentModel` – root object: `Blocks`, `Page`, `Comments`, `Fields`, `Bookmarks`, `PlainText`, `WordCount`
- `Paragraph`, `TextRun`, `TableBlock.Create(rows, columns)`, `ImageBlock`, `SectionBreakBlock` – content blocks
- `TextStyle`, `ParagraphFormat`, `PageSettings` – immutable records using typographic points
- `DocumentStyles.BuiltIn` / `DocumentStyles.Find(name)` – Normal, Heading 1–3, Title, Quote, Caption…
- `TextIndex` – UTF-16 coordinate index with paragraph lookup and grapheme snapping (`At`, `Snap`, `Next`)

**Usage**

```csharp
using TextSpace.Core;

var heading = DocumentStyles.Find("Heading 1");
var document = new DocumentModel { Title = "Quarterly report", Page = new PageSettings().Landscape() };
document.Blocks.Clear();
document.Blocks.Add(new Paragraph("Summary", heading.Character, heading.Paragraph));

var body = new Paragraph("Revenue grew ");
body.Runs.Add(new TextRun("12%", new TextStyle { Bold = true, Color = "#107C10" }));
document.Blocks.Add(body);
document.Blocks.Add(TableBlock.Create(rows: 3, columns: 2));

var index = new TextIndex(document);          // UTF-16 text coordinates, grapheme-safe
var address = index.At(10);                   // paragraph containing offset 10
Console.WriteLine($"{document.WordCount} words; offset 10 is in \"{address.Paragraph.Text}\"");
```

### TextSpace.Storage

Local persistence primitives for document apps: an atomic latest-snapshot store with bounded rolling history, a content-addressed archive of protected originals that is never evicted automatically, and a checksum-verified envelope for moving original text across interop boundaries. It has no dependency on the document model and no UI.

```sh
dotnet add package TextSpace.Storage --prerelease
```

**Key types**

- `IRecoveryStore` / `FileRecoveryStore` – `WriteLatestAsync`, `ReadLatestAsync`, `ListVersionsAsync`, `ReadVersionAsync`
- `IRecoveryArchiveStore` / `FileRecoveryArchiveStore` – `ProtectAsync`, `ListProtectedAsync`, `ReadProtectedAsync`
- `RecoveryArchiveTransport` – `Encode` / `Decode` with SHA-256 and byte-count verification
- `RecoveryArchiveLimits` – entry, per-original and total byte budgets

**Usage**

```csharp
using TextSpace.Storage;

var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyApp");

using var recovery = new FileRecoveryStore(Path.Combine(root, "Recovery"));
await recovery.WriteLatestAsync("Draft", json);          // atomic replace + rolling history
string? latest = await recovery.ReadLatestAsync();
foreach (var version in await recovery.ListVersionsAsync())
    Console.WriteLine($"{version.Title} {version.SavedAt:u} ({version.Bytes} bytes)");

var originals = new FileRecoveryArchiveStore(Path.Combine(root, "ProtectedOriginals"));
RecoveryArchive archive = await originals.ProtectAsync(originalText); // SHA-256 addressed
string envelope = RecoveryArchiveTransport.Encode(originalText);
string verified = RecoveryArchiveTransport.Decode(envelope, expectedId: archive.Id);
```

### TextSpace.Documents

Everything that turns a `DocumentModel` into files and back without a renderer: native `.textspace` JSON with strict size/structure validation, explicit tab-stop repair plans, sample templates, field evaluation, sanitized HTML export, CSV mail merge and offline writing checks. Depends on `TextSpace.Core`; no UI.

```sh
dotnet add package TextSpace.Documents --prerelease
```

**Key types**

- `DocumentJson` – `Save`, `Load` (validating), `Clone`, `FromText`, `Validate`
- `DocumentRecovery.PrepareTabRepair` – non-destructive `RecoveryRepairPlan` for invalid saved files
- `HtmlExporter.Export` – escaped HTML with a restrictive content-security policy
- `MailMerge` – `ParseCsv` and `Merge` for `«Field»` placeholders
- `FieldEngine.Evaluate`, `FieldInstruction.Parse` – DATE, PAGE, SEQ, REF/PAGEREF and other supported fields
- `WritingAnalysis` – `Statistics` and repetition/spacing/long-sentence `Check`

**Usage**

```csharp
using TextSpace.Documents;

var template = DocumentJson.FromText("Dear «Name»,\nYour order ships on «Date».", "Letter");
string json = DocumentJson.Save(template);        // native .textspace JSON
var reloaded = DocumentJson.Load(json);           // bounded + validated

var recipients = MailMerge.ParseCsv("Name,Date\nAda,Monday\nGrace,Friday");
foreach (var record in recipients.Records)
{
    var letter = MailMerge.Merge(reloaded, record);
    File.WriteAllText($"letter-{record["Name"]}.html", HtmlExporter.Export(letter));
}

var stats = WritingAnalysis.Statistics(reloaded);
Console.WriteLine($"{stats.Words} words, ~{stats.ReadingMinutes} min");
foreach (var issue in WritingAnalysis.Check(reloaded)) Console.WriteLine(issue.Message);
```

### TextSpace.Layout

Deterministic, point-based layout: line breaking with tab stops, pagination with keeps and widow control, sections and columns, repeating table headers and row splitting, plus caret geometry and hit testing. It measures text through the `ITextMetrics` interface, so it runs headless (tests, servers) or with the real shaper from `TextSpace.Skia`. Depends on `TextSpace.Core`; no UI.

```sh
dotnet add package TextSpace.Layout --prerelease
```

**Key types**

- `PageLayoutEngine` – `Layout(document)` with a bounded `ParagraphLayoutCache`
- `DocumentLayout` – `Pages`, `Caret`, `HitTest`, `VerticalMove`, `VisiblePages`, `Notices`
- `LayoutPage`, `LayoutLine`, `LayoutCell`, `LayoutRegion` – positioned output for renderers
- `ITextMetrics` / `MonospaceTextMetrics` – measurement contract and a deterministic stand-in

**Usage**

```csharp
using TextSpace.Documents;
using TextSpace.Layout;

var document = DocumentJson.FromText(string.Join('\n', Enumerable.Repeat("Lorem ipsum dolor sit amet.", 200)));

// Any ITextMetrics works; MonospaceTextMetrics is a deterministic stand-in (TextSpace.Skia provides real shaping).
var engine = new PageLayoutEngine(new MonospaceTextMetrics());
DocumentLayout layout = engine.Layout(document);
Console.WriteLine($"{layout.Pages.Count} pages, {layout.Pages[0].Lines.Count} lines on page 1");

CaretGeometry caret = layout.Caret(position: 120);
double x = layout.PageLeft(caret.PageIndex) + caret.X, y = layout.PageTop(caret.PageIndex) + caret.Y;
int hit = layout.HitTest(x, y + caret.Height / 2);   // back to a text position
int below = layout.VerticalMove(hit, deltaY: caret.Height);
```

(`TextSpace.Documents` is used here only for `DocumentJson.FromText`.)

### TextSpace.Editing

A UI-independent editing session: every command is an atomic, undoable transaction over the model, with grapheme-safe selections, typing style, paragraph styles, lists, tables (including merge/split), sections, fields, bookmarks, comments and tracked changes. Use it to script edits or to drive your own editor view. Depends on `TextSpace.Documents` (and Core); no UI.

```sh
dotnet add package TextSpace.Editing --prerelease
```

**Key types**

- `EditorSession` – `Document`, `Selection`, `SetSelection`, `InsertText`, `Replace`, `Undo`/`Redo`, `IsDirty`
- Formatting – `ToggleBold`, `SetFont`, `SetFontSize`, `ApplyStyle`, `FormatParagraph`, `ToggleList`
- Structure – `InsertTable`, `InsertPicture`, `InsertSectionBreak`, `MergeTableCells`, `InsertField`, `SetBookmark`
- `EditorSession.Changed` / `EditorChangedEventArgs` – document, selection and view notifications
- `SessionTextProjection` – revision-bound presentation text for input synchronization

**Usage**

```csharp
using TextSpace.Documents;
using TextSpace.Editing;

var session = new EditorSession(DocumentJson.FromText("Introduction\nDetails", "Example"));
session.Changed += (_, e) => Console.WriteLine($"{e.Kind}: {e.Label}");

session.SetSelection(0, 12);            // "Introduction"
session.ApplyStyle("Heading 1");
session.SetSelection(13, 20);           // "Details"
session.ToggleBold();
session.SetBookmark("Details");
session.SetSelection(session.Index.Length, session.Index.Length);
session.InsertText(" follow.");
session.InsertTable(rows: 2, columns: 3);

Console.WriteLine($"{session.Find("details").Count} match(es); dirty: {session.IsDirty}");
session.Undo();                         // each command is one transaction
string json = DocumentJson.Save(session.Document);
```

### TextSpace.OpenXml

Bounded DOCX import and export written directly against the OOXML package (no Open XML SDK at runtime): paragraphs and runs, styles, lists, tab stops, tables with real `gridSpan`/`vMerge`, pictures, comments, bookmarks, fields, sections, headers and footers. Imports cap package sizes, prohibit DTDs and never fetch external relationships; unsupported content is reported as warnings. Depends on `TextSpace.Documents`; no UI.

```sh
dotnet add package TextSpace.OpenXml --prerelease
```

**Key types**

- `DocxReader.Read(bytes)` – returns a `DocxImportResult`
- `DocxImportResult` – the imported `Document` plus `Warnings` for anything dropped
- `DocxWriter.Write(document)` – `.docx` bytes

**Usage**

```csharp
using TextSpace.OpenXml;

DocxImportResult result = new DocxReader().Read(File.ReadAllBytes("input.docx"));
foreach (var warning in result.Warnings)
    Console.WriteLine($"Not imported: {warning}");

var document = result.Document;              // TextSpace.Core.DocumentModel
document.Title = "Round-tripped";
File.WriteAllBytes("output.docx", new DocxWriter().Write(document));
```

### TextSpace.Skia

The rendering backend: HarfBuzz-shaped text metrics with glyph caches, explicit font registration, and page painting (selection, caret, formatting marks, comments, tracked changes) onto any `SKCanvas`, plus PDF and PNG export. Use it headless for server-side PDF/PNG generation. Depends on `TextSpace.Layout`, SkiaSharp and SkiaSharp.HarfBuzz; no UI framework.

```sh
dotnet add package TextSpace.Skia --prerelease
```

**Key types**

- `DocumentRenderer` – `Layout`, `DrawPage`, `ExportPdf`, `ExportPng`, `Metrics`
- `SkiaTextMetrics` – `Register(family, bold, italic, data)`, HarfBuzz `Measure`/`CaretPositions`, shaped-run cache
- `RenderOptions` – selection, caret, formatting marks, comments, changes and boundaries

**Usage**

```csharp
using SkiaSharp;
using TextSpace.Documents;
using TextSpace.Skia;

var document = SampleDocument.Report();
using var renderer = new DocumentRenderer();

// Register faces before layout; the built-in styles use "Aptos".
var regular = File.ReadAllBytes("fonts/Carlito-Regular.ttf");
renderer.Metrics.Register("Aptos", bold: false, italic: false, regular);

var layout = renderer.Layout(document);
File.WriteAllBytes("report.pdf", renderer.ExportPdf(document, layout));
File.WriteAllBytes("page1.png", renderer.ExportPng(document, layout, pageIndex: 0, scale: 2));

// Or draw a page onto any SKCanvas (units are points).
using var surface = SKSurface.Create(new SKImageInfo(612, 792));
renderer.DrawPage(surface.Canvas, document, layout, pageIndex: 0, new RenderOptions { ShowFormatting = true });
```

### TextSpace.Controls

Custom Uno Platform office chrome drawn with the Skia renderer: a tabbed `RibbonBar` with lazy groups, ribbon/office buttons with bindable appearance properties, vector icons, style gallery, table picker, color palette, menus, dialogs, task panes, zoom slider and scroll bars. Useful for any Uno app that wants an Office-style command surface. Depends on `TextSpace.Core` (for style definitions) and Uno Platform (Skia renderer).

```sh
dotnet add package TextSpace.Controls --prerelease
```

**Key types**

- `RibbonBar` – `AddTab`, `InsertGroup` (extend a tab without replacing its factory), `SelectTab`, `AddFileTab`
- `RibbonGroup`, `RibbonButton`, `OfficeButton` (`IsSelected`, `IsPrimary`, `RestBackground` dependency properties)
- `OfficeIcon`, `StyleGallery`, `TablePicker`, `ColorPalette` (`AsFlyout`)
- `OfficeMenu`, `OfficeDialog`, `OfficeTaskPane`, `OfficeZoomSlider`, `OfficeScrollBar`
- `OfficeTheme` – palette constants, `Font`, and `Text`/`Rows`/`Columns` layout helpers

**Usage**

```csharp
using TextSpace.Controls;

var status = OfficeTheme.Text("Ready", 12, OfficeTheme.Muted);
var ribbon = new RibbonBar();
ribbon.AddTab("Home", () =>
{
    var clipboard = new RibbonGroup("Clipboard");
    clipboard.Body.Children.Add(new RibbonButton("paste", "Paste", () => status.Text = "Pasted", large: true, showLabel: true));
    clipboard.Body.Children.Add(new RibbonButton("copy", "Copy", () => status.Text = "Copied"));
    return [clipboard];
});

var zoom = new OfficeZoomSlider { Value = 1 };
zoom.ValueChanged += value => status.Text = $"{value:P0}";
window.Content = OfficeTheme.Rows((ribbon, 0), (status, -1), (zoom, 0)); // 0 = auto, -1 = star
```

### TextSpace.Editor

The embeddable paginated editor: `DocumentSurface` renders an `EditorSession` with Skia, bridges native text input, handles mouse/keyboard selection, zoom and scrolling, and shows a horizontal ruler with indent handles. `DocumentPreview` draws page thumbnails. Depends on `TextSpace.Controls`, `TextSpace.Editing`, `TextSpace.Skia` and Uno Platform (Skia renderer).

```sh
dotnet add package TextSpace.Editor --prerelease
```

**Key types**

- `DocumentSurface` – `DocumentSurface(session)`, `Session`, `Renderer`, `Layout`, `Relayout`, `SetZoom`, `FitPageWidth`, `FocusEditor`
- `DocumentSurface` events – `Error`, `ViewChanged`, `CommandRequested`, `ContextRequested`
- `PageRuler` – ruler with `IndentChanged`
- `DocumentPreview` – page thumbnail for navigation panes

**Usage**

```csharp
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Editor;

var session = new EditorSession(DocumentJson.FromText("Hello from TextSpace", "Hello"));
var surface = new DocumentSurface(session) { ShowRuler = true };
surface.Renderer.Metrics.Register("Aptos", false, false, fontBytes); // register faces, then relayout
surface.Relayout();
surface.Error += message => System.Diagnostics.Debug.WriteLine(message);

window.Content = surface;
window.Closed += (_, _) => surface.Dispose();
surface.FocusEditor();
```

### TextSpace.Workbench

The complete Word-style shell: ribbon tabs (Home, Insert, Design, Layout, References, Mailings, Review, View, Help plus contextual table/picture tabs), File backstage, navigation/review panes, dialogs, recovery tools and status bar around a `DocumentSurface`. All platform services go through `IWorkspaceHost` (files, clipboard, printing, URI launching and `IRecoveryStore` AutoSave); wrap it in `RecoveryWorkspaceHost` with an `IRecoveryArchiveStore` to enable the Recovery tab. Dispose owned workbenches when their host closes. Depends on `TextSpace.Editor`, `TextSpace.OpenXml`, `TextSpace.Storage` and Uno Platform.

```sh
dotnet add package TextSpace.Workbench --prerelease
```

**Key types**

- `WordWorkbench` – `WordWorkbench(session, host)`, `Session`, `Surface`, `Ribbon`, `ExecuteCommandAsync(id)`, `StateChanged`
- `IWorkspaceHost` – host contract; `OpenedFile` result
- `RecoveryWorkspaceHost` – adds protected originals to any host
- `WordWorkbench.EnableRecoveryTools` / `ShowRecoveryNotice` – recovery UI

**Usage**

```csharp
using TextSpace.Controls;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Storage;
using TextSpace.Workbench;

// MyWorkspaceHost implements IWorkspaceHost (file pickers, clipboard, printing, URIs, AutoSave).
IWorkspaceHost host = new RecoveryWorkspaceHost(new MyWorkspaceHost(),
    new FileRecoveryArchiveStore(Path.Combine(appData, "ProtectedOriginals")));

var workbench = new WordWorkbench(new EditorSession(SampleDocument.Create()), host);
workbench.EnableRecoveryTools();
workbench.Ribbon.InsertGroup("Home", 0, () => new RibbonGroup("My tools"));
workbench.Surface.Renderer.Metrics.Register("Aptos", false, false, fontBytes);
workbench.Surface.Relayout();

window.Content = workbench;
window.Closed += (_, _) => workbench.Dispose();
```

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

**Release** runs for `v*` tags or a supplied manual version. It consumes a successful main browser build for the exact commit, reruns engine tests, publishes self-contained single-file desktop executables for Windows, macOS and Linux (x64 and arm64), packs the ten libraries with symbols and produces versioned source/browser archives and checksums. Tags attach all assets to a GitHub Release and publish the packages to NuGet.org with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC, no stored API key) from the protected `nuget` environment. Manual runs are dry runs: they build and upload every asset as workflow artifacts but publish nothing. [Publishing](docs/PUBLISHING.md).

## Performance and compatibility

Bounded paragraph/glyph caches, caret/table interval indexes, visible-page queries, canonical-run fast paths and print pagination reuse reduce repeated work. `SessionTextProjection` reuses presentation text during input synchronization/selection events. It is not a live model index: raw external mutations require document notification or explicit invalidation/relayout. Transaction indexing and snapshot history remain unchanged.

[Performance](docs/PERFORMANCE.md) and [recovery/input measurements](docs/PERFORMANCE-RECOVERY.md) provide harnesses, raw evidence, allocation costs and limitations. Validation/query timings are not browser FPS or end-to-end typing guarantees.

**Native `.textspace` represents TextSpace's full model, not arbitrary Word structures.** DOCX remains an interchange subset. Rich independent header/footer stories, footnotes/endnotes, equations, floating drawings, full Word revision and field/TOC semantics, advanced table cases, bibliography databases, embedded objects, collaboration, macros and add-ins remain unfinished. HarfBuzz shaping alone does not establish full bidi layout. The offline Editor checks repetition/spacing/long sentences, not full spelling or AI grammar. [Compatibility ledger](docs/COMPATIBILITY.md).

## Privacy, fonts and license

Documents are processed locally: no account, analytics, document upload or AI service is required. AutoSave is device-local, and abrupt termination can lose debounced edits. Keep independent backups. Imports bound compressed/expanded package sizes, prohibit XML DTDs and never fetch external document relationships.

Font acquisition retrieves Inter, Carlito, Tinos and Cousine with their licenses and a source/SHA-256 manifest. Proprietary family names remain metadata; substitutes do not guarantee Word font metrics/pagination. Microsoft fonts and proprietary branding assets are not redistributed.

Source: [MIT](LICENSE). Third-party dependencies/fonts retain their licenses. Microsoft Word and Microsoft 365 are Microsoft trademarks. TextSpace is not affiliated with or endorsed by Microsoft.
