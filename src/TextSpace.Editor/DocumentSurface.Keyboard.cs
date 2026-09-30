using TextSpace.Core;

namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    private bool _ownsNativeInput;
    private int _inputDispatch;

    private static bool IsDocumentKey(VirtualKey key, bool control) => control
        ? key is VirtualKey.B or VirtualKey.I or VirtualKey.U or VirtualKey.Z or VirtualKey.Y
            or VirtualKey.A or VirtualKey.S or VirtualKey.O or VirtualKey.N or VirtualKey.P
            or VirtualKey.F or VirtualKey.H or VirtualKey.K or VirtualKey.E or VirtualKey.L
            or VirtualKey.R or VirtualKey.J or VirtualKey.Enter or VirtualKey.Home or VirtualKey.End
            or VirtualKey.Left or VirtualKey.Right or VirtualKey.Back or VirtualKey.Delete or VirtualKey.Tab
        : key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right
            or VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown
            or VirtualKey.Back or VirtualKey.Delete or VirtualKey.Tab or VirtualKey.Enter or VirtualKey.Escape or VirtualKey.F9;

    private void FinishInputCommand()
    {
        if (!_ownsNativeInput) return;
        _ownsNativeInput = false;
        SyncInput();
    }

    // Key-up follows all synchronous native post-key handlers. Restoring here
    // makes the input ready for a following IME/beforeinput event even when the
    // dispatcher has not yet run the deferred restoration.
    private void OnInputKeyUp(object sender, KeyRoutedEventArgs e) => FinishInputCommand();

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var dispatch = ++_inputDispatch;
        // Never overwrite pending native character input with an older model
        // snapshot: TextChanged is asynchronous. Only restore a buffer previously
        // owned by a document command, and commit pending text before a new command.
        if (_ownsNativeInput) FinishInputCommand();
        var control = ControlDown();
        if (TryVisualKey(e)) return;
        if (KeyDown(VirtualKey.Menu) && e.Key == (VirtualKey)187) { CommandRequested?.Invoke("insert-equation"); e.Handled = true; return; }
        if (!IsDocumentKey(e.Key, control)) return;
        CommitNativeInput();
        var shift = KeyDown(VirtualKey.Shift); var position = Session.Selection.Active; var index = Session.Index;
        void Move(int target) { Session.SetSelection(shift ? Session.Selection.Anchor : target, target); SyncInput(); _caretVisible = true; }
        try
        {
            if (control)
            {
                switch (e.Key)
                {
                    case VirtualKey.B: Session.ToggleBold(); break;
                    case VirtualKey.I: Session.ToggleItalic(); break;
                    case VirtualKey.U: Session.ToggleUnderline(); break;
                    case VirtualKey.Z: if (shift) Session.Redo(); else Session.Undo(); break;
                    case VirtualKey.Y: Session.Redo(); break;
                    case VirtualKey.A: Session.SelectAll(); break;
                    case VirtualKey.S: CommandRequested?.Invoke(shift ? "save-as" : "save"); break;
                    case VirtualKey.O: CommandRequested?.Invoke("open"); break;
                    case VirtualKey.N: CommandRequested?.Invoke("new"); break;
                    case VirtualKey.P: CommandRequested?.Invoke("print"); break;
                    case VirtualKey.F: CommandRequested?.Invoke("find"); break;
                    case VirtualKey.H: CommandRequested?.Invoke("replace"); break;
                    case VirtualKey.K: CommandRequested?.Invoke("link"); break;
                    case VirtualKey.E: Session.FormatParagraph("Center", p => p with { Alignment = TextSpace.Core.TextAlignment.Center }); break;
                    case VirtualKey.L: Session.FormatParagraph("Align left", p => p with { Alignment = TextSpace.Core.TextAlignment.Left }); break;
                    case VirtualKey.R: Session.FormatParagraph("Align right", p => p with { Alignment = TextSpace.Core.TextAlignment.Right }); break;
                    case VirtualKey.J: Session.FormatParagraph("Justify", p => p with { Alignment = TextSpace.Core.TextAlignment.Justify }); break;
                    case VirtualKey.Tab: Session.InsertText("\t"); break;
                    case VirtualKey.Enter: Session.InsertPageBreak(); break;
                    case VirtualKey.Home: Move(0); break;
                    case VirtualKey.End: Move(index.Length); break;
                    case VirtualKey.Left: Move(Session.WordBoundary(position, -1)); break;
                    case VirtualKey.Right: Move(Session.WordBoundary(position, 1)); break;
                    case VirtualKey.Back: var previous = Session.WordBoundary(position, -1); Session.Replace(previous, position - previous, "", "Delete word"); break;
                    case VirtualKey.Delete: var next = Session.WordBoundary(position, 1); Session.Replace(position, next - position, "", "Delete word"); break;
                    default: return;
                }
                e.Handled = true; return;
            }
            switch (e.Key)
            {
                case VirtualKey.F9: CommandRequested?.Invoke("update-fields"); break;
                case VirtualKey.Up:
                case VirtualKey.Down:
                    var caret = Layout.Caret(position); _desiredX ??= caret.X + Layout.PageLeft(caret.PageIndex);
                    Move(Layout.VerticalMove(position, (e.Key == VirtualKey.Up ? -1 : 1) * caret.Height, _desiredX)); break;
                case VirtualKey.Left: _desiredX = null; Move(!shift && !Session.Selection.IsEmpty ? Session.Selection.Start : index.Previous(position)); break;
                case VirtualKey.Right: _desiredX = null; Move(!shift && !Session.Selection.IsEmpty ? Session.Selection.End : index.Next(position)); break;
                case VirtualKey.Home: _desiredX = null; Move(Layout.Lines.LastOrDefault(l => position >= l.Start && position <= l.End)?.Start ?? 0); break;
                case VirtualKey.End: _desiredX = null; Move(Layout.Lines.LastOrDefault(l => position >= l.Start && position <= l.End)?.End ?? index.Length); break;
                case VirtualKey.PageUp: Move(Layout.VerticalMove(position, -Math.Max(20, _canvas.ActualHeight / Scale - 20))); break;
                case VirtualKey.PageDown: Move(Layout.VerticalMove(position, Math.Max(20, _canvas.ActualHeight / Scale - 20))); break;
                case VirtualKey.Back: Session.DeleteBackward(); break;
                case VirtualKey.Delete: Session.DeleteForward(); break;
                case VirtualKey.Tab: if (!Session.MoveTableCell(shift)) Session.InsertText("\t"); break;
                case VirtualKey.Enter:
                    if (shift) Session.InsertText("\u2028");
                    else if (Session.CurrentParagraph.Length == 0 && Session.CurrentParagraph.Format.List != ListKind.None) Session.FormatParagraph("End list", p => p with { List = ListKind.None });
                    else Session.InsertText("\n");
                    break;
                case VirtualKey.Escape: SelectedImageId = null; CommandRequested?.Invoke("escape"); break;
                default: return;
            }
            e.Handled = true;
        }
        catch (Exception ex) { Error?.Invoke(ex.Message); e.Handled = true; SyncInput(); }
        finally
        {
            _ownsNativeInput = e.Handled;
            if (_ownsNativeInput)
            {
                // Post-key processing is synchronous but occurs after routed
                // handlers. Prevent its text/selection notifications from becoming
                // a second document edit, then restore the native input buffer.
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_disposed || dispatch != _inputDispatch) return;
                    FinishInputCommand();
                });
            }
        }
    }
}
