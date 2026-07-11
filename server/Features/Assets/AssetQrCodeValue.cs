using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Assets;

internal static class AssetQrCodeValue
{
    public static string Create(string assetCategory, Guid assetId)
    {
        if (!AssetCategoryCatalog.TryNormalize(assetCategory, out var normalizedCategory))
        {
            throw new ArgumentException("A supported asset category is required.", nameof(assetCategory));
        }

        var categoryToken = normalizedCategory.ToUpperInvariant().Replace(" ", string.Empty);
        var idToken = assetId.ToString()[..8].ToUpperInvariant();
        return AssetCodeValue.NormalizeQrCode($"UNIPM-{categoryToken}-{idToken}");
    }
}
