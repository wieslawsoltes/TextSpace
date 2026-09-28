# Continuous sections and physical-page regions

TextSpace 0.5.0-alpha.1 adds continuous and next-column section starts. The model retains the requested break kind in native documents and WordprocessingML rather than converting it during import.

## Editing

Use **Layout → Section Break → Continuous Section** to change formatting without a forced physical page break. Use **Next Column Section** to begin a section in the next column of the current grid. Paper size, margins and columns apply to the selected section. **Section Settings → Section start** changes an existing boundary; changing the start and other settings is one undo transaction.

A common layout is one-column introduction → continuous two-column body → continuous one-column conclusion. Paragraph-only multi-column bands that end at a continuous boundary are balanced when the entire band fits the remaining physical page. Lines remain in source order; keep-lines, keep-with-next and widow/orphan pairs constrain balancing.

## Model and layout contract

`SectionBreakKind` appends `Continuous` and `NextColumn`; existing enum values are unchanged. `LayoutPage` describes physical paper and its header/footer owner. `LayoutRegion` describes one section on that page, its column range, used vertical extent, displayed page number, section page index and section page count. `LayoutLine.Region` and `ColumnIndex` associate paragraphs and table-cell lines with their actual flow region.

`DocumentLayout.FieldPageAt(position)` resolves `PAGE`, `SECTION`, `SECTIONPAGES` and `PAGEREF` using the containing line's region. A shared page counts once for each section that occupies it. Section numbering can restart on the shared page; subsequent physical pages continue that numbering. `NUMPAGES` remains the physical document-page count. Empty attached regions are removed when their first actual content must move to the next page.

The ruler uses the selected text column's left edge and width. Hit testing and caret indexing still use canonical text coordinates; repeated table-header replicas do not become additional document text. New layouts own new regions even when paragraph geometry is reused from the cache.

## Physical constraints and explicit limits

A continuous start shares paper only when width and height agree. Different paper dimensions promote it to a physical-page start, retaining the requested kind and producing a `LayoutNotice`. Next-column starts additionally require matching margins, column count and gap. The last column advances to a new page. A new continuous band starts below every previously occupied column, including columns belonging to earlier next-column sections.

A physical page has one background, watermark, header and footer. On a shared page these remain owned by its first section region; subsequent pages use the active section's settings. Consequently a body PAGE field with a section-numbering restart can differ from the first region's footer on the same sheet. This policy is explicit and is not a claim of full Microsoft Word compatibility-mode behavior.

Balancing is bounded to 1,000 consecutive main-flow paragraphs / 50,000 measured lines and a 32-step packing search. It does not rebalance bands containing explicit page/column breaks, tables, pictures, page-break-before paragraphs, or content too tall for the remaining page. These retain normal sequential flow without dropping or reordering content. Full Word multi-object balancing, footnote interactions, last-section automatic balancing, rich independent stories and every compatibility-mode pagination rule remain outside this implementation.

## Interchange

DOCX emits and imports `w:type` values `continuous` and `nextColumn` in the appropriate section properties. Page settings, story inheritance and numbering remain associated with their sections. Tests validate generated packages using the Open XML SDK and reimport their text and break types.

HTML preserves section-start metadata. Same-paper continuous sections reuse the same named CSS page with `break-before:auto`, avoiding an accidental print page break caused solely by a changed named-page style. Next-column starts use `break-before:column`, but independent HTML section containers do not reproduce the native column-flow/field/layout engine exactly. Export PDF for the exact current Skia page geometry.

## Reference material

- Microsoft, “Insert a section break”: https://support.microsoft.com/en-us/word/insert-a-section-break
- Microsoft, “Use section breaks to change the layout or formatting”: https://support.microsoft.com/en-us/word/use-section-breaks-to-change-the-layout-or-formatting-in-one-section-of-your-word-document

These references describe the intended product semantics. The explicit limits above describe what TextSpace actually implements.
