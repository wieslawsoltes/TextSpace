# Bookmarks and document navigation

TextSpace bookmarks are named points or ranges in the document's UTF-16 coordinate space. Their endpoints are validated against Unicode grapheme boundaries. Names are case-insensitive, contain up to 40 ASCII letters/digits/underscores, and begin with a letter or underscore. Leading-underscore names are hidden from the default picker but can be shown explicitly.

## Workspace

Select text or place the caret, then open **Insert → Navigation → Bookmark**. Add a new name, move an existing name to the selection with **Add or update**, rename a selected entry, delete it, or navigate with **Go To**. A rename updates matching internal hyperlinks in the same undo transaction. Navigation remains available in reading mode; mutation does not.

Select link text and use **Insert → Link** with an address such as `#Destination`. Use **Open Link** or Control/Command-click the rendered hyperlink to follow it. HTTP, HTTPS and mailto links open through the host's explicit URI service. Unsupported schemes are rejected.

## Engine reuse

```csharp
var editor = new EditorSession(DocumentJson.FromText("Introduction\nDetails"));
editor.SetSelection(13, 20);
editor.SetBookmark("Details");
editor.SetSelection(0, 12);
editor.FormatText("Link", s => s with { Hyperlink = "#Details", Underline = true });
editor.RenameBookmark("Details", "TechnicalDetails");
editor.GoToBookmark("TechnicalDetails");
```

Text edits transform bookmark endpoints. Point bookmarks retain right insertion gravity and remain collapsed. Ranges expand for inserted text inside them. Table structural edits preserve offsets in surviving paragraph identities; ranges inside removed cells collapse toward surviving content. Undo/redo restores both text and bookmark state.

Native `.textspace` files persist bookmark IDs, names, and endpoints. Older files without a bookmarks collection remain readable. DOCX exports use actual `w:bookmarkStart`/`w:bookmarkEnd` markup and `w:hyperlink w:anchor`; import restores supported bookmark ranges. Invalid, duplicate, or unmatched imported bookmarks generate warnings. HTML export emits named targets and safe links, escapes CSS font strings, validates color values, and includes a restrictive content-security policy.

This does not add Word cross-reference fields, outline-linked automatic bookmarks, full section stories, or lossless handling of bookmarks around unsupported document structures.
