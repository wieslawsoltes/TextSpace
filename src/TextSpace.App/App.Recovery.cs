using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TextSpace.Controls;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Storage;
using Windows.Foundation;

namespace TextSpace.App;

public sealed partial class App
{
    private DispatcherTimer? _recoveryTimer;

    private void ShowRecoveryCenter(string? original, Exception failure)
    {
        _recoveryTimer?.Stop();
        Console.Error.WriteLine(failure);
        var status = OfficeTheme.Text("", 12, OfficeTheme.Muted); status.TextWrapping = TextWrapping.Wrap;
        var details = OfficeTheme.Text("TextSpace could not start from the saved workspace. " + failure.Message, 14, "#A4262C"); details.TextWrapping = TextWrapping.Wrap;
        var description = OfficeTheme.Text("Your recovery data has not been cleared. Download the original, preview a repair, restore an earlier version, or explicitly protect the original and start a new document.", 12, OfficeTheme.Muted); description.TextWrapping = TextWrapping.Wrap;
        var panel = new StackPanel { Spacing = 14, Padding = new(28), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { OfficeTheme.Text("Recover your document", 28, OfficeTheme.Accent, true), details, description } };
        var actions = new StackPanel { Spacing = 8 }; panel.Children.Add(actions); panel.Children.Add(status);
        var root = new Grid { Background = OfficeTheme.Brush("#F5F7FA"), Children = { new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Center } } };
        _window!.Content = root;
        RecoveryRepairPlan? plan = null;
        async Task Run(Func<Task> action)
        {
            actions.IsHitTestVisible = false;
            foreach (var child in actions.Children.OfType<Control>()) child.IsEnabled = false;
            try { await action(); }
            catch (OperationCanceledException) { status.Text = "Cancelled. The original recovery has not been removed."; }
            catch (Exception ex) { status.Text = ex.Message; }
            finally
            {
                actions.IsHitTestVisible = true;
                foreach (var child in actions.Children.OfType<Control>()) child.IsEnabled = true;
            }
        }
        void Button(string label, Func<Task> action)
        {
            var button = new OfficeButton { Content = label, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(12, 8), BorderThickness = new(1) };
            AutomationProperties.SetName(button, label);
            button.Click += async (_, _) => await Run(action);
            actions.Children.Add(button);
        }
        async Task Adopt(DocumentModel document)
        {
            if (original is null || _host is not IRecoveryArchiveStore archives)
                throw new InvalidOperationException("The original must be readable and protected before active recovery can be replaced.");
            var archive = await archives.ProtectAsync(original);
            // This await is the safety boundary: do not construct an auto-saving
            // workbench until protection has committed successfully.
            document.Id = Guid.NewGuid().ToString("N");
            await OpenWorkbenchAsync(document, "Original protected as " + archive.Id[..12] + ". Recovery > Protected Originals downloads the unchanged JSON. Keep an external backup.");
            await _workbench!.SaveRecoveryAsync();
        }
        if (original is not null && _host is not null)
        {
            Button("Download original recovery", () => _host.SaveFileAsync("TextSpace-original-recovery.textspace", Encoding.UTF8.GetBytes(original), "application/vnd.textspace+json"));
            Button("Preview tab-stop repair", () =>
            {
                plan = DocumentRecovery.PrepareTabRepair(original);
                status.Text = $"Repair preview: {plan.Issues.Length} affected paragraphs; {plan.RemovedStops} invalid, duplicate or excess tab stops would be removed. Text and other metadata are retained. Only the first 128 valid unique stops per paragraph are retained; tab positioning may change. Nothing has been saved or replaced.";
                return Task.CompletedTask;
            });
            Button("Protect original and open repaired copy", async () =>
            {
                if (plan is null) throw new InvalidOperationException("Choose Preview tab-stop repair first and review its changes.");
                var document = plan.CreateDocument(); document.Title += " (recovered)";
                await Adopt(document);
            });
            Button("Protect original and start blank", () => Adopt(new DocumentModel()));
            Button("Show previous recovery versions", async () =>
            {
                var versions = await _host.ListVersionsAsync();
                status.Text = versions.Count == 0 ? "No earlier recovery snapshots are available." : "Choose a previous version. The current original is protected before that version is opened.";
                foreach (var version in versions.Take(12))
                {
                    var label = "Restore " + version.Title + " · " + version.SavedAt.LocalDateTime.ToString("g");
                    if (actions.Children.OfType<FrameworkElement>().Any(e => AutomationProperties.GetName(e) == label)) continue;
                    Button(label, async () =>
                    {
                        var saved = await _host.ReadVersionAsync(version.Id) ?? throw new InvalidDataException("That recovery version is unavailable.");
                        var document = DocumentJson.Load(saved); document.Title += " (restored)";
                        await Adopt(document);
                    });
                }
            });
        }
        if (_host is not null) Button("Retry saved workspace", LoadWorkspaceAsync);
#if __WASM__
        BrowserWorkspaceHost.StartupError(failure.ToString());
        if (BrowserWorkspaceHost.DiagnosticsEnabled())
        {
            void Publish()
            {
                if (!ReferenceEquals(_window.Content, root)) { _recoveryTimer?.Stop(); return; }
                var controls = new List<RecoveryControlSnapshot>();
                foreach (var button in actions.Children.OfType<OfficeButton>())
                {
                    if (button.ActualWidth <= 0 || button.ActualHeight <= 0) continue;
                    var origin = button.TransformToVisual(root).TransformPoint(new Point());
                    controls.Add(new(AutomationProperties.GetName(button), origin.X, origin.Y, button.ActualWidth, button.ActualHeight, button.IsEnabled));
                }
                var snapshot = new RecoveryUiSnapshot(true, original is not null, plan is not null, status.Text, controls);
                BrowserRecoveryArchiveStore.PublishState(JsonSerializer.Serialize(snapshot, RecoveryWireContext.Default.RecoveryUiSnapshot));
            }
            _recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _recoveryTimer.Tick += (_, _) => Publish(); _recoveryTimer.Start();
        }
#endif
    }
}
