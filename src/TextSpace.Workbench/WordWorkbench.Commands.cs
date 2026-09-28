using System.Globalization;
using TextSpace.Editor;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private TextStyle? _paintStyle;
    private string? _selectedCommentId;
    private static double ParseNumber(string text)
    {
        text = text.Trim().TrimEnd('p', 't', ' ');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw new InvalidOperationException("Enter a valid number using a decimal point.");
        return number;
    }
    private void RunEdit(string label, Action action)
    {
        try { action(); RefreshFormatting(); Notify(label); }
        catch (Exception ex) { Notify(ex.Message, true); }
        Surface.FocusEditor();
    }
    public async Task ExecuteCommandAsync(string id)
    {
        try
        {
            switch (id)
            {
                case "tab-stops": await TabStopsAsync(); break;
                case "pagination": await PaginationAsync(); break;
                case "undo": Session.Undo(); break;
                case "redo": Session.Redo(); break;
                case "bold": Session.ToggleBold(); break;
                case "italic": Session.ToggleItalic(); break;
                case "underline": Session.ToggleUnderline(); break;
                case "strike": var strike = !Session.TypingStyle.StrikeThrough; Session.FormatText("Strikethrough", s => s with { StrikeThrough = strike }); break;
                case "subscript": var sub = !Session.TypingStyle.Subscript; Session.FormatText("Subscript", s => s with { Subscript = sub, Superscript = false }); break;
                case "superscript": var sup = !Session.TypingStyle.Superscript; Session.FormatText("Superscript", s => s with { Superscript = sup, Subscript = false }); break;
                case "grow": Session.SetFontSize(Session.TypingStyle.FontSize + (Session.TypingStyle.FontSize >= 24 ? 4 : 1)); break;
                case "shrink": Session.SetFontSize(Session.TypingStyle.FontSize - (Session.TypingStyle.FontSize > 24 ? 4 : 1)); break;
                case "clear-format": Session.FormatText("Clear formatting", _ => new()); Session.FormatParagraph("Clear paragraph formatting", _ => new()); break;
                case "format-painter":
                    if (_paintStyle is null) { _paintStyle = Session.TypingStyle; Notify("Select the destination text, then click Format Painter again."); }
                    else { var style = _paintStyle; Session.FormatText("Format Painter", _ => style); _paintStyle = null; }
                    break;
                case "align-left": SetAlignment(TextSpace.Core.TextAlignment.Left); break;
                case "align-center": SetAlignment(TextSpace.Core.TextAlignment.Center); break;
                case "align-right": SetAlignment(TextSpace.Core.TextAlignment.Right); break;
                case "justify": SetAlignment(TextSpace.Core.TextAlignment.Justify); break;
                case "bullets": Session.ToggleList(ListKind.Bullet); break;
                case "numbering": Session.ToggleList(ListKind.Number); break;
                case "indent": Session.Indent(1); break;
                case "outdent": Session.Indent(-1); break;
                case "border-bottom": Session.FormatParagraph("Bottom border", p => p with { BorderBottom = !p.BorderBottom }); break;
                case "format-marks": Surface.ShowFormatting = !Surface.ShowFormatting; Surface.Invalidate(); break;
                case "cut": if (!Session.Selection.IsEmpty) { await Host.WriteClipboardAsync(Session.SelectedText()); Session.InsertText(""); } break;
                case "copy": if (!Session.Selection.IsEmpty) { await Host.WriteClipboardAsync(Session.SelectedText()); Notify("Copied selected text"); } break;
                case "copy-all": await Host.WriteClipboardAsync(Session.Document.PlainText); Notify("Copied document text"); break;
                case "paste": var pasted = await Host.ReadClipboardAsync(); if (pasted is not null) Session.InsertText(pasted); break;
                case "sort": await SortDialogAsync(); break;
                case "font-dialog": await FontDialogAsync(); break;
                case "paragraph-dialog": await ParagraphDialogAsync(); break;
                case "section-settings": await SectionSettingsAsync(); break;
                case "remove-section": Session.RemoveCurrentSectionBreak(); break;
                case "insert-field": await InsertFieldAsync(); break;
                case "manage-fields": await ManageFieldsAsync(); break;
                case "cross-reference": await CrossReferenceAsync(); break;
                case "update-fields": UpdateDocumentFields(); break;
                case "page-setup": await PageSetupDialogAsync(); break;
                case "new": await NewDocumentAsync(new DocumentModel()); break;
                case "open": await OpenDocumentAsync(); break;
                case "save": case "save-as": await ExportAsync("textspace"); break;
                case "export-docx": await ExportAsync("docx"); break;
                case "export-pdf": await ExportAsync("pdf"); break;
                case "export-html": await ExportAsync("html"); break;
                case "export-text": await ExportAsync("txt"); break;
                case "export-png": await ExportAsync("png"); break;
                case "print": await PrintAsync(); break;
                case "rename": await RenameAsync(); break;
                case "find": _navigationMode = "Results"; SetNavigation(true); _searchField?.Focus(FocusState.Programmatic); return;
                case "replace": _navigationMode = "Replace"; SetNavigation(true); _searchField?.Focus(FocusState.Programmatic); return;
                case "comments": SetReview(!_reviewVisible || _reviewMode != "Comments", "Comments"); return;
                case "changes": SetReview(true, "Changes"); return;
                case "proofing": SetReview(true, "Editor"); return;
                case "new-comment": await NewCommentAsync(); break;
                case "delete-comment": if (CurrentComment() is { } comment) Session.DeleteComment(comment.Id); break;
                case "previous-comment": NavigateComment(-1); break;
                case "next-comment": NavigateComment(1); break;
                case "track": Session.TrackChanges = !Session.TrackChanges; Session.Notify(EditorChangeKind.View); Notify(Session.TrackChanges ? "Tracking local text edits" : "Track Changes off"); break;
                case "accept-change": if (CurrentChange() is { } accepted) Session.AcceptChange(accepted.Id); break;
                case "reject-change": if (CurrentChange() is { } rejected) Session.RejectChange(rejected.Id); break;
                case "reject-all":
                    var rejectedCount = 0;
                    foreach (var change in Session.Document.Changes.ToArray().Reverse()) { if (!change.CanReject) continue; Session.RejectChange(change.Id); rejectedCount++; }
                    Notify($"Rejected {rejectedCount} safe changes; overlapping changes remain for review."); break;
                case "compare": await CompareAsync(); return;
                case "word-count": await WordCountAsync(); break;
                case "zoom": await ZoomDialogAsync(); break;
                case "zoom-100": Surface.SetZoom(1); break;
                case "one-page": Surface.FitWholePage(); break;
                case "page-width": Surface.FitPageWidth(); break;
                case "go-to": await GoToAsync(); break;
                case "focus": ToggleFocus(); break;
                case "read-mode": Session.IsReadOnly = true; Ribbon.IsCollapsed = true; Notify("Reading mode · choose Editing to make changes"); break;
                case "editing": case "print-layout": Session.IsReadOnly = false; Ribbon.IsCollapsed = false; Notify("Editing mode"); break;
                case "reviewing": Session.IsReadOnly = false; Session.TrackChanges = true; Ribbon.IsCollapsed = false; SetReview(true, "Changes"); break;
                case "escape": if (_backstage is not null) HideBackstage(); if (_focusMode) ToggleFocus(); break;
                case "blank-page": Session.InsertPageBreak(); Session.InsertPageBreak(); break;
                case "page-break": Session.InsertPageBreak(); break;
                case "insert-picture": await InsertPictureAsync(); break;
                case "link": await HyperlinkDialogAsync(); break;
                case "header": await HeaderFooterAsync(true); break;
                case "footer": await HeaderFooterAsync(false); break;
                case "date": await DateDialogAsync(); break;
                case "watermark": await WatermarkAsync(); break;
                case "toc": await InsertContentsAsync(false); break;
                case "update-toc": await InsertContentsAsync(true); break;
                case "caption": await CaptionAsync(); break;
                case "merge-recipients": await SelectRecipientsAsync(); return;
                case "merge-preview": await PreviewMergeAsync(); return;
                case "merge-finish": await FinishMergeAsync(); break;
                case "table-row": Session.AddTableRow(); break;
                case "table-column": Session.AddTableColumn(); break;
                case "table-properties": await TablePropertiesAsync(); break;
                case "picture-selected": Ribbon.SelectTab("Picture Format"); return;
                case "picture-size": await PictureSizeAsync(); break;
                case "picture-alt": await PictureAltAsync(); break;
                case "delete-picture": RemovePicture(); break;
                case "command-search": await CommandSearchAsync(); return;
                case "shortcuts": await ShortcutsAsync(); break;
                case "help": await HelpAsync(); break;
                case "about": await AboutAsync(); break;
                case "feedback": await Host.OpenUriAsync("https://github.com/wieslawsoltes/TextSpace/issues"); break;
                default: throw new InvalidOperationException("Unknown command: " + id);
            }
            RefreshStatus(); RefreshFormatting(); Surface.FocusEditor();
        }
        catch (Exception ex) { Notify(ex.Message, true); Surface.FocusEditor(); }
    }
    private void SetAlignment(TextSpace.Core.TextAlignment alignment) => Session.FormatParagraph("Paragraph alignment", p => p with { Alignment = alignment });
    private void ToggleFocus()
    {
        _focusMode = !_focusMode;
        if (_titleBar is not null) _titleBar.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        Ribbon.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        if (_focusMode) { SetNavigation(false); SetReview(false, _reviewMode); }
        Notify(_focusMode ? "Focus mode · press Escape to return" : "Print layout");
    }
    private ImageBlock? SelectedPicture() => Session.Document.Blocks.OfType<ImageBlock>().FirstOrDefault(i => i.Id == Surface.SelectedImageId);
    private void RemovePicture()
    {
        var image = SelectedPicture(); if (image is null) return;
        Session.Execute("Remove picture", () => Session.Document.Blocks.Remove(image)); Ribbon.SelectTab("Home");
    }
    private CommentThread? CurrentComment() => Session.Document.Comments.FirstOrDefault(c => c.Id == _selectedCommentId) ?? Session.Document.Comments.FirstOrDefault(c => Session.Selection.Active >= c.Start && Session.Selection.Active <= c.End) ?? Session.Document.Comments.FirstOrDefault();
    private TrackedEdit? CurrentChange() => Session.Document.Changes.OrderBy(c => Math.Abs(c.Start - Session.Selection.Active)).FirstOrDefault();
    private void NavigateComment(int direction)
    {
        var comments = Session.Document.Comments.Where(c => !c.Resolved).OrderBy(c => c.Start).ToArray(); if (comments.Length == 0) return;
        var current = Array.FindIndex(comments, c => c.Id == _selectedCommentId); var index = (current + direction + comments.Length) % comments.Length; var comment = comments[index];
        _selectedCommentId = comment.Id; Session.SetSelection(comment.Start, comment.End); SetReview(true, "Comments");
    }
}
