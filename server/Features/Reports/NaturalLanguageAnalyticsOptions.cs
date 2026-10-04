namespace UniPM.Api.Features.Reports;

public sealed class NaturalLanguageAnalyticsOptions
{
    public const string SectionName = "NaturalLanguageAnalytics";

    public bool Enabled { get; set; }
    public string BaseAddress { get; set; } = "http://127.0.0.1:11434/";
    public string Model { get; set; } = "qwen3:4b-instruct";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxOutputTokens { get; set; } = 1024;
    public int MaxResponseBytes { get; set; } = 16 * 1024;
}
