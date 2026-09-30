namespace TextSpace.Core;

public readonly record struct BlockLocation(List<Block> Container, int Index)
{
    public Block Block => Container[Index];
}

public static class BlockTree
{
    public static IEnumerable<Block> Enumerate(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block is TableBlock table)
                foreach (var region in new TableGrid(table).Regions)
                    foreach (var child in Enumerate(region.Cell.Blocks)) yield return child;
        }
    }
    public static BlockLocation? Find(List<Block> blocks, string id)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Id == id) return new(blocks, i);
            if (blocks[i] is TableBlock table)
                foreach (var region in new TableGrid(table).Regions)
                    if (Find(region.Cell.Blocks, id) is { } found) return found;
        }
        return null;
    }
    public static VisualBlock CloneVisual(VisualBlock source, bool newIds = false)
    {
        VisualBlockRules.Validate(source);
        VisualBlock clone = source switch
        {
            ImageBlock image => new ImageBlock { Data = (byte[])image.Data.Clone(), ContentType = image.ContentType, AltText = image.AltText, Crop = image.Crop },
            ShapeBlock shape => new ShapeBlock { Kind = shape.Kind, Fill = shape.Fill, Stroke = shape.Stroke, StrokeWidth = shape.StrokeWidth,
                CornerRadius = shape.CornerRadius, Text = shape.Text, TextStyle = shape.TextStyle, VerticalAlignment = shape.VerticalAlignment, Padding = shape.Padding },
            EquationBlock equation => new EquationBlock { Root = equation.Root.Clone(newIds), FontSize = equation.FontSize, Color = equation.Color },
            _ => throw new ArgumentException("Unsupported visual object.", nameof(source))
        };
        clone.Id = newIds ? Guid.NewGuid().ToString("N") : source.Id;
        clone.Width = source.Width; clone.Height = source.Height; clone.Alignment = source.Alignment; clone.Placement = source.Placement;
        return clone;
    }
}
