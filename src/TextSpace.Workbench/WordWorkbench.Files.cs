using System.IO.Compression;
using System.Text;
using SkiaSharp;
using TextSpace.OpenXml;

namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    public async Task SaveRecoveryAsync()
    {
        if (_disposed) return;
        await _saveGate.WaitAsync();
        try
        {
            if (_disposed) return;
            var revision = Session.Revision; var title = Session.Document.Title; var json = DocumentJson.Save(Session.Document);
            await Host.WriteLatestAsync(title, json);
            _saveState.Text = revision == Session.Revision ? "Saved locally" : "Saving…";
        }
        catch (Exception ex) { _saveState.Text = "Not saved"; Notify("Local recovery could not be saved: " + ex.Message + " Save a file copy now.", true); }
        finally { _saveGate.Release(); }
    }
    private static string SafeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var name = new string(title.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(name) ? "Document" : name.Length > 100 ? name[..100] : name;
    }
    private async Task<bool> ConfirmReplaceAsync()
    {
        if (!Session.IsDirty) return true;
        var dialog = new OfficeDialog("Keep your current work", "Save copy & continue");
        dialog.AddDescription("Save a complete .textspace file before replacing the current document. Local recovery keeps recent snapshots, but it is not a substitute for a file copy.");
        var discard = new OfficeButton("Continue without a copy", () => dialog.Close(true)) { BorderThickness = new(1), Height = 32 };
        var saveCopy = true; discard.Click += (_, _) => saveCopy = false; dialog.Footer.Children.Insert(0, discard);
        if (!await ShowDialogAsync(dialog)) return false;
        if (saveCopy) await ExportAsync("textspace");
        await SaveRecoveryAsync(); return true;
    }
    private async Task NewDocumentAsync(DocumentModel document)
    {
        if (!await ConfirmReplaceAsync()) return;
        Session.IsReadOnly = false; Session.Load(document); _selectedCommentId = null; _searchQuery = ""; _searchMatchIndex = 0;
        Ribbon.IsCollapsed = false; Ribbon.SelectTab("Home"); SetNavigation(false); SetReview(false, "Comments"); HideBackstage();
        await SaveRecoveryAsync(); Surface.FocusEditor();
    }
    private static (DocumentModel Document, IReadOnlyList<string> Warnings) ReadDocument(OpenedFile file)
    {
        if (file.Bytes.Length > DocumentJson.MaxFileBytes) throw new InvalidDataException("Documents must be smaller than 32 MB.");
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (extension == ".docx") { var result = new DocxReader().Read(file.Bytes); return (result.Document, result.Warnings); }
        if (extension == ".textspace") return (DocumentJson.Load(Encoding.UTF8.GetString(file.Bytes)), []);
        if (extension is ".txt" or ".md" or ".csv") return (DocumentJson.FromText(Encoding.UTF8.GetString(file.Bytes).TrimStart('\uFEFF'), Path.GetFileNameWithoutExtension(file.Name)), []);
        throw new InvalidDataException("Choose a .docx, .textspace, .txt or .md document. Legacy .doc and macro-enabled files are not supported.");
    }
    private async Task OpenDocumentAsync()
    {
        var file = await Host.OpenFileAsync(".docx,.textspace,.txt,.md"); if (file is null) return;
        var result = ReadDocument(file); await NewDocumentAsync(result.Document);
        if (result.Warnings.Count > 0) Notify("Import notes: " + string.Join(" ", result.Warnings), true); else Notify("Opened " + file.Name);
    }
    private async Task ExportAsync(string format)
    {
        if (_busy) return; _busy = true;
        try
        {
            byte[] bytes; string type;
            switch (format)
            {
                case "textspace": bytes = Encoding.UTF8.GetBytes(DocumentJson.Save(Session.Document)); type = "application/vnd.textspace+json"; break;
                case "docx": bytes = new DocxWriter().Write(Session.Document); type = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"; break;
                case "pdf": bytes = Surface.Renderer.ExportPdf(Session.Document, Surface.Layout); type = "application/pdf"; break;
                case "png": bytes = Surface.Renderer.ExportPng(Session.Document, Surface.Layout.Caret(Session.Selection.Active).PageIndex); type = "image/png"; break;
                case "html": bytes = Encoding.UTF8.GetBytes(HtmlExporter.Export(Session.Document)); type = "text/html;charset=utf-8"; break;
                case "txt": bytes = Encoding.UTF8.GetBytes(Session.Document.PlainText); type = "text/plain;charset=utf-8"; break;
                default: throw new ArgumentOutOfRangeException(nameof(format));
            }
            await Host.SaveFileAsync(SafeFileName(Session.Document.Title) + "." + format, bytes, type);
            if (format == "textspace") Session.MarkSaved();
            Notify("Saved a " + format.ToUpperInvariant() + " copy");
            if (format == "docx" && Session.Document.Changes.Count > 0) Notify("DOCX includes the current text and comments. Local revision history is preserved only in .textspace files.", true);
        }
        finally { _busy = false; }
    }
    private async Task PrintAsync()
    {
        if (Surface.Layout.Pages.Count > 100) throw new InvalidOperationException("Browser print preview is limited to 100 pages. Export the document to PDF instead.");
        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><title>" + System.Net.WebUtility.HtmlEncode(Session.Document.Title) + "</title><style>html,body{margin:0;padding:0}img{display:block;width:100%;break-after:page}img:last-child{break-after:auto}@page{size:" + Session.Document.Page.Width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "pt " + Session.Document.Page.Height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "pt;margin:0}</style></head><body>");
        for (var i = 0; i < Surface.Layout.Pages.Count; i++)
        {
            var png = Surface.Renderer.ExportPng(Session.Document, i, 1.5); html.Append("<img alt=\"Page ").Append(i + 1).Append("\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(png)).Append("\">");
            if (html.Length > 80 * 1024 * 1024) throw new InvalidOperationException("Print preview exceeds the browser memory budget. Export to PDF instead.");
        }
        html.Append("</body></html>"); await Host.PrintHtmlAsync(html.ToString());
    }
    private async Task InsertPictureAsync()
    {
        var file = await Host.OpenFileAsync(".png,.jpg,.jpeg,.gif"); if (file is null) return;
        if (file.Bytes.Length > 16 * 1024 * 1024) throw new InvalidDataException("Pictures must be smaller than 16 MB.");
        using var data = SKData.CreateCopy(file.Bytes); using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width < 1 || codec.Info.Height < 1 || (long)codec.Info.Width * codec.Info.Height > 40_000_000) throw new InvalidDataException("This picture is invalid or exceeds 40 megapixels.");
        var type = codec.EncodedFormat switch { SKEncodedImageFormat.Png => "image/png", SKEncodedImageFormat.Jpeg => "image/jpeg", SKEncodedImageFormat.Gif => "image/gif", _ => throw new InvalidDataException("Only PNG, JPEG and GIF pictures are supported.") };
        Session.InsertPicture(file.Bytes, type, codec.Info.Width * 0.75, codec.Info.Height * 0.75, Path.GetFileNameWithoutExtension(file.Name));
    }
    private async Task SelectRecipientsAsync()
    {
        var file = await Host.OpenFileAsync(".csv"); if (file is null) return;
        _mergeData = MailMerge.ParseCsv(Encoding.UTF8.GetString(file.Bytes)); Ribbon.RefreshTab();
        Notify($"Loaded {_mergeData.Records.Count} recipients and {_mergeData.Columns.Count} fields");
    }
    private async Task PreviewMergeAsync()
    {
        if (_mergeData is null || _mergeData.Records.Count == 0) throw new InvalidOperationException("Load a CSV recipient list with at least one record first.");
        var document = MailMerge.Merge(Session.Document, _mergeData.Records[0]);
        var dialog = new OfficeDialog("Preview results · first recipient", "Close", 560);
        dialog.AddDescription("This preview does not modify your template. Finish & Merge exports a separate DOCX for each recipient.");
        dialog.Body.Children.Add(new TextSpace.Editor.DocumentPreview { Document = document, Renderer = Surface.Renderer, Width = 390, Height = 480 });
        await ShowDialogAsync(dialog);
    }
    private async Task FinishMergeAsync()
    {
        if (_mergeData is null || _mergeData.Records.Count == 0) throw new InvalidOperationException("Load recipients before finishing the merge.");
        if (_mergeData.Records.Count > 100) throw new InvalidOperationException("A browser merge is limited to 100 recipients per export. Split the recipient CSV into smaller batches.");
        var dialog = new OfficeDialog("Finish & Merge", "Export documents"); dialog.AddDescription($"Create {_mergeData.Records.Count} DOCX files in one ZIP archive. Your template will remain unchanged.");
        if (!await ShowDialogAsync(dialog)) return;
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            for (var i = 0; i < _mergeData.Records.Count; i++)
            {
                var document = MailMerge.Merge(Session.Document, _mergeData.Records[i]); var bytes = new DocxWriter().Write(document);
                using var stream = zip.CreateEntry($"{SafeFileName(document.Title)}-{i + 1:D3}.docx", CompressionLevel.Optimal).Open(); stream.Write(bytes);
                if (output.Length > 64 * 1024 * 1024) throw new InvalidOperationException("The merged archive exceeds 64 MB.");
            }
        }
        await Host.SaveFileAsync(SafeFileName(Session.Document.Title) + "-merged.zip", output.ToArray(), "application/zip"); Notify("Merged documents exported");
    }
}
