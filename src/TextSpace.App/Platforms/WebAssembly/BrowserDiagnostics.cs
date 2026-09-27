using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TextSpace.Controls;
using TextSpace.Workbench;
using Windows.Foundation;

namespace TextSpace.App;

/// <summary>Read-only, opt-in browser inspection. Explicit JSON writing is safe under trimming.</summary>
internal static partial class BrowserDiagnostics
{
    [JSImport("globalThis.TextSpaceHost.publishState")]
    private static partial void Publish(string json);

    public static void Attach(WordWorkbench workbench, Window window)
    {
        if (!BrowserWorkspaceHost.DiagnosticsEnabled()) return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        var updating = false;
        void Update()
        {
            if (updating) return;
            updating = true;
            try
            {
                var session = workbench.Session;
                var surface = workbench.Surface;
                var origin = surface.TransformToVisual(workbench).TransformPoint(new Point());
                using var stream = new MemoryStream();
                using (var json = new Utf8JsonWriter(stream))
                {
                    json.WriteStartObject();
                    json.WriteBoolean("ready", true);
                    json.WriteString("runtime", "Uno WebAssembly / Skia");
                    json.WriteString("title", session.Document.Title);
                    json.WriteString("text", session.Document.PlainText);
                    json.WriteNumber("pages", surface.Layout.Pages.Count);
                    json.WriteNumber("words", session.Document.WordCount);
                    json.WriteNumber("sections", TextSpace.Core.DocumentSections.Definitions(session.Document).Count);
                    json.WriteNumber("currentSection", session.CurrentSectionIndex);
                    json.WriteStartArray("pageGeometry");
                    foreach (var item in surface.Layout.Pages)
                    {
                        json.WriteStartObject(); json.WriteNumber("width", item.Settings.Width); json.WriteNumber("height", item.Settings.Height);
                        json.WriteNumber("top", surface.Layout.PageTop(item.Index)); json.WriteNumber("left", surface.Layout.PageLeft(item.Index));
                        json.WriteNumber("section", item.SectionIndex); json.WriteNumber("number", item.PageNumber);
                        json.WriteString("header", item.Header); json.WriteString("footer", item.Footer);
                        json.WriteBoolean("blank", item.IsParityBlank); json.WriteEndObject();
                    }
                    json.WriteEndArray();
                    json.WriteStartArray("fields");
                    foreach (var field in session.Document.Fields)
                    {
                        json.WriteStartObject(); json.WriteString("id", field.Id); json.WriteString("code", field.Instruction);
                        json.WriteNumber("start", field.Start); json.WriteNumber("end", field.End); json.WriteBoolean("locked", field.Locked);
                        json.WriteString("value", session.Index.Text.Substring(field.Start, field.End - field.Start)); json.WriteEndObject();
                    }
                    json.WriteEndArray();
                    json.WriteNumber("paragraphs", session.Document.Paragraphs().Count());
                    json.WriteNumber("tables", session.Document.Blocks.OfType<TextSpace.Core.TableBlock>().Count());
                    json.WriteNumber("images", session.Document.Blocks.OfType<TextSpace.Core.ImageBlock>().Count());
                    json.WriteStartArray("comments");
                    foreach (var comment in session.Document.Comments)
                    {
                        json.WriteStartObject(); json.WriteString("id", comment.Id); json.WriteString("text", comment.Text);
                        json.WriteBoolean("resolved", comment.Resolved); json.WriteEndObject();
                    }
                    json.WriteEndArray();
                    json.WriteNumber("changes", session.Document.Changes.Count);
                    json.WriteStartObject("selection");
                    json.WriteNumber("start", session.Selection.Start); json.WriteNumber("end", session.Selection.End);
                    json.WriteNumber("active", session.Selection.Active); json.WriteNumber("length", session.Selection.Length);
                    json.WriteEndObject();
                    json.WriteStartObject("style");
                    json.WriteBoolean("bold", session.TypingStyle.Bold); json.WriteBoolean("italic", session.TypingStyle.Italic);
                    json.WriteBoolean("underline", session.TypingStyle.Underline);
                    json.WriteNumber("fontSize", session.TypingStyle.FontSize); json.WriteString("fontFamily", session.TypingStyle.FontFamily);
                    json.WriteEndObject();
                    WriteTypography(json, session, surface);
                    json.WriteBoolean("canUndo", session.CanUndo); json.WriteBoolean("canRedo", session.CanRedo);
                    json.WriteNumber("revision", session.Revision); json.WriteNumber("zoom", surface.Zoom);
                    json.WriteString("selectedTab", workbench.Ribbon.SelectedTab); json.WriteString("status", workbench.StatusText);
                    json.WriteBoolean("dialog", workbench.IsDialogOpen);
                    json.WriteStartObject("canvas");
                    json.WriteNumber("x", origin.X); json.WriteNumber("y", origin.Y + (surface.ShowRuler ? 25 : 0));
                    json.WriteNumber("width", surface.ViewportWidth); json.WriteNumber("height", surface.ViewportHeight);
                    json.WriteNumber("paperLeft", surface.PaperLeft); json.WriteNumber("scale", surface.Scale);
                    json.WriteNumber("scrollY", surface.ScrollY); json.WriteEndObject();
                    json.WriteStartArray("controls");
                    void Walk(DependencyObject node)
                    {
                        if (node is UIElement ui && ui.Visibility != Visibility.Visible) return;
                        if (node is FrameworkElement element && element.ActualWidth > 0 && element.ActualHeight > 0 && element is Button or TextBox)
                        {
                            var name = AutomationProperties.GetName(element);
                            if (!string.IsNullOrEmpty(name))
                            {
                                Point point;
                                try { point = element.TransformToVisual(workbench).TransformPoint(new Point()); }
                                catch (InvalidOperationException) { point = new Point(-10000, -10000); }
                                json.WriteStartObject(); json.WriteString("name", name);
                                json.WriteString("command", (element as RibbonButton)?.CommandId ?? "");
                                json.WriteString("kind", element is TextBox ? "textbox" : "button");
                                json.WriteNumber("x", point.X); json.WriteNumber("y", point.Y);
                                json.WriteNumber("width", element.ActualWidth); json.WriteNumber("height", element.ActualHeight);
                                json.WriteBoolean("enabled", element is not Control control || control.IsEnabled); json.WriteEndObject();
                            }
                        }
                        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
                    }
                    Walk(workbench);
                    if (workbench.XamlRoot is not null)
                        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(workbench.XamlRoot))
                            if (popup.Child is not null) Walk(popup.Child);
                    json.WriteEndArray(); json.WriteEndObject();
                }
                Publish(Encoding.UTF8.GetString(stream.ToArray()));
            }
            catch (Exception error) { Console.Error.WriteLine("TextSpace diagnostics: " + error.Message); }
            finally { updating = false; }
        }
        timer.Tick += (_, _) => Update();
        timer.Start();
        window.Closed += (_, _) => timer.Stop();
        Update();
    }
}
