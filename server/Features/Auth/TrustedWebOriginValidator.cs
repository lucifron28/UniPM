namespace UniPM.Api.Features.Auth;

internal sealed class TrustedWebOriginValidator(AuthSessionRuntimeConfiguration configuration)
{
    public bool IsTrusted(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        return string.IsNullOrWhiteSpace(origin)
            || TryNormalize(origin, out var normalized)
                && string.Equals(normalized, configuration.WebOrigin, StringComparison.Ordinal);
    }

    public static string Normalize(string value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            throw new InvalidOperationException("AuthSession WebOrigin must be an absolute HTTP or HTTPS origin without credentials, path, query, fragment, or wildcard.");
        }

        return normalized;
    }

    private static bool TryNormalize(string value, out string normalized)
    {
        normalized = string.Empty;
        if (value.Contains('*', StringComparison.Ordinal)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || uri.AbsolutePath is not "/" and not "")
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}
