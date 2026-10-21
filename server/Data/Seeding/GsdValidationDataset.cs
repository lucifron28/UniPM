using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Data.Seeding;

internal sealed record GsdValidationDatasetContents(
    IReadOnlyList<Asset> Assets,
    IReadOnlyList<PreventiveMaintenanceSchedule> Schedules,
    IReadOnlyList<InspectionRecord> Inspections,
    IReadOnlyList<PreventiveMaintenanceForm> Forms,
    IReadOnlyList<PreventiveMaintenanceAcknowledgement> Acknowledgements,
    IReadOnlyList<InspectionWmsReferral> WmsReferrals,
    IReadOnlyList<InspectionWmsReferralAudit> WmsReferralAudits,
    int OnTimeInspections,
    int LateInspections,
    int NonOperationalInspections,
    int AcknowledgedNonOperationalInspections,
    int AwaitingWmsReferral,
    int ReferredToWms);

internal static class GsdValidationDatasetBuilder
{
    private static readonly DateTimeOffset AssetRegistrationDate = At(2026, 4, 1, 8);
    private const string SyntheticSignaturePng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=";
    private static readonly string[] FictionalSignatories =
        ["Mara Santos", "Nilo Reyes", "Lira Navarro", "Tomas Rivera"];

    private static readonly DepartmentDefinition[] Departments =
    [
        new("CCMS", "CCMS Building", ["Ground floor corridor", "Second floor laboratory entrance", "Third floor stair landing", "North wing exit"]),
        new("Administration", "Administration Building", ["Ground floor service counter", "Second floor corridor", "Third floor stair landing", "East wing exit"]),
        new("Library", "University Library", ["Ground floor reading area", "Second floor stacks", "Third floor study area", "West wing exit"]),
        new("CNAHS", "CNAHS Building", ["Ground floor clinic corridor", "Second floor skills laboratory", "Third floor stair landing", "South wing exit"])
    ];

    private static readonly CategoryDefinition[] Categories =
    [
        new(AssetCategoryCatalog.FireExtinguisher, "FE", "CCMS", [5, 8, 11]),
        new(AssetCategoryCatalog.FireAlarm, "FA", "Library", [6, 12]),
        new(AssetCategoryCatalog.EmergencyLight, "EL", "CNAHS", [6, 12]),
        new(AssetCategoryCatalog.WaterDrinkingStation, "WDS", "Administration", [5, 8, 11])
    ];

    internal static GsdValidationDatasetContents Build(
        Guid inspectorUserId,
        Guid supervisorUserId,
        Guid gsdUserId)
    {
        var assets = new List<Asset>(200);
        var schedules = new List<PreventiveMaintenanceSchedule>(500);
        var assetsById = new Dictionary<Guid, Asset>();

        foreach (var category in Categories)
        {
            var departmentOrder = DepartmentOrderFor(category.LiveDepartment);
            for (var departmentIndex = 0; departmentIndex < departmentOrder.Count; departmentIndex++)
            {
                var department = departmentOrder[departmentIndex];
                var departmentCount = departmentIndex < 2 ? 13 : 12;
                for (var localNumber = 1; localNumber <= departmentCount; localNumber++)
                {
                    var id = StableId($"asset:{category.AssetCategory}:{department.Code}:{localNumber:D3}");
                    var asset = new Asset
                    {
                        Id = id,
                        AssetCode = $"{category.Code}-{department.Code.ToUpperInvariant()}-{localNumber:D3}",
                        AssetCategory = category.AssetCategory,
                        Building = department.Building,
                        Department = department.Code,
                        Location = department.Locations[(localNumber - 1) % department.Locations.Length],
                        QrCodeValue = AssetQrCodeValue.Create(category.AssetCategory, id),
                        Status = AssetStatusCatalog.Active,
                        CreatedAt = AssetRegistrationDate,
                        UpdatedAt = AssetRegistrationDate
                    };
                    assets.Add(asset);
                    assetsById.Add(id, asset);
                }
            }

            foreach (var asset in assets.Where(asset => asset.AssetCategory == category.AssetCategory))
            foreach (var month in category.ScheduleMonths)
            {
                var pmCycle = $"2026-{month:D2}";
                var isLiveBatch = month == LiveMonth(category)
                    && asset.Department == category.LiveDepartment;
                var isCompletedCycle = month < 10 && !isLiveBatch;
                var schedule = new PreventiveMaintenanceSchedule
                {
                    Id = StableId($"schedule:{asset.Id:D}:{pmCycle}"),
                    AssetId = asset.Id,
                    Asset = asset,
                    PmCycle = pmCycle,
                    ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle),
                    PeriodType = SchedulePeriodTypeCatalog.Quarter,
                    Quarter = $"Q{(month - 1) / 3 + 1}",
                    Year = 2026,
                    AcademicYear = AcademicYearFor(month),
                    Status = isCompletedCycle ? ScheduleStatusCatalog.Completed : ScheduleStatusCatalog.Due,
                    AssignedToUserId = isCompletedCycle ? inspectorUserId : null,
                    AssignedSupervisorUserId = isCompletedCycle ? supervisorUserId : null,
                    CreatedAt = AssetRegistrationDate,
                    UpdatedAt = AssetRegistrationDate
                };
                schedules.Add(schedule);
            }
        }

