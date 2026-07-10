namespace UniPM.Api.Features.Assets;

internal static class AssetQrCodeValue
{
    public static string Create(string assetCategory, Guid assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetCategory);

        var normalizedCategory = assetCategory.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        return $"UNIPM-{normalizedCategory}-{assetId.ToString()[..8]}";
    }
}
