namespace UniPM.Api.Features.Inspections;

public static class InspectionFollowUpStatusCatalog
{
    public const string NoReferralRequired = "NoReferralRequired";
    public const string CorrectiveFollowUpPending = "CorrectiveFollowUpPending";
    public const string ReferredToWms = "ReferredToWms";

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized == NoReferralRequired
            || normalized == CorrectiveFollowUpPending
            || normalized == ReferredToWms;
    }

    public static string For(bool isOperational, bool hasWmsReferral)
    {
        if (hasWmsReferral)
        {
            return ReferredToWms;
        }

        return isOperational
            ? NoReferralRequired
            : CorrectiveFollowUpPending;
    }
}
