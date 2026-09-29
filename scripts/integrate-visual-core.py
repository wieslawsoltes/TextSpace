from pathlib import Path

# One-time, asserted integration of reviewed source additions. Removed after use.
def replace(path, old, new, count=1):
    p = Path(path); text = p.read_text()
    assert text.count(old) == count, (path, text.count(old), old[:100])
    p.write_text(text.replace(old, new))

replace('src/TextSpace.Core/Block.cs', '[JsonDerivedType(typeof(ImageBlock), "image")]', '[JsonDerivedType(typeof(ImageBlock), "image")]\n[JsonDerivedType(typeof(ShapeBlock), "shape")]\n[JsonDerivedType(typeof(EquationBlock), "equation")]')
replace('src/TextSpace.Documents/DocumentJson.cs', '''                    case ImageBlock image:
                        images += image.Data?.Length ?? 0;
                        if (images > MaxFileBytes || !double.IsFinite(image.Width + image.Height) || image.Width is <= 0 or > 4000 || image.Height is <= 0 or > 4000) throw new InvalidDataException("Invalid picture.");
                        break;''', '''                    case VisualBlock visual:
                        VisualBlockRules.Validate(visual);
                        if (depth > 0 && visual.Placement.Floating) throw new InvalidDataException("Floating placement is supported in the main story; objects inside table cells remain in flow.");
                        if (visual is ImageBlock image) images += image.Data.Length;
                        if (visual is ShapeBlock shape) chars += shape.Text.Length;
                        if (visual is EquationBlock equation) chars += equation.Root.DescendantsAndSelf().Sum(n => n.Text.Length);
                        if (images > MaxFileBytes || chars > MaxCharacters) throw new InvalidDataException("Visual content exceeds the document limits.");
                        break;''')
replace('src/TextSpace.Documents/DocumentJson.cs', '[JsonSerializable(typeof(TabStop))]', '[JsonSerializable(typeof(TabStop))]\n[JsonSerializable(typeof(Block))]\n[JsonSerializable(typeof(EquationNode))]')
replace('src/TextSpace.Layout/LayoutModels.cs', 'public sealed record LayoutImage(ImageBlock Image, RectD Bounds);', 'public sealed record LayoutImage(ImageBlock Image, RectD Bounds)\n{\n    public bool IsReplica { get; init; }\n}')
replace('src/TextSpace.Layout/LayoutModels.cs', 'public sealed record LayoutCell(string TableId, RectD Bounds, string? Fill, bool Header)\n{', '''public sealed record LayoutCell(string TableId, RectD Bounds, string? Fill, bool Header)
{
    public int Row { get; init; }
    public int Column { get; init; }
    public int RowSpan { get; init; } = 1;
    public int ColumnSpan { get; init; } = 1;
    public double TableWidth { get; init; }
    public double TableLeft { get; init; }
    public double LogicalHeight { get; init; }''')
replace('src/TextSpace.Layout/LayoutModels.cs', '    public List<LayoutImage> Images { get; } = [];', '    public List<LayoutImage> Images { get; } = [];\n    public List<LayoutObject> Objects { get; } = [];')
replace('src/TextSpace.Layout/PageLayoutEngine.cs', '''                case ImageBlock image:
                    var ratio = Math.Min(1, Math.Min(flow.Settings.ColumnWidth / image.Width, flow.Settings.ContentHeight / image.Height));
                    var width = image.Width * ratio; var height = image.Height * ratio; flow.Ensure(height);
                    var x = flow.Left + (image.Alignment == TextAlignment.Center ? (flow.Settings.ColumnWidth - width) / 2 : image.Alignment == TextAlignment.Right ? flow.Settings.ColumnWidth - width : 0);
                    flow.Page.Images.Add(new(image, new(x, flow.Y, width, height))); flow.Y += height + 8; break;''', '''                case VisualBlock visual:
                    var ratio = Math.Min(1, Math.Min(flow.Settings.ColumnWidth / visual.Width, flow.Settings.ContentHeight / visual.Height));
                    var height = visual.Height * ratio + Math.Max(0, visual.Placement.Y);
                    if (!visual.Placement.Floating) flow.Ensure(height);
                    var bounds = VisualGeometry.Place(visual, flow.Settings.ColumnWidth, flow.Settings.ContentHeight, flow.Left, flow.Y);
                    flow.Page.Objects.Add(new(visual, bounds));
                    if (visual is ImageBlock image) flow.Page.Images.Add(new(image, bounds));
                    if (!visual.Placement.Floating) flow.Y += height + 8;
                    break;''')
