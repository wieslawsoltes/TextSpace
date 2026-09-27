using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TextSpace.Controls;
using TextSpace.Documents;
using TextSpace.Editing;
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
        var title = OfficeTheme.Text("TextSpace", 32, OfficeTheme.Accent, true);
        var message = OfficeTheme.Text("Preparing your writing room…", 14, OfficeTheme.Muted);
        var loading = new StackPanel { Spacing = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Children = { title, message, new ProgressRing { IsActive = true, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Left } } };
        _window.Content = new Grid { Background = OfficeTheme.Brush("#F5F7FA"), Children = { loading } }; _window.Activate();
        try
        {
#if __WASM__
            _host = new BrowserWorkspaceHost();
#else
            _host = new DesktopWorkspaceHost();
#endif
            // Failed recovery must not replace user data with a sample autosave.
            var json = await _host.ReadLatestAsync();
            var document = string.IsNullOrWhiteSpace(json) ? SampleDocument.Create() : DocumentJson.Load(json);
            var session = new EditorSession(document); _workbench = new(session, _host);
            await FontBootstrap.RegisterAsync(_workbench.Surface.Renderer.Metrics);
            _workbench.Surface.Relayout();
            _window.Content = _workbench;
            _window.Closed += (_, _) => { _workbench?.Dispose(); if (_host is IDisposable disposable) disposable.Dispose(); };
#if __WASM__
            BrowserDiagnostics.Attach(_workbench, _window);
            BrowserWorkspaceHost.Ready();
#endif
            _window.Activate();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            var details = OfficeTheme.Text("TextSpace could not start. " + ex.Message, 14, "#A4262C"); details.TextWrapping = TextWrapping.Wrap; details.MaxWidth = 600;
            _window.Content = new Grid { Background = OfficeTheme.Brush("#F5F7FA"), Children = { new StackPanel { Padding = new(36), Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Children = { OfficeTheme.Text("TextSpace", 32, OfficeTheme.Accent, true), details, OfficeTheme.Text("Your recovery data has not been cleared. Reload to try again.", 12, OfficeTheme.Muted) } } } };
#if __WASM__
            BrowserWorkspaceHost.StartupError(ex.ToString());
#endif
        }
    }
}
