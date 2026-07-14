namespace UniPM.Api.Features.MaintenanceReview;

public sealed class SummaryOptions
{
    public const string SectionName = "Summary";

    public bool Enabled { get; set; }
    public string? ProviderKey { get; set; }
    public string? BaseAddress { get; set; }
    public string Path { get; set; } = "/v1/chat/completions";
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public string? ThinkingMode { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxPromptCharacters { get; set; } = 12000;
    public int MaxOutputCharacters { get; set; } = 4000;
    public int MaxSourceTextCharacters { get; set; } = 1500;
    public bool AllowRemoteProvider { get; set; }
}

