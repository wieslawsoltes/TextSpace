namespace TextSpace.Core;

public sealed record PageSettings
{
    public double Width { get; init; } = 612;
    public double Height { get; init; } = 792;
    public double MarginLeft { get; init; } = 72;
    public double MarginRight { get; init; } = 72;
    public double MarginTop { get; init; } = 72;
    public double MarginBottom { get; init; } = 72;
    public double HeaderDistance { get; init; } = 30;
    public double FooterDistance { get; init; } = 30;
    public int Columns { get; init; } = 1;
    public double ColumnGap { get; init; } = 24;
    public string Color { get; init; } = "#FFFFFF";
    public string? Watermark { get; init; }
    public double ContentWidth => Width - MarginLeft - MarginRight;
    public double ContentHeight => Height - MarginTop - MarginBottom;
    public double ColumnWidth => (ContentWidth - ColumnGap * (Columns - 1)) / Columns;
    public PageSettings Landscape() => this with { Width = Math.Max(Width, Height), Height = Math.Min(Width, Height) };
    public PageSettings Portrait() => this with { Width = Math.Min(Width, Height), Height = Math.Max(Width, Height) };
}
