namespace UniPM.Api.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool MetricsEnabled { get; init; }
}
