using System.Text;
using TextSpace.Storage;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private bool _recoveryToolsAdded;
    public void EnableRecoveryTools()
    {
        if (_recoveryToolsAdded || Host is not IRecoveryArchiveStore) return;
        _recoveryToolsAdded = true;
        Ribbon.AddTab("Recovery", () =>
        [
            Group("Document Recovery",
                new RibbonButton("open", "Open and Repair", () => _ = RecoveryActionAsync(OpenAndRepairAsync), large: true),
                new RibbonButton("history", "Protected Originals", () => _ = RecoveryActionAsync(ProtectedOriginalsAsync), large: true))
        ]);
    }

    public void ShowRecoveryNotice(string message) => Notify(message, true);

    private async Task RecoveryActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { Notify("Recovery action cancelled; the original was not removed."); }
        catch (Exception ex) { Notify(ex.Message, true); }
    }

    private async Task OpenAndRepairAsync()
    {
        var file = await Host.OpenFileAsync(".textspace"); if (file is null) return;
        if (file.Bytes.Length > DocumentJson.MaxFileBytes) throw new InvalidDataException("The native file exceeds 32 MB.");
        // Preserve the UTF-8 BOM in the protected original. The repair parser
        // handles that marker on a separate parseable copy, never this payload.
        var original = new UTF8Encoding(false, true).GetString(file.Bytes);
        var plan = DocumentRecovery.PrepareTabRepair(original);
        var dialog = new OfficeDialog("Open and Repair", "Protect original and open", 560);
        dialog.AddDescription("This operation creates a copy. Your source file is not changed. The original JSON is also protected separately from rolling AutoSave history before the copy opens.");
        dialog.AddDescription($"{plan.Issues.Length} affected paragraphs; {plan.RemovedStops} invalid, duplicate or excess tab stops will be removed. Text and other document metadata are retained. A maximum of 128 valid unique tab stops is retained per paragraph, in original order.");
        foreach (var issue in plan.Issues.Take(12)) dialog.AddDescription($"Paragraph {issue.ParagraphId}: {issue.OriginalCount} -> {issue.RetainedCount} stops; invalid {issue.InvalidCount}, duplicates {issue.DuplicateCount}, excess {issue.ExcessCount}.");
        if (!await ShowDialogAsync(dialog)) return;
        var archive = await ((IRecoveryArchiveStore)Host).ProtectAsync(original);
        var document = plan.CreateDocument(); document.Id = Guid.NewGuid().ToString("N"); document.Title += " (recovered)";
        await NewDocumentAsync(document);
        Notify("Original protected as " + archive.Id[..12] + ". Open Recovery > Protected Originals to download it.", true);
    }

    private async Task ProtectedOriginalsAsync()
    {
        var store = (IRecoveryArchiveStore)Host;
        var entries = await store.ListProtectedAsync();
        var dialog = new OfficeDialog("Protected Originals", "Close", 560);
        dialog.AddDescription("Original native JSON saved before recovery actions. These copies are checksum-verified and not evicted by AutoSave. Download copies for independent backup; browser or device storage can still be cleared externally.");
        if (entries.Count == 0) dialog.AddDescription("There are no protected originals on this device.");
        foreach (var entry in entries)
        {
            var button = new OfficeButton($"Download original {entry.Id[..12]} · {entry.SavedAt.LocalDateTime:g} · {entry.Bytes / 1024d:0.#} KB", () => { });
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try
                {
                    var original = await store.ReadProtectedAsync(entry.Id) ?? throw new InvalidDataException("The protected original is missing.");
                    await Host.SaveFileAsync("TextSpace-original-" + entry.Id[..12] + ".textspace", Encoding.UTF8.GetBytes(original), "application/vnd.textspace+json");
                }
                catch (Exception ex) { Notify(ex.Message, true); }
                finally { button.IsEnabled = true; }
            };
            dialog.Body.Children.Add(button);
        }
        await ShowDialogAsync(dialog);
    }
}