        var completedBatches = schedules
            .Where(schedule => schedule.Status == ScheduleStatusCatalog.Completed)
            .GroupBy(schedule =>
            {
                var asset = assetsById[schedule.AssetId];
                return new BatchKey(asset.AssetCategory, asset.Department!, schedule.PmCycle);
            })
            .OrderBy(batch => CategoryOrder(batch.Key.AssetCategory))
            .ThenBy(batch => batch.Key.PmCycle, StringComparer.Ordinal)
            .ThenBy(batch => batch.Key.Department, StringComparer.Ordinal)
            .ToArray();

        var forms = new List<PreventiveMaintenanceForm>(20);
        var inspections = new List<InspectionRecord>(248);
        var acknowledgements = new List<PreventiveMaintenanceAcknowledgement>(10);
        var referrals = new List<InspectionWmsReferral>();
        var referralAudits = new List<InspectionWmsReferralAudit>();
        var inspectionOrdinal = 0;
        var onTime = 0;
        var late = 0;
        var nonOperational = 0;
        var acknowledgedNonOperational = 0;
        var awaitingReferral = 0;
        var balancedAcknowledgedCondition = false;
        var balancedSubmittedCondition = false;

        for (var batchOrdinal = 0; batchOrdinal < completedBatches.Length; batchOrdinal++)
        {
            var batch = completedBatches[batchOrdinal];
            var batchSchedules = batch.OrderBy(schedule => assetsById[schedule.AssetId].AssetCode, StringComparer.Ordinal).ToArray();
            var category = Categories.Single(item => item.AssetCategory == batch.Key.AssetCategory);
            var month = int.Parse(batch.Key.PmCycle.AsSpan(5), CultureInfo.InvariantCulture);
            var isAcknowledged = batchOrdinal % 2 == 0;
            var form = new PreventiveMaintenanceForm
            {
                Id = StableId($"form:{batch.Key.AssetCategory}:{batch.Key.Department}:{batch.Key.PmCycle}"),
                FileNumber = $"PM-2026-{category.Code}-{batch.Key.Department.ToUpperInvariant()}-{month:D2}",
                AssetCategory = batch.Key.AssetCategory,
                Building = assetsById[batchSchedules[0].AssetId].Building,
                Department = batch.Key.Department,
                PmCycle = batch.Key.PmCycle,
                PeriodType = SchedulePeriodTypeCatalog.Quarter,
                Quarter = $"Q{(month - 1) / 3 + 1}",
                Year = 2026,
                AcademicYear = AcademicYearFor(month),
                Status = isAcknowledged
                    ? PreventiveMaintenanceFormStatusCatalog.Acknowledged
                    : PreventiveMaintenanceFormStatusCatalog.Submitted,
                CreatedByUserId = inspectorUserId,
                SubmittedByUserId = inspectorUserId,
                CreatedAt = At(2026, month, 1, 9),
                UpdatedAt = At(2026, month, 1, 9)
            };
            forms.Add(form);

            var batchInspections = new List<InspectionRecord>(batchSchedules.Length);
            var batchReferrals = new List<(InspectionRecord Inspection, string ExternalPmNumber)>();
            foreach (var schedule in batchSchedules)
            {
                var asset = assetsById[schedule.AssetId];
                var isLate = inspectionOrdinal % 4 == 0;
                var completedAt = isLate
                    ? schedule.ScheduleDate.AddDays(3).AddHours(9)
                    : schedule.ScheduleDate.AddDays(-2).AddHours(-3);
                if (isLate)
                {
                    late++;
                }
                else
                {
                    onTime++;
                }

                var isOperational = inspectionOrdinal % 5 != 0;
                if (isAcknowledged
                    && !isOperational
                    && inspectionOrdinal % 2 == 1
                    && !balancedAcknowledgedCondition)
                {
                    isOperational = true;
                    balancedAcknowledgedCondition = true;
                }
                else if (!isAcknowledged && isOperational && !balancedSubmittedCondition)
                {
                    isOperational = false;
                    balancedSubmittedCondition = true;
                }
                if (!isOperational)
                {
                    nonOperational++;
                    if (isAcknowledged)
                    {
                        acknowledgedNonOperational++;
                    }
                }

                schedule.CompletedAt = completedAt;
                schedule.UpdatedAt = completedAt;
                var hasReferral = isAcknowledged && !isOperational && inspectionOrdinal % 2 == 0;
                var inspection = new InspectionRecord
                {
                    Id = StableId($"inspection:{schedule.Id:D}"),
                    ScheduleId = schedule.Id,
                    Schedule = schedule,
                    PreventiveMaintenanceFormId = form.Id,
                    PreventiveMaintenanceForm = form,
                    AssetId = asset.Id,
                    Asset = asset,
                    InspectorUserId = inspectorUserId,
                    DateInspected = completedAt,
                    StartedAt = completedAt.AddMinutes(-45),
                    CompletedAt = completedAt,
                    DateAccomplished = completedAt,
                    IsOperational = isOperational,
                    Remarks = RemarksFor(category.AssetCategory, isOperational, inspectionOrdinal),
                    ActionsRecommendations = RecommendationFor(category.AssetCategory, isOperational),
                    WaterReplaceCarbonFilter = category.AssetCategory == AssetCategoryCatalog.WaterDrinkingStation
                        ? (inspectionOrdinal % 2 == 0)
                        : null,
                    WaterReplaceSedimentFilter = category.AssetCategory == AssetCategoryCatalog.WaterDrinkingStation
                        ? (inspectionOrdinal % 3 == 0)
                        : null,
                    WaterCheckUvLight = category.AssetCategory == AssetCategoryCatalog.WaterDrinkingStation
                        ? (inspectionOrdinal % 4 == 0)
                        : null,
                    CreatedAt = completedAt,
                    UpdatedAt = completedAt
                };
                inspections.Add(inspection);
                batchInspections.Add(inspection);
                form.Inspections.Add(inspection);

                if (isAcknowledged && !isOperational && !hasReferral)
                {
                    awaitingReferral++;
                }
                if (hasReferral)
                {
                    batchReferrals.Add((inspection, $"WMS-2026-{referrals.Count + batchReferrals.Count + 1:D4}"));
                }

                inspectionOrdinal++;
            }

            var fieldWorkCompletedAt = batchInspections.Max(inspection => inspection.CompletedAt!.Value);
            form.FieldWorkCompletedAt = fieldWorkCompletedAt;
            form.SubmittedAt = fieldWorkCompletedAt.AddHours(2);
            form.UpdatedAt = form.SubmittedAt.Value;
            var acknowledgedAt = form.SubmittedAt.Value.AddDays(1);
            if (isAcknowledged)
            {
                var signatureBytes = Convert.FromBase64String(SyntheticSignaturePng);
                acknowledgements.Add(new PreventiveMaintenanceAcknowledgement
                {
                    Id = StableId($"acknowledgement:{form.Id:D}"),
                    FormId = form.Id,
                    Form = form,
                    SignatoryName = FictionalSignatories[(batchOrdinal / 2) % FictionalSignatories.Length],
                    SignatoryPosition = "Department Head",
                    SignatureData = SyntheticSignaturePng,
                    SignatureContentType = "image/png",
                    SignatureChecksum = Convert.ToHexString(SHA256.HashData(signatureBytes)),
                    CapturedByUserId = gsdUserId,
                    AcknowledgedAt = acknowledgedAt
                });
            }

            foreach (var (inspection, externalPmNumber) in batchReferrals)
            {
                var recordedAt = acknowledgedAt.AddHours(1);
                referrals.Add(new InspectionWmsReferral
                {
                    InspectionId = inspection.Id,
                    Inspection = inspection,
                    ExternalPmNumber = externalPmNumber,
                    Revision = 1,
                    RecordedByUserId = gsdUserId,
                    RecordedAt = recordedAt,
                    LastUpdatedByUserId = gsdUserId,
                    LastUpdatedAt = recordedAt
                });
                referralAudits.Add(new InspectionWmsReferralAudit
                {
                    Id = StableId($"wms-audit:{inspection.Id:D}:1"),
                    InspectionId = inspection.Id,
                    Inspection = inspection,
                    PreviousExternalPmNumber = null,
                    NewExternalPmNumber = externalPmNumber,
                    Revision = 1,
                    ChangedByUserId = gsdUserId,
                    ChangedAt = recordedAt
                });
            }
        }

