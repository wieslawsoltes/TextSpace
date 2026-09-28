# Recovery download byte evidence

The framed browser-original transport fixes a leading-marker preservation boundary without changing stored archive format or normalizing expected originals. Subsequent CI evidence was inconsistent: the job endpoint for Build 36449604005 reported a failing decoded-character assertion, while its downloaded validation artifact contained a successful nine-scenario recovery report and byte-identical original/download files. A failed job is not accepted as a successful merge gate.

Two independent artifact inspections were performed before changing any further application behavior:

- Recovery byte diagnostics run 36451330138 / job 109026556620 inspected raw files with Python.
- Run 36451547720 / job 109027270936 checked them with Node 22.23.2, Buffer and an explicit BOM-preserving UTF-8 decoder.

Both original and downloaded fixture files contain 7,088 bytes, start `EF BB BF 7B`, and have SHA-256 `a664b70963438f9155021aec4dd3545a76efd853cc35270dd3407e0bc00b81b9`. Raw byte equality passed. Node's direct readFile, Buffer decoding and explicit TextDecoder all returned U+FEFF as the first character. The artifact's report recorded all nine recovery scenarios passing with no page errors. These observations do not establish a general Node decoding bug or make the contradictory workflow status successful.

The permanent browser test now records each downloaded file's suggested name, raw length, hash, prefix and decoded first code unit. It compares raw source/download buffers and SHA-256, asserts the BOM bytes and expected archive filename, and retains the earlier decoded U+FEFF and full-text equality assertions. The explicit decoder uses `fatal:true, ignoreBOM:true`; it neither inserts a marker nor rewrites file bytes. There are no retries or exception-to-success conversions. Existing real pointer, keyboard and filechooser interactions remain.

All one-time diagnostic workflows are removed from the release tree. The new exact-head full Build, not the inconsistent earlier report, remains mandatory before merge; main Build and public Pages tests are separate deployment gates.
