namespace TextSpace.Layout;

/// <summary>Metric providers increment this version whenever fonts or metric configuration change.</summary>
public interface IVersionedTextMetrics : ITextMetrics
{
    long MetricsVersion { get; }
}
