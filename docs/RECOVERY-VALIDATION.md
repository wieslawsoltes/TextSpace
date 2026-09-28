# Recovery validation and performance evidence

The reported startup trace enters `DocumentJson.ValidateTypography`, `DocumentJson.Load` and `App.OnLaunched`. The previous diagnostic combined two distinct cases: an uninitialized immutable array and a real array with more than 128 stops. The trace alone does not reveal the stored JSON, so tests cover both without asserting which occurred in the user's document.

## Trimmed browser contracts

Recovery archive metadata and diagnostic UI state use closed source-generated JSON contracts. The custom immutable tab-array converter resolves `JsonTypeInfo<TabStop>` from its caller's source-generated context once per nonempty collection. Missing, null and empty arrays require no per-element contract lookup. There is no reflection-resolver fallback in this converter.

The source-generated contract tests exercise sparse constructor defaults, explicit false/zero preservation, null/empty arrays, exactly 128 stops, all supported properties and rejection before converting an oversized tail. They complement actual Release WebAssembly publication tests; a desktop JSON round-trip alone does not prove trimming safety.

Initial recovery browser runs reached the expected invalid-document handler but did not publish usable recovery state. Replacing anonymous reflection-based diagnostic serialization with a generated DTO allowed the oversized recovery, original download and non-mutating preview scenario to pass on Build 36443009702. The application view lifecycle and original-protection ordering were unchanged.

## Asynchronous adoption is a distinct readiness state

Cold startup tests fail promptly on a startup error. Leaving the recovery center is different: the previous startup error is intentionally retained while the original is protected, fonts load and the new workbench is created. An assertion immediately after clicking a repair button must not mistake this retained error for the completion of the new asynchronous operation.

The recovery suite observes successful workbench readiness, absence of the old startup error, closure of recovery state and real canvas layout. It never clears globals, invokes an editing command through diagnostics, or weakens the original-byte, archive checksum, active-storage, metadata or editing assertions. All modifications to application storage in tests are synthetic persisted-input and explicit fault fixtures.

## Reproducible tab-validation measurements

Run:

```sh
dotnet build benchmarks/TextSpace.Performance -c Release
dotnet run --project benchmarks/TextSpace.Performance -c Release --no-build -- --tab-validation
```

CI records the raw JSON alongside engine test results. The harness compares the tab-validation loop from baseline `d6059e2112697baa9e69123de07c2959682710c8` with `TabStopRules.Validate`. It checks acceptance equivalence before measurement, then measures 20,000 validations per sample at 0, 1, 4, 8, 32 and 128 stops, with three warmups and seven measured batches. Stopwatch timestamps and thread-managed allocation counters exclude report/sample construction.

The common empty-array path does not allocate a HashSet. Collections with up to eight stops use stack storage; larger collections pre-size their set. These measurements cover validation only, not JSON parsing, complete document startup, I/O, rendering, typing latency or browser frame rate. Timing is observational rather than a flaky CI speed threshold. Actual run artifacts, not hypothetical speedup ratios, are the evidence.

See [Recovery](RECOVERY.md) for explicit repair choices and preservation limits. Passing this work does not establish full Word compatibility or exhaustive browser/native accessibility and IME behavior.