        return new GsdValidationDatasetContents(
            assets,
            schedules,
            inspections,
            forms,
            acknowledgements,
            referrals,
            referralAudits,
            onTime,
            late,
            nonOperational,
            acknowledgedNonOperational,
            awaitingReferral,
            referrals.Count);
    }

    internal static Guid StableId(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"UniPM:GsdValidation:v1:{key}"))[..16];
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static string AcademicYearFor(int month) =>
        month <= 6 ? "2025-2026" : "2026-2027";

    private static string RemarksFor(string category, bool operational, int ordinal)
    {
        var variants = category switch
        {
            AssetCategoryCatalog.FireExtinguisher => operational
                ? new[]
                {
                    "The pressure indicator was in range and the extinguisher remained accessible.",
                    "The safety seal and mounting bracket appeared intact during the visual check."
                }
                : new[]
                {
                    "The pressure indicator was outside its expected range during inspection.",
                    "The tamper seal was missing and readiness could not be confirmed.",
                    "The mounting bracket was loose and the unit needs maintenance review."
                },
            AssetCategoryCatalog.FireAlarm => operational
                ? new[]
                {
                    "Panel indicators were normal and the sampled alarm response was observed.",
                    "The panel showed no active fault and the sampled detector response was normal."
                }
                : new[]
                {
                    "A sampled detector did not produce the expected panel response.",
                    "The alarm panel displayed a fault indication during the inspection."
                },
            AssetCategoryCatalog.EmergencyLight => operational
                ? new[]
                {
                    "The charging indicator was on and the emergency light operated during the brief test.",
                    "The battery indicator was normal and the light remained available on test."
                }
                : new[]
                {
                    "The emergency light did not remain illuminated during the brief battery test.",
                    "The charging indicator was off and the battery requires maintenance review."
                },
            AssetCategoryCatalog.WaterDrinkingStation => operational
                ? new[]
                {
                    "Water flow was available and the filter service indicators were checked.",
                    "The dispenser operated normally during the flow and filter-status check."
                }
                : new[]
                {
                    "The filter service indicator was overdue during the water-station check.",
                    "Water flow was interrupted during the dispenser check."
                },
            _ => throw new InvalidOperationException($"Unsupported GSD validation category '{category}'.")
        };

        return variants[ordinal % variants.Length];
    }

    private static string RecommendationFor(string category, bool operational)
    {
        if (operational)
        {
            return category == AssetCategoryCatalog.WaterDrinkingStation
                ? "Continue routine flow and filter-status checks on the scheduled cycle."
                : "Continue the scheduled inspection cycle and record any change in condition.";
        }

        return category switch
        {
            AssetCategoryCatalog.FireExtinguisher => "Route the pressure, seal, or mounting observation for maintenance review through the external WMS process; no repair is recorded here.",
            AssetCategoryCatalog.FireAlarm => "Route the panel or detector response observation for maintenance review through the external WMS process; no repair is recorded here.",
            AssetCategoryCatalog.EmergencyLight => "Route the battery or charging observation for maintenance review through the external WMS process; no repair is recorded here.",
            AssetCategoryCatalog.WaterDrinkingStation => "Route the flow or filter observation for maintenance review through the external WMS process; no repair is recorded here.",
            _ => throw new InvalidOperationException($"Unsupported GSD validation category '{category}'.")
        };
    }

    private static IReadOnlyList<DepartmentDefinition> DepartmentOrderFor(string liveDepartment)
    {
        var liveIndex = Array.FindIndex(Departments, department => department.Code == liveDepartment);
        return Enumerable.Range(0, Departments.Length)
            .Select(offset => Departments[(liveIndex + offset) % Departments.Length])
            .ToArray();
    }

    private static int CategoryOrder(string category) =>
        Array.FindIndex(Categories, item => item.AssetCategory == category);

    private static int LiveMonth(CategoryDefinition category) =>
        category.AssetCategory is AssetCategoryCatalog.FireExtinguisher or AssetCategoryCatalog.WaterDrinkingStation
            ? 8
            : 6;

    private static DateTimeOffset At(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.FromHours(8));

    private sealed record DepartmentDefinition(
        string Code,
        string Building,
        string[] Locations);

    private sealed record CategoryDefinition(
        string AssetCategory,
        string Code,
        string LiveDepartment,
        int[] ScheduleMonths);

    private sealed record BatchKey(
        string AssetCategory,
        string Department,
        string PmCycle);
}
