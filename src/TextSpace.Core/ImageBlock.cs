namespace TextSpace.Core;

public sealed class ImageBlock : VisualBlock
{
    public ImageBlock() { Width = 360; Height = 240; }
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "image/png";
    public string AltText { get; set; } = "Picture";
    public ImageCrop Crop { get; set; } = new();
}

/// <summary>Fractions removed from the four source-image edges. Original bytes are retained.</summary>
public sealed record ImageCrop
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Right { get; init; }
    public double Bottom { get; init; }
}
