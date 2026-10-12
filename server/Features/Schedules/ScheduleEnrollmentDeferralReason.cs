using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

internal static class ScheduleEnrollmentDeferralReason
{
    internal const string BatchAssigned = "BatchAssigned";
    internal const string WorkInProgress = "WorkInProgress";
    internal const string CycleCompleted = "CycleCompleted";
    internal const string CycleCancelled = "CycleCancelled";
    internal const string InspectionStarted = "InspectionStarted";
    internal const string FormSubmitted = "FormSubmitted";
    internal const string FormAcknowledged = "FormAcknowledged";

    internal static IReadOnlyList<string> Values { get; } =
    [BatchAssigned, WorkInProgress, CycleCompleted, CycleCancelled, InspectionStarted, FormSubmitted, FormAcknowledged];

    internal static string GetDescription(string code) => code switch
    {
        BatchAssigned => "The batch already had an assigned Supervisor or Inspector.",
        WorkInProgress => "Inspection work had started in the batch.",
        CycleCompleted => "The PM cycle was already completed.",
        CycleCancelled => "The PM cycle was already cancelled.",
        InspectionStarted => "An inspection record already existed for the batch.",
        FormSubmitted => "The batch form had already been submitted.",
        FormAcknowledged => "The batch form had already been acknowledged.",
        _ => "The existing PM batch was locked against additional enrollment."
    };
}

internal static class ScheduleEnrollmentDeferralStatusCatalog
{
    internal const string NeedsReview = "NeedsReview";
    internal const string Reviewed = "Reviewed";

    internal static bool TryNormalize(string? value, out string status)
    {
        status = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value.Trim(), NeedsReview, StringComparison.OrdinalIgnoreCase))
        {
            status = NeedsReview;
            return true;
        }

        if (string.Equals(value.Trim(), Reviewed, StringComparison.OrdinalIgnoreCase))
        {
            status = Reviewed;
            return true;
        }

        return false;
    }

    internal static string ToLabel(ScheduleEnrollmentDeferral deferral) =>
        deferral.ReviewedAt is null ? "Needs review" : "Reviewed";
}
