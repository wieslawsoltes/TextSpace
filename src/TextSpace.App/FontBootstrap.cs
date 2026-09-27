using TextSpace.Skia;
using Windows.Storage;

namespace TextSpace.App;

internal static class FontBootstrap
{
    private static async Task<byte[]> ReadAsync(string file)
    {
        var asset = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Fonts/" + file));
        using var stream = await asset.OpenStreamForReadAsync(); using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes); return bytes.ToArray();
    }
    public static async Task RegisterAsync(SkiaTextMetrics metrics)
    {
        foreach (var (suffix, bold, italic) in new[] { ("Regular", false, false), ("Bold", true, false), ("Italic", false, true), ("BoldItalic", true, true) })
        {
            var data = await ReadAsync("Carlito-" + suffix + ".ttf");
            foreach (var family in new[] { "Aptos", "Aptos Display", "Calibri", "Carlito", "Arial", "Inter" }) metrics.Register(family, bold, italic, data);
        }
        foreach (var family in new[] { "Times New Roman", "Georgia" })
        {
            try
            {
                foreach (var (suffix, bold, italic) in new[] { ("Regular", false, false), ("Bold", true, false), ("Italic", false, true), ("BoldItalic", true, true) }) metrics.Register(family, bold, italic, await ReadAsync("Tinos-" + suffix + ".ttf"));
            }
            catch (FileNotFoundException) { metrics.Register(family, false, false, await ReadAsync("Carlito-Regular.ttf")); }
        }
        try
        {
            foreach (var (suffix, bold, italic) in new[] { ("Regular", false, false), ("Bold", true, false), ("Italic", false, true), ("BoldItalic", true, true) }) metrics.Register("Courier New", bold, italic, await ReadAsync("Cousine-" + suffix + ".ttf"));
        }
        catch (FileNotFoundException) { metrics.Register("Courier New", false, false, await ReadAsync("Carlito-Regular.ttf")); }
    }
}
