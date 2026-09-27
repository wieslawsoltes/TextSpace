using System.Text.Json.Serialization;

namespace TextSpace.Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Paragraph), "paragraph")]
[JsonDerivedType(typeof(TableBlock), "table")]
[JsonDerivedType(typeof(ImageBlock), "image")]
[JsonDerivedType(typeof(PageBreakBlock), "pageBreak")]
[JsonDerivedType(typeof(SectionBreakBlock), "sectionBreak")]
[JsonDerivedType(typeof(ColumnBreakBlock), "columnBreak")]
public abstract class Block
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class PageBreakBlock : Block;
