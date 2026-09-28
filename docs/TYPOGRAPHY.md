# Typography and paragraph pagination

TextSpace 0.3.0-alpha.1 provides reusable point-based tab geometry, discretionary line breaks and paragraph keep controls. It does not claim identical Word pagination or full Unicode bidirectional/line-break conformance.

## Workspace

**Layout → Tabs** edits a pending collection. Choose a position, alignment, leader and decimal separator, then **Set tab stop**. Selecting an existing entry loads it for modification. **Clear tab stop** removes that position, **Clear all tab stops** removes all custom stops, **Apply** commits once, and Cancel leaves the document unchanged. The default interval is document-wide; custom stops apply to selected paragraphs. Points are typographic: 72 points per inch.

The optional right-edge mode represents an inset from the available column's right edge, so generated contents follow resizing and orientation changes. Bar tabs draw vertical rules but do not advance the text cursor. Stops behind the cursor, outside the right text edge, or unable to accommodate the following alignment segment are skipped; the default grid provides a forward fallback. Tabbed lines use explicit stop positions rather than shifting them again for paragraph centering/justification. Very long tab fields can still wrap; this is not full Word tab-overflow semantics.

**Layout → Pagination** and the Paragraph dialog expose Widow/orphan control, Keep with next, Keep lines together, and Page break before. These apply to main-flow paragraphs across pages/columns. Keep with next measures adjacent paragraph chains rather than reserving a fixed guessed height. Impossible constraints (a single line, paragraph or chain taller than the column) degrade to forward flow; they must not create an infinite empty-page loop. Full table-row keep rules and keep relationships across tables/drawings are not implemented.

**Insert → Special Characters** inserts a tab, nonbreaking space U+00A0, nonbreaking hyphen U+2011, optional hyphen U+00AD or zero-width space U+200B. Ctrl+Tab is routed to a literal tab when received by the editor, including table cells; browsers may reserve that shortcut, so the menu is the portable alternative. Ordinary Tab retains cell navigation.

Words can span multiple formatting runs without creating new wrap opportunities. An optional hyphen has zero visible width except at a chosen discretionary break, where a hyphen glyph is rendered. Raw UTF-16 text positions remain unchanged. Emergency wrapping of overlong words uses grapheme boundaries. This is a focused line breaker, not dictionary-based automatic hyphenation or a complete implementation of UAX #14 or UAX #9. Mixed-direction tab semantics remain unsupported.

## Reuse

```csharp
using System.Collections.Immutable;
using TextSpace.Core;

var format = new ParagraphFormat
{
    KeepLinesTogether = true,
    WidowControl = true,
    TabStops = [new TabStop
    {
        Position = 0,
        RelativeToRightEdge = true,
        Alignment = TabAlignment.Right,
        Leader = TabLeader.Dot
    }]
};

editor.SetTabStops(format.TabStops, defaultInterval: 48);
editor.FormatParagraph("Pagination", p => p with
{
    KeepLinesTogether = true,
    WidowControl = true
});
```

`ParagraphFormat.TabStops` is an immutable array of immutable records. Replacing it invalidates cached layout safely; mutable lists would allow stale geometry. Each paragraph supports at most 128 stops, finite positions within ±4,000 points, and distinct position/edge pairs at twip precision. Default intervals are 1–720 points. Invalid edits roll back atomically.

`LayoutChunk.Text` preserves source characters. `DisplayText` optionally substitutes the visible representation; caret arrays map original UTF-16 offsets to rendered positions. `TabLeader` and `LayoutLine.BarTabs` carry non-text decorations through pagination and renderer-independent layout. Cached outputs clone mutable arrays to prevent callers from corrupting later pages.

## Native and DOCX interchange

Native `.textspace` files preserve custom stops, their edge-relative mode and decimal character, default interval, pagination flags and original Unicode text. Existing version-1 native files default to a 36-point interval and enabled widow control.

DOCX writes standard `w:tabs`/`w:tab`, `w:defaultTabStop`, `w:keepNext`, `w:keepLines`, `w:pageBreakBefore`, `w:widowControl`, `w:softHyphen` and `w:noBreakHyphen` elements. Import merges document/style/paragraph tab definitions, including inherited `clear` stops. Explicit false pagination values override inherited true styles. Right-relative positions are resolved using the actual section/table-cell width; standard DOCX does not retain the native edge-relative flag. DOCX decimal tabs use the imported default period, not the complete Word locale-dependent decimal policy. HTML exports preserve text but do not promise custom-tab layout equivalence.

Generated contents retain live REF/PAGEREF fields and add a right-edge dot stop. Leaders are rendering decorations, not inserted dot characters. DOCX uses individual reference fields, not an enclosing Word TOC field.

## Validation

The regression suite includes aligned/decimal tabs across rich runs, right-edge geometry, default grid origins, fallback progress, cache ownership/invalidation, Unicode break mapping, paragraph keeps, DOCX schema validation, inherited clears/defaults, actual section/cell widths, field-backed contents and malformed inputs. Browser tests drive the actual dialogs, keyboard, downloads, DOCX reopening and recovery; the opt-in diagnostic bridge is read-only.

Native builds establish compilation. Chromium acceptance does not establish Safari, Firefox, screen-reader or exhaustive IME/mobile behavior. See [Compatibility](COMPATIBILITY.md) for remaining document areas.
