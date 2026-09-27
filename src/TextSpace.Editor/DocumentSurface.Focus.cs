using Microsoft.UI.Xaml.Controls.Primitives;

namespace TextSpace.Editor;

public sealed partial class DocumentSurface
{
    private readonly HashSet<Popup> _focusWaiters = [];

    /// <summary>Wait for native flyout focus restoration instead of fighting it.</summary>
    private bool DeferFocusUntilPopupsClose()
    {
        if (XamlRoot is null) return false;
        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
        if (popups.Count == 0) return false;
        foreach (var popup in popups)
        {
            if (!_focusWaiters.Add(popup)) continue;
            EventHandler<object>? closed = null;
            closed = (_, _) =>
            {
                popup.Closed -= closed;
                _focusWaiters.Remove(popup);
                if (!_disposed) DispatcherQueue.TryEnqueue(FocusEditor);
            };
            popup.Closed += closed;
        }
        return true;
    }
}
