namespace UniPM.Api.Features.Auth;

public static class AuthPolicyCatalog
{
    public const string CanManageAssets = nameof(CanManageAssets);
    public const string CanManageSchedules = nameof(CanManageSchedules);
    public const string CanGenerateSchedules = nameof(CanGenerateSchedules);
    public const string CanReadSchedules = nameof(CanReadSchedules);
    public const string CanAssignScheduleSupervisors = nameof(CanAssignScheduleSupervisors);
    public const string CanAssignScheduleWorkers = nameof(CanAssignScheduleWorkers);
    public const string CanManagePreventiveMaintenanceForms = nameof(CanManagePreventiveMaintenanceForms);
    public const string CanInspectPreventiveMaintenanceSchedule = nameof(CanInspectPreventiveMaintenanceSchedule);
    public const string CanAccessCorrectiveMaintenanceHandoff = nameof(CanAccessCorrectiveMaintenanceHandoff);
    public const string CanManageWmsReferral = nameof(CanManageWmsReferral);
}
