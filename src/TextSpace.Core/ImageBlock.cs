namespace TextSpace.Core;

public sealed class ImageBlock : Block
{
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "image/png";
    public string AltText { get; set; } = "Picture";
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 240;
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;
}
