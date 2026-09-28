using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TextSpace.Controls;
using TextSpace.Core;
using TextSpace.Documents;
using TextSpace.Editing;
using TextSpace.Storage;
using TextSpace.Workbench;

namespace TextSpace.App;

public sealed partial class App : Application
{
    private Window? _window;
    private WordWorkbench? _workbench;
    private IWorkspaceHost? _host;
    public App() { InitializeComponent(); RequestedTheme = ApplicationTheme.Light; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "TextSpace" };
        OfficeTheme.Font = new FontFamily("ms-appx:///Assets/Fonts/Inter.ttf#Inter");
        ShowLoading(); _window.Activate();
        _window.Closed += (_, _) => { _recoveryTimer?.Stop(); _workbench?.Dispose(); if (_host is IDisposable disposable) disposable.Dispose(); };
        try
        {
#if __WASM__
            _host = new RecoveryWorkspaceHost(new BrowserWorkspaceHost(), new BrowserRecoveryArchiveStore());
#else
            _host = new RecoveryWorkspaceHost(new DesktopWorkspaceHost(), new FileRecoveryArchiveStore(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TextSpace", "ProtectedOriginals")));
#endif
            await LoadWorkspaceAsync();
        }
        catch (Exception ex) { ShowRecoveryCenter(null, ex); }
    }

    private void ShowLoading()
    {
        var loading = new StackPanel { Spacing = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Children = { OfficeTheme.Text("TextSpace", 32, OfficeTheme.Accent, true), OfficeTheme.Text("Preparing your writing room…", 14, OfficeTheme.Muted),
                new ProgressRing { IsActive = true, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Left } } };
        _window!.Content = new Grid { Background = OfficeTheme.Brush("#F5F7FA"), Children = { loading } };
    }

    private async Task LoadWorkspaceAsync()
    {
        string? original = null;
        DocumentModel document;
        try
        {
            original = await _host!.ReadLatestAsync();
            // Only an absent recovery means a new workspace. Empty/whitespace or
            // invalid persisted text is preserved and offered for explicit recovery.
            document = original is null ? SampleDocument.Create() : DocumentJson.Load(original);
        }
        catch (Exception ex) { ShowRecoveryCenter(original, ex); return; }
        try { await OpenWorkbenchAsync(document); }
        catch (Exception ex) { ShowRecoveryCenter(null, ex); }
    }

    private async Task OpenWorkbenchAsync(DocumentModel document, string? notice = null)
    {
        WordWorkbench? next = null;
        try
        {
            next = new WordWorkbench(new EditorSession(document), _host!);
            next.EnableRecoveryTools();
            await FontBootstrap.RegisterAsync(next.Surface.Renderer.Metrics);
            next.Surface.Relayout();
            _recoveryTimer?.Stop();
            _workbench?.Dispose(); _workbench = next;
            _window!.Content = next;
            if (notice is not null) next.ShowRecoveryNotice(notice);
#if __WASM__
            BrowserRecoveryArchiveStore.Complete();
            BrowserDiagnostics.Attach(next, _window);
            BrowserWorkspaceHost.Ready();
#endif
            _window.Activate();
        }
        catch { if (!ReferenceEquals(next, _workbench)) next?.Dispose(); throw; }
    }
}
