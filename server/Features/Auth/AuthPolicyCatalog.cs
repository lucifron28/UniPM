namespace UniPM.Api.Features.Auth;

public static class AuthPolicyCatalog
{
    public const string CanManageAssets = nameof(CanManageAssets);
    public const string CanManageSchedules = nameof(CanManageSchedules);
    public const string CanManagePreventiveMaintenanceForms = nameof(CanManagePreventiveMaintenanceForms);
    public const string CanAccessCorrectiveMaintenanceHandoff = nameof(CanAccessCorrectiveMaintenanceHandoff);
    public const string CanReviewMaintenanceHistory = nameof(CanReviewMaintenanceHistory);
}
