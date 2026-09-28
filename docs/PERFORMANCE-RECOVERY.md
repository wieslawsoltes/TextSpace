# Recovery and native input performance

## Presentation text reuse

`SessionTextProjection` caches committed text for its owning session and invalidates on document notifications, revision changes, document replacement or explicit invalidation. The native editor bridge uses it during synchronization, capture comparisons and selection notifications, and disposes the subscription with the surface. Public relayout invalidates it.

`EditorSession.Index` remains live and uncached. Hosts that mutate model objects directly must notify `EditorChangeKind.Document` or invalidate/relayout before consuming presentation state. Tests cover reuse, external same-revision refresh, document replacement, undo/redo and disposal. This does not replace snapshot history or remove all full-document mutation costs.

## Controlled tab-validation measurements

Evidence: [Build 36447147013, engine job](https://github.com/wieslawsoltes/TextSpace/actions/runs/36447147013/job/109012523132). The harness compares baseline `d6059e2112697baa9e69123de07c2959682710c8` with candidate `2a6271a28974dcdb21a1fac16c661d005616a459`; raw samples are `tab-validation.json` in **TextSpace-engine-validation**.

Environment: Ubuntu 24.04.5 LTS x64, .NET 10.0.12. Twenty thousand validations per sample, three warmups, seven measured batches. Single-tier optimized JIT and ReadyToRun disabled for the benchmark process only; application runtime settings are unchanged. Fixed/random cases verify matching acceptance before timing.

| Stops | Previous median ms | Current median ms | Previous managed bytes/batch | Current managed bytes/batch |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 0.9174 | 0.0836 | 1,280,000 | 0 |
| 1 | 0.9425 | 0.3483 | 4,000,000 | 0 |
| 4 | 3.0682 | 1.2018 | 8,960,512 | 0 |
| 8 | 6.2999 | 2.4390 | 19,520,512 | 0 |
| 32 | 19.4326 | 15.9186 | 41,281,024 | 23,040,512 |
| 128 | 76.7715 | 62.4050 | 203,526,144 | 75,682,560 |

Empty arrays return without allocating a HashSet. Up to eight stops use bounded stack storage and pairwise duplicate checks; larger arrays pre-size their set. Rounded-twip/relative-edge identity, enum/decimal validity, finite bounds and count limits retain their semantics. Oversized serialized arrays fail before their unsupported tail is deserialized.

These are one-runner validation-only observations, not browser FPS, JSON parsing, storage, startup or typing measurements. The initial default-tier run produced nonmonotonic four/eight-stop timings despite allocation reductions; those ratios are not used as improvement claims. The controlled configuration removes compilation-tier transitions as a potential confound. No speed threshold is a CI gate. Runtime allocations on the measured thread may appear in raw samples; report/sample construction is excluded.

```sh
dotnet build benchmarks/TextSpace.Performance -c Release
DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
  dotnet run --project benchmarks/TextSpace.Performance -c Release --no-build -- --tab-validation
```

Recovery transport intentionally pays encoding/hash verification on explicit archive operations. Generated JSON framing keeps leading BOMs inside the payload; UTF-8 byte counts and SHA-256 checks guard both sides. These infrequent safety operations are not part of the validation microbenchmark.

See [Recovery](RECOVERY.md), [validation](RECOVERY-VALIDATION.md), and [general performance](PERFORMANCE.md).
