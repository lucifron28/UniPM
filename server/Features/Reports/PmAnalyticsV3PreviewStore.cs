using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniPM.Api.Features.Auth;

namespace UniPM.Api.Features.Reports;

internal enum PmAnalyticsV3PreviewLookup
{
    Found,
    Unavailable
}

/// <summary>
/// Creates short-lived, user-bound preview tokens. Tokens carry only the
/// validated plan and can be verified by any API instance sharing the JWT key.
/// </summary>
internal sealed class PmAnalyticsV3PreviewStore
{
    internal const int MaximumTokenLength = 16_384;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly byte[] KeyPurpose = Encoding.UTF8.GetBytes(
        "UniPM.PmAnalyticsV3.PreviewToken.HmacSha256.v1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TimeProvider _timeProvider;
    private readonly byte[] _signingKey;

    public PmAnalyticsV3PreviewStore(
        TimeProvider timeProvider,
        JwtRuntimeConfiguration jwtConfiguration)
    {
        _timeProvider = timeProvider;
        _signingKey = HMACSHA256.HashData(jwtConfiguration.SigningKey.Key, KeyPurpose);
    }

    internal bool TryCreate(
        string userId,
        PmAnalyticsV3NormalizedPlan plan,
        out string previewId,
        out DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            previewId = string.Empty;
            expiresAtUtc = default;
            return false;
        }

        expiresAtUtc = _timeProvider.GetUtcNow().Add(Lifetime);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new PreviewPayload(userId, expiresAtUtc.UtcDateTime.Ticks, plan.Plan),
            JsonOptions);
        var encodedPayload = Base64UrlEncode(payload);
        var signature = HMACSHA256.HashData(_signingKey, Encoding.ASCII.GetBytes(encodedPayload));
        previewId = $"{encodedPayload}.{Base64UrlEncode(signature)}";
        if (previewId.Length <= MaximumTokenLength)
        {
            return true;
        }

        previewId = string.Empty;
        expiresAtUtc = default;
        return false;
    }

    internal PmAnalyticsV3PreviewLookup TryGet(
        string userId,
        string previewId,
        out PmAnalyticsV3NormalizedPlan? plan)
    {
        plan = null;
        if (string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(previewId)
            || previewId.Length > MaximumTokenLength)
        {
            return PmAnalyticsV3PreviewLookup.Unavailable;
        }

        try
        {
            var separator = previewId.IndexOf('.');
            if (separator <= 0 || separator != previewId.LastIndexOf('.'))
            {
                return PmAnalyticsV3PreviewLookup.Unavailable;
            }

            var encodedPayload = previewId[..separator];
            var suppliedSignature = Base64UrlDecode(previewId[(separator + 1)..]);
            var expectedSignature = HMACSHA256.HashData(
                _signingKey,
                Encoding.ASCII.GetBytes(encodedPayload));
            if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            {
                return PmAnalyticsV3PreviewLookup.Unavailable;
            }

            var payload = JsonSerializer.Deserialize<PreviewPayload>(
                Base64UrlDecode(encodedPayload),
                JsonOptions);
            if (payload is null
                || !string.Equals(payload.UserId, userId, StringComparison.Ordinal)
                || payload.ExpiresAtUtcTicks <= _timeProvider.GetUtcNow().UtcDateTime.Ticks
                || !PmAnalyticsV3PlanValidator.TryNormalize(payload.Plan, out var normalized, out _))
            {
                return PmAnalyticsV3PreviewLookup.Unavailable;
            }

            plan = normalized;
            return PmAnalyticsV3PreviewLookup.Found;
        }
        catch (Exception exception) when (
            exception is FormatException
                or JsonException
                or ArgumentException
                or OverflowException)
        {
            return PmAnalyticsV3PreviewLookup.Unavailable;
        }
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }

    private sealed record PreviewPayload(
        string UserId,
        long ExpiresAtUtcTicks,
        PmAnalyticsV3ReportPlan Plan);
}
