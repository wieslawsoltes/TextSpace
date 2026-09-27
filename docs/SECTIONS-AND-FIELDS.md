# Sections, fields, and live contents

## Section workflow

Place the caret outside a table and choose **Layout → Section Break → Next Page Section**, **Odd Page Section**, or **Even Page Section**. The following content becomes a new section. Odd/even starts may insert a blank physical page; changing the displayed numbering does not change that physical-page parity. A column break advances to the next column, or a new page when no column remains.

Page Setup, Orientation, Margins, Columns, and Paper Size now apply to the section containing the caret. Each section has its own paper dimensions; the editor centers narrower pages and measures scrolling from cumulative page heights. The ruler follows the active page. PDF page boxes and PNG dimensions match the selected section.

**Section Settings** controls first/even/default header and footer text, independent links to the preceding section, numbering continuation or restart, and decimal/Roman/alphabetic display. The first-page variant takes precedence over even-page selection. A checked link inherits the preceding corresponding story; an unchecked link with empty text creates a blank story. The first section cannot link to a nonexistent predecessor.

Story text wraps within the page margins and accepts `{PAGE}`, `{NUMPAGES}`, `{SECTION}`, `{SECTIONPAGES}`, `{TITLE}`, and `{AUTHOR}`. These placeholders are evaluated at rendering time. Text beyond the available header/footer margin is clipped rather than painting over body content. Rich header/footer runs, tables, pictures, footnotes, and independently editable story trees are not implemented.

**Remove Section Break** removes the boundary before the current section and adopts the previous section's settings while retaining text. This explicit command is not a claim of parity with Word's formatting behavior when deleting a section-break character. Undo restores the boundary and formatting.

## Field workflow

Use **Insert → Field** to type an instruction or choose a common field. Field code is not displayed as literal document text; the document contains its cached result. Press **F9** with the document focused, or choose **Update Fields**, to refresh the results. Page fields use the current paginated layout, including section numbering.

**Manage Fields** shows cached text and instructions. You can navigate to a field, edit its code, lock/unlock its result, or unlink it. Lock preserves the cached result during updates; unlink removes the instruction but retains editable text. An ordinary text edit inside a cached result also unlinks the field. Typing exactly at its boundaries is outside the field. Formatting alone retains it.

| Instruction | Result |
| --- | --- |
| `TITLE`, `AUTHOR`, `SUBJECT` | Current document metadata |
| `FILENAME` | Current document title plus `.docx`; this is not an operating-system path |
| `NUMWORDS`, `NUMCHARS` | Current model word count and non-whitespace character count |
| `DATE`, `TIME` | Evaluation-time date/time |
| `CREATEDATE`, `SAVEDATE` | Stored creation/modification timestamp |
| `PAGE`, `NUMPAGES` | Displayed page number / physical document page count |
| `SECTION`, `SECTIONPAGES` | One-based section number / physical pages assigned to that section |
| `REF BookmarkName` | Current bookmark text |
| `PAGEREF BookmarkName` | Displayed page number of the bookmark start |
| `SEQ Figure` | Next number in the named sequence |

Supported numeric switches are `\* Arabic`, `\* ROMAN`, `\* roman`, `\* ALPHABETIC`, and `\* alphabetic`. Roman values above 3,999 fall back to decimal; alphabetic sequences continue as AA, AB, and so on. `SEQ Figure \r 10` restarts at ten; `SEQ Figure \c` repeats the current sequence value. `REF Details \h` and `PAGEREF Details \h` create navigable internal hyperlinks.

Dates accept `\@ "yyyy-MM-dd"` or another supported **.NET date/time format**, not every Word date-picture convention. `MERGEFORMAT` and `CHARFORMAT` switches are accepted without implementing additional Word-specific formatting semantics.

Evaluation never launches processes, runs scripts, reads local files, or retrieves external content. Unknown imported instructions are retained with cached text, skipped by the evaluator, and exported locked. This lock is not a sandbox in another application: do not unlock or update untrusted external fields after exporting them into a different editor.

## Live table of contents

Apply Heading styles, place the insertion point outside the source heading text, and choose **References → Table of Contents**. The command creates hidden `_TextSpaceToc_…` bookmarks and live REF/PAGEREF entries. F9 refreshes entry text and displayed pages after edits. **Update Table** rebuilds the first generated contents block to include the current heading list.

Insertion/rebuild is one undo transaction and refuses a selection that would remove its own heading sources. At most 1,000 nonempty heading paragraphs can be included. The generated entries remain editable, but editing inside a field result unlinks it as described above. DOCX exports these standard individual reference fields, not an enclosing Word TOC field. Full right-aligned dot-leader tab stops are not yet implemented.

## Embedding API

```csharp
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Skia;

var editor = new EditorSession(DocumentJson.FromText("Introduction\n", "Report"));
using var renderer = new DocumentRenderer();
FieldContext Context(DocumentModel document) => new()
{
    PageAt = renderer.Layout(document).FieldPageAt,
    CultureName = "en-US"
};

editor.SetSelection(editor.Index.Length, editor.Index.Length);
editor.InsertSectionBreak(SectionBreakKind.OddPage);
editor.SetPage(page => page.Landscape());
editor.SetSection(section => section with
{
    Footer = "Section {SECTION} · {PAGE} / {SECTIONPAGES}",
    Options = section.Options with
    {
        PageNumberStart = 1,
        NumberStyle = PageNumberStyle.LowerRoman
    }
});

var fieldId = editor.InsertField("PAGE", Context);
editor.UpdateFields(Context);
editor.LockField(fieldId, true);
// editor.UnlinkField(fieldId) keeps its result while removing live behavior.
```

The pure `FieldEngine.Evaluate` accepts a fixed `FieldContext.Now` for deterministic evaluation. Session updates hold a fixed timestamp across their pagination passes. A field result is limited to 64,000 characters, instructions to 1,024 characters, field count to 10,000, dependency recursion to 64, and update stabilization to eight passes. Failure to stabilize rolls the whole update back. Changed field results do not create tracked-text revision noise, but updates remain undoable.

## Interchange and remaining boundaries

Native version-1 JSON remains readable: absent fields/section-options receive defaults. Older TextSpace builds do not understand new section/column block discriminators; keep copies before opening a new document with an older build.

DOCX uses paragraph-level section properties plus final body section properties, header/footer relationships, and actual simple/complex field markers. Supported cached-result character formatting and bookmark boundaries are retained. Missing story relationships preserve link-to-previous semantics. `evenAndOddHeaders` is document-wide in WordprocessingML; export materializes even stories where necessary to retain a section's current appearance.

Continuous and next-column **section starts** are normalized to next-page section starts with import warnings; explicit column-break blocks are supported separately. Nested or cross-paragraph complex fields are normalized to supported visible content with warnings. Advanced section line numbering, gutter/mirrored margins, rich independent stories, tracked field-code changes, formula/IF fields, arbitrary field providers, full TOC grammar, and exact Word pagination are not implemented. HTML is a static cached-result export; browser named-page CSS support and printer behavior can vary. PDF is the deterministic mixed-page-size output.

Primary format reference: [Microsoft SectionProperties](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.sectionproperties), [SectionType](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.sectiontype), and [SimpleField](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.simplefield).
