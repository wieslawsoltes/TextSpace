using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace TextSpace.Controls;

/// <summary>Original vector icon set; no proprietary icon font or image assets are required.</summary>
public sealed class OfficeIcon : SKCanvasElement
{
    private string _glyph = "document", _color = OfficeTheme.Ink;
    public string Glyph { get => _glyph; set { _glyph = value; Invalidate(); } }
    public string Color { get => _color; set { _color = value; Invalidate(); } }
    public OfficeIcon() { Width = Height = 18; IsHitTestVisible = false; }
    public OfficeIcon(string glyph, double size = 18, string color = OfficeTheme.Ink) : this() { Glyph = glyph; Width = Height = size; Color = color; }
    private static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["document"] = "M6 2.5H15L20 7.5V21.5H6Z M15 2.5V8H20 M9 12H17 M9 15H17 M9 18H14",
        ["save"] = "M4 3H17L21 7V21H3V3Z M7 3V9H16V3 M7 21V14H17V21 M13 4V7",
        ["undo"] = "M8 5L3 10L8 15 M3 10H14C21 10 22 19 15 21",
        ["redo"] = "M16 5L21 10L16 15 M21 10H10C3 10 2 19 9 21",
        ["paste"] = "M8 5H5V21H19V5H16 M9 3H15V7H9Z M8 11H16 M8 15H16 M8 18H14",
        ["copy"] = "M8 7H20V21H8Z M16 7V3H4V17H8",
        ["cut"] = "M5 4L19 20 M19 4L5 20 M6 15A3 3 0 1 0 6 21A3 3 0 1 0 6 15 M18 15A3 3 0 1 0 18 21A3 3 0 1 0 18 15",
        ["paint"] = "M5 3H19V10H5Z M5 6H3V12H12V15 M10 15H14V22H10Z",
        ["bold"] = "M7 3V21H14C21 21 21 12 14 12H7 M7 3H13C20 3 20 12 13 12",
        ["italic"] = "M10 3H19 M5 21H14 M15 3L9 21",
        ["underline"] = "M6 3V12C6 20 18 20 18 12V3 M4 22H20",
        ["strike"] = "M17 5C14 1 5 3 6 8C7 12 17 11 18 16C19 22 9 24 5 19 M3 12H21",
        ["subscript"] = "M3 5L12 17 M12 5L3 17 M16 16C20 12 23 17 18 20L16 22H22",
        ["superscript"] = "M3 7L12 19 M12 7L3 19 M16 4C20 0 23 5 18 8L16 10H22",
        ["font"] = "M4 19L11 3H13L20 19 M7 13H17 M3 22H21",
        ["grow"] = "M2 20L8 5L14 20 M4 15H12 M17 9V1 M14 4L17 1L20 4",
        ["shrink"] = "M2 20L8 5L14 20 M4 15H12 M17 1V9 M14 6L17 9L20 6",
        ["clear"] = "M4 17L13 4L21 10L13 22H9Z M8 12L17 18 M14 22H22",
        ["highlight"] = "M4 16L14 3L21 8L11 21Z M7 13L15 18 M3 22H13",
        ["bullets"] = "M8 5H21 M8 12H21 M8 19H21 M3 4H4V6H3Z M3 11H4V13H3Z M3 18H4V20H3Z",
        ["numbering"] = "M9 5H21 M9 12H21 M9 19H21 M3 3H4V7 M2 10C6 8 6 13 2 14H6 M2 17H5L3 19C7 18 6 23 2 21",
        ["multilist"] = "M8 4H21 M11 11H21 M14 18H21 M3 3H5V5H3Z M6 10H8V12H6Z M9 17H11V19H9Z",
        ["align-left"] = "M3 4H21 M3 9H16 M3 14H21 M3 19H16",
        ["align-center"] = "M3 4H21 M6 9H18 M3 14H21 M6 19H18",
        ["align-right"] = "M3 4H21 M8 9H21 M3 14H21 M8 19H21",
        ["justify"] = "M3 4H21 M3 9H21 M3 14H21 M3 19H21",
        ["indent"] = "M3 4H21 M11 9H21 M11 14H21 M3 19H21 M3 8L7 12L3 16",
        ["outdent"] = "M3 4H21 M11 9H21 M11 14H21 M3 19H21 M7 8L3 12L7 16",
        ["spacing"] = "M11 4H22 M11 9H22 M11 14H22 M11 19H22 M5 3V21 M2 6L5 3L8 6 M2 18L5 21L8 18",
        ["paragraph"] = "M17 3H10C2 3 2 13 10 13H12 M12 3V22 M17 3V22 M9 22H20",
        ["borders"] = "M3 3H21V21H3Z M12 3V21 M3 12H21",
        ["shade"] = "M4 13L13 3L22 12L12 22Z M5 13H21 M5 2L12 9",
        ["search"] = "M10 3A7 7 0 1 0 10 17A7 7 0 1 0 10 3 M15 15L22 22",
        ["replace"] = "M9 3A5 5 0 1 0 9 13A5 5 0 1 0 9 3 M13 12L18 17 M4 18H18 M14 14L18 18L14 22",
        ["select"] = "M5 2L5 20L10 15L14 23L18 21L14 13H21Z",
        ["table"] = "M3 3H21V21H3Z M3 9H21 M3 15H21 M9 3V21 M15 3V21",
        ["image"] = "M3 4H21V20H3Z M3 17L9 11L13 15L17 10L21 14 M7 7A1.5 1.5 0 1 0 7 10A1.5 1.5 0 1 0 7 7",
        ["link"] = "M10 8L13 5C19 -1 26 6 20 12L17 15 M14 16L11 19C5 25 -2 18 4 12L7 9 M8 16L16 8",
        ["comment"] = "M3 4H21V17H11L5 22V17H3Z M7 8H17 M7 12H14",
        ["new-comment"] = "M3 4H21V17H11L5 22V17H3Z M8 10H16 M12 6V14",
        ["track"] = "M5 3H14L19 8V21H5Z M14 3V8H19 M8 12H15 M8 16H12 M1 7V18",
        ["check"] = "M4 12L9 17L21 5",
        ["reject"] = "M5 5L19 19 M19 5L5 19",
        ["close"] = "M6 6L18 18 M18 6L6 18",
        ["chevron"] = "M7 9L12 14L17 9",
        ["chevron-right"] = "M9 7L14 12L9 17",
        ["chevron-up"] = "M7 15L12 10L17 15",
        ["more"] = "M4 12H5 M11 12H12 M18 12H19",
        ["launch"] = "M4 4H20V20 M12 4H4V20H20V12 M10 14L20 4",
        ["open"] = "M2 6H9L12 9H22L19 21H3Z M4 6V3H11L14 6H21V9",
        ["new"] = "M5 2H14L19 7V22H5Z M14 2V8H19 M8 15H16 M12 11V19",
        ["print"] = "M6 8V2H18V8 M6 18H3V8H21V18H18 M6 14H18V22H6Z M17 11H18",
        ["export"] = "M13 3H4V21H20V14 M12 12L22 2 M15 2H22V9",
        ["download"] = "M12 2V16 M6 10L12 16L18 10 M3 17V22H21V17",
        ["pdf"] = "M5 2H14L20 8V22H5Z M14 2V8H20 M8 17C16 11 9 3 10 13C11 21 24 17 15 15C7 13 3 22 8 17Z",
        ["pagebreak"] = "M5 3H19V9 M5 9V3 M5 15V22H19V15 M2 12H6 M9 12H13 M16 12H22",
        ["margins"] = "M4 2H20V22H4Z M8 6H16V18H8Z",
        ["orientation"] = "M3 3H13V17H3Z M9 10H22V21H9 M16 3L20 7 M16 7H20V3",
        ["columns"] = "M3 3H10V21H3Z M14 3H21V21H14Z M5 6H8 M5 10H8 M5 14H8 M16 6H19 M16 10H19 M16 14H19",
        ["header"] = "M5 2H19V22H5Z M7 5H17V9H7Z M8 13H16 M8 17H14",
        ["footer"] = "M5 2H19V22H5Z M7 15H17V19H7Z M8 6H16 M8 10H14",
        ["page-number"] = "M5 2H19V22H5Z M9 15H15 M9 18H15 M11 13L10 20 M14 13L13 20",
        ["date"] = "M3 5H21V21H3Z M3 10H21 M7 2V7 M17 2V7 M7 14H9 M12 14H14 M17 14H18 M7 18H9 M12 18H14",
        ["symbol"] = "M3 4H21 M3 4L12 12L3 21H21 M21 4V7 M21 18V21",
        ["toc"] = "M3 4H15 M3 10H12 M3 16H15 M3 22H12 M19 4H21 M19 10H21 M19 16H21 M19 22H21",
        ["book"] = "M12 5C8 2 4 2 2 3V20C5 18 9 19 12 21C15 19 19 18 22 20V3C19 2 15 2 12 5Z M12 5V21",
        ["ruler"] = "M2 8H22V17H2Z M5 8V12 M9 8V14 M13 8V12 M17 8V14 M21 8V12",
        ["navigation"] = "M3 3H21V21H3Z M9 3V21 M5 7H7 M5 11H7 M5 15H7",
        ["zoom"] = "M10 3A7 7 0 1 0 10 17A7 7 0 1 0 10 3 M15 15L22 22 M6 10H14 M10 6V14",
        ["plus"] = "M4 12H20 M12 4V20",
        ["minus"] = "M4 12H20",
        ["focus"] = "M3 9V3H9 M15 3H21V9 M21 15V21H15 M9 21H3V15",
        ["settings"] = "M3 6H21 M3 12H21 M3 18H21 M8 3V9 M16 9V15 M10 15V21",
        ["help"] = "M12 2A10 10 0 1 0 12 22A10 10 0 1 0 12 2 M8 8C8 3 18 4 16 9C15 12 11 10 12 15 M12 18V19",
        ["info"] = "M12 2A10 10 0 1 0 12 22A10 10 0 1 0 12 2 M12 10V18 M12 6V7",
        ["mail"] = "M2 5H22V20H2Z M2 5L12 13L22 5",
        ["people"] = "M9 3A4 4 0 1 0 9 11A4 4 0 1 0 9 3 M2 22V19C2 12 16 12 16 19V22 M17 4C24 4 24 12 18 12 M18 15C22 15 23 18 23 22",
        ["microphone"] = "M8 5C8 0 16 0 16 5V12C16 17 8 17 8 12Z M4 10V12C4 23 20 23 20 12V10 M12 21V24",
        ["history"] = "M4 7C11 -2 23 4 22 14C21 23 7 26 3 17 M4 2V8H10 M12 6V13L17 16",
        ["lock"] = "M5 10H19V22H5Z M8 10V6C8 0 16 0 16 6V10 M12 14V18",
        ["row"] = "M3 3H21V21H3Z M3 9H21 M3 15H21 M12 3V21 M1 12H23",
        ["delete"] = "M4 6H20 M9 3H15 M6 6L7 22H17L18 6 M10 10V18 M14 10V18",
        ["cloud"] = "M6 19C-2 18 0 9 7 9C7 0 20 0 20 10C27 11 24 20 18 19H6 M9 14L12 17L18 11",
        ["spell"] = "M2 13L7 2L12 13 M4 9H10 M13 14L17 18L23 10 M3 19H10",
        ["checkbox"] = "M4 4H20V20H4Z",
        ["checked"] = "M4 4H20V20H4Z M7 12L11 16L18 8"
    };
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        canvas.Save(); canvas.Scale((float)(area.Width / 24), (float)(area.Height / 24));
        using var paint = new SKPaint { Color = SKColor.TryParse(Color, out var color) ? color : SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Glyph is "bold" ? 2.4f : 1.4f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        if (Paths.TryGetValue(Glyph, out var data)) { using var path = SKPath.ParseSvgPathData(data); if (path is not null) canvas.DrawPath(path, paint); }
        else { using var path = SKPath.ParseSvgPathData(Paths["document"]); canvas.DrawPath(path, paint); }
        canvas.Restore();
    }
}
