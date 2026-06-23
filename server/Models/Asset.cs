using System;

namespace UniPM.Api.Models;

public class Asset
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    
    // Vector search embedding for hybrid search (to be populated later via Ollama/Qwen3)
    public string? DescriptionEmbedding { get; set; } // Note: SQL Server 2025 vector type would be mapped here eventually

    // Flexible fields until GSD confirms
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? QrCodeValue { get; set; }
    public string Status { get; set; } = "Active";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
