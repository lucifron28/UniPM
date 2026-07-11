using System;

namespace UniPM.Api.Models;

public class Asset
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stores the canonical uppercase asset identifier normalized by AssetCodeValue.
    /// </summary>
    public string AssetCode { get; set; } = string.Empty;

    /// <summary>
    /// Stores the canonical category code validated and normalized by AssetCategoryCatalog.
    /// </summary>
    public string AssetCategory { get; set; } = string.Empty;

    /// <summary>
    /// Deferred retrieval field; embedding generation and storage are not part of the current contract.
    /// </summary>
    public string? DescriptionEmbedding { get; set; }

    // Provisional free-text fields pending GSD confirmation.
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? QrCodeValue { get; set; }

    /// <summary>
    /// Stores the canonical controlled status from AssetStatusCatalog.
    /// Persisted statuses may be broader than values currently written by API commands;
    /// the catalog does not define implemented status transitions.
    /// </summary>
    public string Status { get; set; } = "Active";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
