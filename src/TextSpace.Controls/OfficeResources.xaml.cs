namespace TextSpace.Controls;

public sealed partial class OfficeResources : ResourceDictionary
{
    public static OfficeResources Current { get; } = new();
    public OfficeResources() => InitializeComponent();
}