replace('src/TextSpace.Layout/TableLayouter.cs', '        public List<LayoutImage> Images = [];', '        public List<LayoutImage> Images = [];\n        public List<LayoutObject> Objects = [];')
replace('src/TextSpace.Layout/TableLayouter.cs', 'result.Cells.Add(new(table.Id, new(x[region.Column], y[region.Row], x[region.ColumnEnd] - x[region.Column], cellHeight), fill, table.HeaderRow && region.Row == 0));', 'result.Cells.Add(new(table.Id, new(x[region.Column], y[region.Row], x[region.ColumnEnd] - x[region.Column], cellHeight), fill, table.HeaderRow && region.Row == 0)\n            { Row = region.Row, Column = region.Column, RowSpan = region.RowSpan, ColumnSpan = region.ColumnSpan, TableWidth = width, LogicalHeight = cellHeight });')
replace('src/TextSpace.Layout/TableLayouter.cs', 'nested with { Bounds = new(nested.Bounds.X + dx, nested.Bounds.Y + dy, nested.Bounds.Width, nested.Bounds.Height) }', 'nested with { TableLeft = nested.TableLeft + dx, Bounds = new(nested.Bounds.X + dx, nested.Bounds.Y + dy, nested.Bounds.Width, nested.Bounds.Height) }')
replace('src/TextSpace.Layout/TableLayouter.cs', '            foreach (var image in item.Content.Images) result.Images.Add(image with { Bounds = new(image.Bounds.X + dx, image.Bounds.Y + dy, image.Bounds.Width, image.Bounds.Height) });', '            foreach (var image in item.Content.Images) result.Images.Add(image with { Bounds = new(image.Bounds.X + dx, image.Bounds.Y + dy, image.Bounds.Width, image.Bounds.Height) });\n            foreach (var itemObject in item.Content.Objects) result.Objects.Add(itemObject with { Bounds = new(itemObject.Bounds.X + dx, itemObject.Bounds.Y + dy, itemObject.Bounds.Width, itemObject.Bounds.Height) });')
replace('src/TextSpace.Layout/TableLayouter.cs', '                    result.Height += layout.Height + 8; break;', '                    foreach (var itemObject in layout.Objects) result.Objects.Add(itemObject with { Bounds = new(itemObject.Bounds.X, itemObject.Bounds.Y + result.Height, itemObject.Bounds.Width, itemObject.Bounds.Height) });\n                    result.Height += layout.Height + 8; break;')
replace('src/TextSpace.Layout/TableLayouter.cs', '''                case ImageBlock image:
                    var ratio = Math.Min(1, Math.Min(width / image.Width, maximumImageHeight / image.Height));
                    var w = image.Width * ratio; var h = image.Height * ratio;
                    var left = image.Alignment == TextAlignment.Center ? (width - w) / 2 : image.Alignment == TextAlignment.Right ? width - w : 0;
                    result.Images.Add(new(image, new(left, result.Height, w, h))); result.Height += h + 8; break;''', '''                case VisualBlock visual:
                    var bounds = VisualGeometry.Place(visual, width, maximumImageHeight, 0, result.Height);
                    result.Objects.Add(new(visual, bounds));
                    if (visual is ImageBlock image) result.Images.Add(new(image, bounds));
                    result.Height += bounds.Height + Math.Max(0, visual.Placement.Y) + 8; break;''')
