using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TextSpace.Controls;
using TextSpace.Workbench;
using Windows.Foundation;

namespace TextSpace.App;

/// <summary>Opt-in, read-only inspection for acceptance tests. All mutations still use real pointer and keyboard input.</summary>
internal static partial class BrowserDiagnostics
{
    private static DispatcherTimer? _timer;
    [JSImport("globalThis.TextSpaceHost.publishState")] private static partial void Publish(string json);
    public static void Attach(WordWorkbench workbench, Window window)
    {
        if (!BrowserWorkspaceHost.DiagnosticsEnabled()) return;
        void Update()
        {
            try
            {
                var controls = new List<object>();
                void Walk(DependencyObject node, bool visible = true)
                {
                    if (node is UIElement ui && ui.Visibility != Visibility.Visible) return;
                    if (node is FrameworkElement element && element.ActualWidth > 0 && element.ActualHeight > 0)
                    {
                        var name = AutomationProperties.GetName(element);
                        if (!string.IsNullOrEmpty(name) && (element is Button or TextBox or TextSpace.Controls.OfficeComboField))
                        {
                            try
                            {
                                var point = element.TransformToVisual(workbench).TransformPoint(new Point(0, 0));
                                controls.Add(new { name, command = (element as RibbonButton)?.CommandId ?? "", kind = element is TextBox ? "textbox" : "button", x = point.X, y = point.Y, width = element.ActualWidth, height = element.ActualHeight, enabled = element is not Control control || control.IsEnabled });
                            }
                            catch { }
                        }
                    }
                    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i), visible);
                }
                Walk(workbench);
                if (workbench.XamlRoot is not null) foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(workbench.XamlRoot)) if (popup.Child is not null) Walk(popup.Child);
                var s = workbench.Session; var surface = workbench.Surface; var position = surface.TransformToVisual(workbench).TransformPoint(new Point(0, 0)); var caret = surface.Layout.Caret(s.Selection.Active);
                var snapshot = new
                {
                    ready = true, runtime = "Uno WebAssembly / Skia", title = s.Document.Title, text = s.Document.PlainText,
                    pages = surface.Layout.Pages.Count, words = s.Document.WordCount, paragraphs = s.Document.Paragraphs().Count(), tables = s.Document.Blocks.OfType<TextSpace.Core.TableBlock>().Count(), images = s.Document.Blocks.OfType<TextSpace.Core.ImageBlock>().Count(),
                    comments = s.Document.Comments.Select(c => new { c.Id, c.Text, c.Resolved }).ToArray(), changes = s.Document.Changes.Count,
                    selection = new { start = s.Selection.Start, end = s.Selection.End, active = s.Selection.Active, length = s.Selection.Length },
                    style = new { bold = s.TypingStyle.Bold, italic = s.TypingStyle.Italic, underline = s.TypingStyle.Underline, fontSize = s.TypingStyle.FontSize, fontFamily = s.TypingStyle.FontFamily },
                    canUndo = s.CanUndo, canRedo = s.CanRedo, revision = s.Revision, zoom = surface.Zoom, selectedTab = workbench.Ribbon.SelectedTab, status = workbench.StatusText, dialog = workbench.IsDialogOpen,
                    canvas = new { x = position.X, y = position.Y + (surface.ShowRuler ? 25 : 0), width = surface.ViewportWidth, height = surface.ViewportHeight, paperLeft = surface.PaperLeft, scale = surface.Scale, scrollY = surface.ScrollY },
                    caret = new { x = caret.X, y = caret.Y, page = caret.PageIndex, height = caret.Height },
                    controls
                };
                Publish(JsonSerializer.Serialize(snapshot));
            }
            catch (Exception ex) { Console.Error.WriteLine("TextSpace diagnostics: " + ex.Message); }
        }
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; _timer.Tick += (_, _) => Update(); _timer.Start(); workbench.StateChanged += Update; Update();
    }
}
