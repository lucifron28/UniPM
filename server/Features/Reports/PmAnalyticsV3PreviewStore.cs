using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace UniPM.Api.Features.Reports;

internal enum PmAnalyticsV3PreviewLookup
{
    Found,
    Unavailable
}

/// <summary>
/// Holds short-lived report previews in process memory. Preview IDs are random,
/// user-bound, expire, and contain no serialized plan or source data.
/// </summary>
internal sealed class PmAnalyticsV3PreviewStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private const int MaximumEntries = 5_000;
    private readonly ConcurrentDictionary<string, StoredPreview> _previews = new(StringComparer.Ordinal);

    internal bool TryCreate(
        string userId,
        PmAnalyticsV3NormalizedPlan plan,
        out string previewId,
        out DateTimeOffset expiresAtUtc)
    {
        PruneExpired();
        if (string.IsNullOrWhiteSpace(userId) || _previews.Count >= MaximumEntries)
        {
            previewId = string.Empty;
            expiresAtUtc = default;
            return false;
        }

        previewId = CreateOpaqueId();
        expiresAtUtc = timeProvider.GetUtcNow().Add(Lifetime);
        return _previews.TryAdd(previewId, new StoredPreview(userId, expiresAtUtc, plan));
    }

    internal PmAnalyticsV3PreviewLookup TryGet(
        string userId,
        string previewId,
        out PmAnalyticsV3NormalizedPlan? plan)
    {
        plan = null;
        if (string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(previewId)
            || !_previews.TryGetValue(previewId, out var stored)
            || !string.Equals(stored.UserId, userId, StringComparison.Ordinal)
            || stored.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            if (!string.IsNullOrWhiteSpace(previewId)
                && _previews.TryGetValue(previewId, out stored)
                && stored.ExpiresAtUtc <= timeProvider.GetUtcNow())
            {
                _previews.TryRemove(previewId, out _);
            }

            return PmAnalyticsV3PreviewLookup.Unavailable;
        }

        plan = stored.Plan;
        return PmAnalyticsV3PreviewLookup.Found;
    }

    private void PruneExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in _previews)
        {
            if (entry.Value.ExpiresAtUtc <= now)
            {
                _previews.TryRemove(entry.Key, out _);
            }
        }
    }

    private static string CreateOpaqueId()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record StoredPreview(
        string UserId,
        DateTimeOffset ExpiresAtUtc,
        PmAnalyticsV3NormalizedPlan Plan);
}
