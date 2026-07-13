namespace UniPM.Api.Features.Auth;

public static class AuthPolicyCatalog
{
    public const string CanManageAssets = nameof(CanManageAssets);
    public const string CanManageSchedules = nameof(CanManageSchedules);
    public const string CanSubmitInspections = nameof(CanSubmitInspections);
    public const string CanReviewMaintenanceHistory = nameof(CanReviewMaintenanceHistory);
}
