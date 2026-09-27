# Font assets

Run `python3 scripts/fetch-assets.py` before building. Font binaries are not committed.

Inter supplies the UI; Carlito supplies the default office sans-serif text; Tinos supplies serif substitutions; Cousine supplies monospaced substitutions. These fonts are distributed under their upstream SIL Open Font License files, fetched alongside the binaries. The asset manifest records source URLs, SHA-256 hashes and sizes.

Aptos, Calibri, Arial, Times New Roman, Georgia and Courier New remain document font-family metadata. When the requested proprietary font is unavailable, the renderer uses the mapped open-source substitute. This is not a claim of exact Aptos metrics or Microsoft Word pagination. No Microsoft font files are included.
