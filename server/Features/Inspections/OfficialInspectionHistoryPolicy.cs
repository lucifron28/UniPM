using Microsoft.EntityFrameworkCore;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

internal static class OfficialInspectionHistoryPolicy
{
    public static IQueryable<InspectionRecord> WhereOfficial(
        this IQueryable<InspectionRecord> query)
    {
        return query.Where(inspection =>
            inspection.PreventiveMaintenanceFormId == null
            || inspection.PreventiveMaintenanceForm!.Status
                == PreventiveMaintenanceFormStatusCatalog.Acknowledged);
    }
}
