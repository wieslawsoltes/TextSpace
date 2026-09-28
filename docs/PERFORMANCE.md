# Performance engineering

The paragraph layout cache is single-threaded and bounded by 2,048 entries and 32 MiB of estimated managed payload. Returned geometry is independently owned. Text, styles, width, default formatting, and `IVersionedTextMetrics.MetricsVersion` invalidate measurements; global text offsets are rebased without remeasurement. Unversioned metric providers must remain stable or explicitly clear the cache.

`DocumentLayout` builds a text-interval index with original-order tie resolution for caret queries. Hit testing selects the nearest line without sorting and caches immutable grapheme offsets. The renderer retains its page-layout engine. PNG export accepts an existing document/layout snapshot, and printing reuses that layout instead of paginating the document once per printed page.

## Reproduce

```sh
dotnet run --project benchmarks/TextSpace.Performance -c Release -- current
```

The harness reports median timings and thread-local managed allocations after three warmups. It uses 1,000 paragraphs / 127,889 UTF-16 characters, 20,000 seeded caret queries and 2,000 hit tests. Run the same harness against both revisions on the same host. Timings are observations, not CI thresholds or browser-frame-rate guarantees.

The accompanying JSON records were collected on .NET 10.0.12, Linux x64. Baseline `49c08288b3b052eab2adae4896bbf4f205163300`: warm layout 80.6052 ms / 28,953,136 allocated bytes; caret batch 45.26 ms / 3,520,040 bytes; hit-test batch 18.7866 ms / 4,926,800 bytes. Optimized: 19.0995 ms / 10,042,872 bytes; 14.9334 ms / 40 bytes; 12.708 ms / 128,040 bytes. Page counts (59) and query checksum match. Instrumentation itself accounts for the 40-byte stopwatch allocation.

## Boundaries

Repagination still walks the document and allocates independent output geometry. The document and public layout models are mutable; a layout index is a snapshot and must be rebuilt after mutating text intervals. The cache compares source runs rather than assuming all mutation goes through `EditorSession`. Snapshot undo and full-document index reconstruction remain; invalidation must be explicit before replacing them with cached state. HarfBuzz prefix measurement remains quadratic on cold very-long unbroken tokens. Benchmark results do not establish native UI, browser, memory working-set, or million-character editing performance.

## Native shaped-text reuse

`SkiaTextMetrics` also retains a separate LRU of origin-relative `SKTextBlob` resources (default 2,048 runs / 16 MiB estimated payload). Its key includes text, family, effective size, weight and slant, but not paint-only color, decorations, hyperlinks or baseline shifts. Clearing metrics or disposing the renderer releases native blobs. Do not mutate cache-owned fonts returned by `Font`; invalidate metrics after changing metric configuration. Both caches are single-writer resources.

```sh
dotnet run --project benchmarks/TextSpace.Performance -c Release -- --glyphs
```

`performance-glyphs.json` compares the previous direct `DrawShapedText` path with warm cached drawing on the same CPU Skia surface: 1,000 draws / 100 unique runs, three warmups, seven measured iterations. Median 13.0663 → 5.5165 ms, allocated managed bytes 1,636,040 → 40 (stopwatch allocation). The cached runs account for an estimated 65,020 bytes. This isolates drawing; it is not an end-to-end browser benchmark. Raster tests compare cached and uncached shaping at fixed coordinates for Latin ligatures, combining marks and Arabic; that does not establish full bidirectional paragraph correctness.

Font/shaper/measurement caches outside these two LRUs retain their earlier behavior. The payload budgets do not include all native driver allocations or guarantee total process memory usage. The richer line breaker performs additional work on cache misses; monitor cold layout separately from warm reuse.

## Table editing and normalization

Canonical `Paragraph.Normalize()` calls now retain the list/run identities and allocate nothing for already-normalized content. Noncanonical runs still remove empty strings and coalesce equal adjacent styles. TextRun and paragraph collections are mutable and must not be shared between independently editable document locations. Normalization is not a cloning API.

Structural edits precompute surviving paragraph identities plus nearest before/after fallbacks, then binary-search original paragraph endpoints for each bookmark/comment/field/selection endpoint. This replaces an O(anchors × paragraphs) remapping scan with O(paragraphs + anchors × log paragraphs) mapping work. Validation, serialization and index reconstruction still have their own costs. Table page slices use vertical interval indexes to find relevant lines, pictures and cell frames; long overlapping cells can still require larger candidate scans.

```sh
dotnet run --project benchmarks/TextSpace.Performance -c Release -- --table-edit current
```

`performance-tables-before.json` and `performance-tables-after.json` were measured on the same Debian 13 Linux x64 host, .NET 10.0.12, three warmups and seven measurements. The baseline source is `84f095437abca971927cd05d370c085d4f1be630` (engine-equivalent to merged `b397a304f800529243776ea286ffb96da67f8c10`; intervening change fixes only browser-test focus). Copy the identical TableEditingBenchmark harness and Editing project reference into the baseline to reproduce the comparison.

| Operation | Baseline median | New median | Managed bytes: baseline → new |
| --- | ---: | ---: | ---: |
| Normalize 4,000 already-canonical four-run paragraphs | 3.8148 ms | 0.6962 ms | 2,176,040 → 40 |
| Insert a column and Undo, 100×16 table / 1,601 bookmarks | 140.3147 ms | 78.7507 ms | 22,595,784 → 23,172,504 |

The table operation is faster in this sample but allocates about 2.6% more managed bytes because it now maintains validated merged-cell topology and richer native metadata. The remaining allocations are not hidden by the timing improvement. Results are observations with raw per-iteration data, not fixed performance promises. The 40-byte normalization result is the benchmark Stopwatch allocation. These are CPU engine measurements, not browser input latency or FPS measurements.
