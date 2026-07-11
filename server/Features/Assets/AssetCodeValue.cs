namespace UniPM.Api.Features.Assets;

internal static class AssetCodeValue
{
    internal const int MaxLength = 64;
    internal const int QrCodeMaxLength = 128;

    internal static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var lineNormalized = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return string.Join(
                ' ',
                lineNormalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToUpperInvariant();
    }

    internal static string NormalizeQrCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToUpperInvariant();
    }
}
