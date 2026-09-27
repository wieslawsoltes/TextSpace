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
