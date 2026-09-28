# Protected-original transport

The BOM-preservation browser scenario exposed a boundary that native archive tests did not cover: a repaired file opened successfully, but the protected archive was not stored under the SHA-256 of the BOM-inclusive original. Existing preservation assertions were retained rather than normalizing the expected source.

Original transport now uses `RecoveryArchiveTransport`, a source-generated JSON envelope containing the original string, its strict UTF-8 length and SHA-256. The envelope starts with JSON syntax, so a leading payload U+FEFF is not a transport encoding signature. Browser `protectPayload` verifies length/hash before starting archive writes. `readPayload` verifies the stored original and returns a framed payload; managed code verifies it against the requested archive identity before a download.

This avoids depending on raw-string BOM handling in runtime interop. The .NET 10 runtime source initializes a UTF-16 `TextDecoder` without an `ignoreBOM` override in `src/mono/browser/runtime/strings.ts`; the framing does not patch the runtime or globally alter browser decoders. The actual Release browser test, not only inspection of runtime source, is the end-to-end preservation gate.

The archive format itself is unchanged. Existing entries remain raw original strings keyed by SHA-256. There is no migration, automatic deletion, or checksum rewrite. The UI still archives before active-recovery replacement. A transport mismatch fails closed. Repeated BOM characters, empty text, quotes, control characters, supplementary characters, invalid Unicode and altered envelope identities/counts have managed regression coverage. The browser suite also repairs a BOM-prefixed real file and downloads the protected original for byte-for-byte comparison.

This is a preservation transport, not encryption, authentication against a malicious browser owner, or an independent backup. Local storage and the application share the user's browser trust boundary.