replace('src/TextSpace.Layout/TableFlowLayout.cs', '    public List<LayoutImage> Images { get; } = [];', '    public List<LayoutImage> Images { get; } = [];\n    public List<LayoutObject> Objects { get; } = [];\n    public VerticalIntervalIndex<LayoutObject> ObjectIndex { get; private set; } = null!;')
replace('src/TextSpace.Layout/TableFlowLayout.cs', '        ImageIndex = new(Images, i => i.Bounds.Y, i => i.Bounds.Bottom);', '        ImageIndex = new(Images, i => i.Bounds.Y, i => i.Bounds.Bottom);\n        ObjectIndex = new(Objects, o => o.Bounds.Y, o => o.Bounds.Bottom);')
replace('src/TextSpace.Layout/TableFlowLayout.cs', '        return cut;', '        foreach (var item in ObjectIndex.Intersect(end - 0.000001, end))\n            if (item.Bounds.Y >= start && item.Bounds.Bottom > end + 0.0001) cut = Math.Min(cut, item.Bounds.Y);\n        return cut;')
replace('src/TextSpace.Layout/TableFlowLayout.cs', '        var images = ImageIndex.Intersect(start, end).Where(i => i.Bounds.Y >= start - 0.0001 && i.Bounds.Y < end - 0.0001).ToArray();', '        var images = ImageIndex.Intersect(start, end).Where(i => i.Bounds.Y >= start - 0.0001 && i.Bounds.Y < end - 0.0001).ToArray();\n        var objects = ObjectIndex.Intersect(start, end).Where(i => i.Bounds.Y >= start - 0.0001 && i.Bounds.Y < end - 0.0001).ToArray();')
replace('src/TextSpace.Layout/TableFlowLayout.cs', '        foreach (var cell in CellIndex.Intersect(start, drawEnd))', '        if (objects.Length > 0) drawEnd = Math.Max(drawEnd, objects.Max(o => o.Bounds.Bottom));\n        foreach (var cell in CellIndex.Intersect(start, drawEnd))')
replace('src/TextSpace.Layout/TableFlowLayout.cs', 'page.Cells.Add(cell with { Bounds =', 'page.Cells.Add(cell with { TableLeft = cell.TableLeft + x, Bounds =')
replace('src/TextSpace.Layout/TableFlowLayout.cs', 'image with { Bounds = new(x + image.Bounds.X, y + image.Bounds.Y - start, image.Bounds.Width, image.Bounds.Height) }', 'image with { IsReplica = replica, Bounds = new(x + image.Bounds.X, y + image.Bounds.Y - start, image.Bounds.Width, image.Bounds.Height) }')
replace('src/TextSpace.Layout/TableFlowLayout.cs', '        return drawEnd - start;', '        foreach (var item in objects) page.Objects.Add(item with { IsReplica = replica, Bounds = new(x + item.Bounds.X, y + item.Bounds.Y - start, item.Bounds.Width, item.Bounds.Height) });\n        return drawEnd - start;')
replace('src/TextSpace.Skia/DocumentRenderer.cs', '    public string? SelectedImageId { get; init; }', '    public string? SelectedImageId { get; init; }\n    public string? HiddenObjectId { get; init; }')
replace('src/TextSpace.Skia/DocumentRenderer.cs', '    public DocumentLayout Layout(DocumentModel document) => (_layoutEngine ??= new PageLayoutEngine(Metrics)).Layout(document);', '    public DocumentLayout Layout(DocumentModel document)\n    {\n        ClearVisualLayouts();\n        return (_layoutEngine ??= new PageLayoutEngine(Metrics)).Layout(document);\n    }')
p = Path('src/TextSpace.Skia/DocumentRenderer.cs'); text = p.read_text()
a = text.index('        foreach (var image in page.Images)\n'); b = text.index('        foreach (var line in page.Lines)\n', a)
text = text[:a] + '        DrawVisualObjects(canvas, page, options, floating: false);\n' + text[b:]; p.write_text(text)
replace('src/TextSpace.Skia/DocumentRenderer.cs', '        if (options.ShowFormatting)\n        {', '        DrawVisualObjects(canvas, page, options, floating: true);\n        if (options.ShowFormatting)\n        {')
Path('scripts/integrate-visual-core.py').unlink()
Path('.github/workflows/visual-core-integration.yml').unlink()
print('Integrated visual models, layout, rendering and transaction tests; one-time tools removed.')
