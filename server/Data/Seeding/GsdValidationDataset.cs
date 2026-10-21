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
    private static readonly DateTimeOffset AssetRegistrationDate = At(2024, 1, 1, 8);
    private static readonly DateTimeOffset OriginalFixtureUpdatedAt = At(2026, 4, 1, 8);
    private const string SyntheticSignaturePng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=";
    private static readonly string[] FictionalSignatories =
        ["Mara Santos", "Nilo Reyes", "Lira Navarro", "Tomas Rivera"];

    private static readonly DepartmentDefinition[] Departments =
    [
        new("CCMS", "CCMS", "CCMS Building", ["Ground floor corridor", "Second floor laboratory entrance", "Third floor stair landing", "North wing exit"]),
        new("Administration", "Administration", "Administration Building", ["Ground floor service counter", "Second floor corridor", "Third floor stair landing", "East wing exit"]),
        new("Library", "Library", "Library Complex", ["Ground floor reading area", "Second floor stacks", "Third floor study area", "West wing exit"]),
        new("CNAHS", "CNAHS", "CNAHS Building", ["Ground floor clinic corridor", "Second floor skills laboratory", "Third floor stair landing", "South wing exit"])
    ];

    private static readonly DepartmentDefinition[] AdditionalDepartments =
    [
        new("GSD", "General Services Department", "Library Complex", ["Ground floor service office", "Ground floor equipment room", "First floor corridor", "West service entrance"]),
        new("PROC", "Procurement", "Library Complex", ["Ground floor receiving area", "Ground floor records office", "First floor corridor", "East service entrance"]),
        new("ICTD", "ICTD", "Library Complex", ["Ground floor support desk", "Ground floor network room entrance", "First floor corridor", "North service entrance"])
    ];

    private static readonly CategoryDefinition[] Categories =
    [
        new(AssetCategoryCatalog.FireExtinguisher, "FE", "CCMS", [2, 5, 8, 11]),
        new(AssetCategoryCatalog.FireAlarm, "FA", "Library", [6, 12]),
        new(AssetCategoryCatalog.EmergencyLight, "EL", "CNAHS", [6, 12]),
        new(AssetCategoryCatalog.WaterDrinkingStation, "WDS", "Administration", [2, 5, 8, 11])
    ];

    internal static GsdValidationDatasetContents Build(
        Guid inspectorUserId,
        Guid supervisorUserId,
        Guid gsdUserId)
    {
        var assets = new List<Asset>(248);
        var schedules = new List<PreventiveMaintenanceSchedule>(2_232);
        var assetsById = new Dictionary<Guid, Asset>();
        var originalAssetIds = new HashSet<Guid>();

        foreach (var category in Categories)
        {
            var departmentOrder = DepartmentOrderFor(category.LiveDepartment);
            var assetDepartments = departmentOrder
                .Select((department, index) => (Department: department, Count: index < 2 ? 13 : 12, IsOriginal: true))
                .Concat(AdditionalDepartments.Select(department => (Department: department, Count: 4, IsOriginal: false)));
            foreach (var (department, departmentCount, isOriginal) in assetDepartments)
            {
                for (var localNumber = 1; localNumber <= departmentCount; localNumber++)
                {
                    var id = StableId($"asset:{category.AssetCategory}:{department.Code}:{localNumber:D3}");
                    var asset = new Asset
                    {
                        Id = id,
                        AssetCode = $"{category.Code}-{department.Code.ToUpperInvariant()}-{localNumber:D3}",
                        AssetCategory = category.AssetCategory,
                        Building = department.Building,
                        Department = department.Name,
                        Location = department.Locations[(localNumber - 1) % department.Locations.Length],
                        QrCodeValue = AssetQrCodeValue.Create(category.AssetCategory, id),
                        Status = AssetStatusCatalog.Active,
                        CreatedAt = AssetRegistrationDate,
                        UpdatedAt = isOriginal ? OriginalFixtureUpdatedAt : AssetRegistrationDate
                    };
                    assets.Add(asset);
                    assetsById.Add(id, asset);
                    if (isOriginal)
                    {
                        originalAssetIds.Add(id);
                    }
                }
            }

            foreach (var asset in assets.Where(asset => asset.AssetCategory == category.AssetCategory))
            foreach (var year in new[] { 2024, 2025, 2026 })
            foreach (var month in category.ScheduleMonths)
            {
                var pmCycle = $"{year}-{month:D2}";
                var isLiveBatch = year == 2026
                    && month == LiveMonth(category)
                    && asset.Department == category.LiveDepartment;
                var isUpcomingCycle = year == 2026 && month >= 10;
                var isCompletedCycle = !isUpcomingCycle && !isLiveBatch;
                var isOriginal2026Schedule = year == 2026
                    && month != 2
                    && originalAssetIds.Contains(asset.Id);
                var createdAt = isOriginal2026Schedule
                    ? OriginalFixtureUpdatedAt
                    : At(year, 1, 1, 8);
                var schedule = new PreventiveMaintenanceSchedule
                {
                    Id = StableId($"schedule:{asset.Id:D}:{pmCycle}"),
                    AssetId = asset.Id,
                    Asset = asset,
                    PmCycle = pmCycle,
                    ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle),
                    PeriodType = SchedulePeriodTypeCatalog.Quarter,
                    Quarter = $"Q{(month - 1) / 3 + 1}",
                    Year = year,
                    AcademicYear = AcademicYearFor(year, month),
                    Status = isCompletedCycle ? ScheduleStatusCatalog.Completed : ScheduleStatusCatalog.Due,
                    AssignedToUserId = isCompletedCycle ? inspectorUserId : null,
                    AssignedSupervisorUserId = isCompletedCycle ? supervisorUserId : null,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                schedules.Add(schedule);
            }
        }

        var completedBatchGroups = schedules
            .Where(schedule => schedule.Status == ScheduleStatusCatalog.Completed)
            .GroupBy(schedule =>
            {
                var asset = assetsById[schedule.AssetId];
                return new BatchKey(asset.AssetCategory, asset.Department!, schedule.PmCycle);
            })
            .ToArray();
        var original2026Batches = completedBatchGroups
            .Where(batch => IsOriginal2026Batch(batch.Key))
            .OrderBy(batch => CategoryOrder(batch.Key.AssetCategory))
            .ThenBy(batch => batch.Key.PmCycle, StringComparer.Ordinal)
            .ThenBy(batch => batch.Key.Department, StringComparer.Ordinal);
        var appendedBatches = completedBatchGroups
            .Where(batch => !IsOriginal2026Batch(batch.Key))
            .OrderByDescending(batch => CycleYear(batch.Key.PmCycle))
            .ThenBy(batch => CategoryOrder(batch.Key.AssetCategory))
            .ThenBy(batch => batch.Key.PmCycle, StringComparer.Ordinal)
            .ThenBy(batch => batch.Key.Department, StringComparer.Ordinal);
        var completedBatches = original2026Batches.Concat(appendedBatches).ToArray();

        var forms = new List<PreventiveMaintenanceForm>(220);
        var inspections = new List<InspectionRecord>(1_932);
        var acknowledgements = new List<PreventiveMaintenanceAcknowledgement>(110);
        var referrals = new List<InspectionWmsReferral>();
        var referralAudits = new List<InspectionWmsReferralAudit>();
        var wmsNumberByYear = new Dictionary<int, int>();
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
            var year = CycleYear(batch.Key.PmCycle);
            var month = int.Parse(batch.Key.PmCycle.AsSpan(5), CultureInfo.InvariantCulture);
            var departmentCode = DepartmentCodeFor(batch.Key.Department);
            var isAcknowledged = batchOrdinal % 2 == 0;
            var form = new PreventiveMaintenanceForm
            {
                Id = StableId($"form:{batch.Key.AssetCategory}:{batch.Key.Department}:{batch.Key.PmCycle}"),
                FileNumber = $"PM-{year}-{category.Code}-{departmentCode.ToUpperInvariant()}-{month:D2}",
                AssetCategory = batch.Key.AssetCategory,
                Building = assetsById[batchSchedules[0].AssetId].Building,
                Department = batch.Key.Department,
                PmCycle = batch.Key.PmCycle,
                PeriodType = SchedulePeriodTypeCatalog.Quarter,
                Quarter = $"Q{(month - 1) / 3 + 1}",
                Year = year,
                AcademicYear = AcademicYearFor(year, month),
                Status = isAcknowledged
                    ? PreventiveMaintenanceFormStatusCatalog.Acknowledged
                    : PreventiveMaintenanceFormStatusCatalog.Submitted,
                CreatedByUserId = inspectorUserId,
                SubmittedByUserId = inspectorUserId,
                CreatedAt = At(year, month, 1, 9),
                UpdatedAt = At(year, month, 1, 9)
            };
            forms.Add(form);

            var batchInspections = new List<InspectionRecord>(batchSchedules.Length);
            var batchReferrals = new List<InspectionRecord>();
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
                    batchReferrals.Add(inspection);
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

            foreach (var inspection in batchReferrals)
            {
                var wmsNumber = wmsNumberByYear.GetValueOrDefault(year) + 1;
                wmsNumberByYear[year] = wmsNumber;
                var externalPmNumber = $"WMS-{year}-{wmsNumber:D4}";
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

    private static string AcademicYearFor(int year, int month) =>
        month <= 6 ? $"{year - 1}-{year}" : $"{year}-{year + 1}";

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

    private static string DepartmentCodeFor(string departmentName) =>
        Departments.Concat(AdditionalDepartments)
            .Single(department => department.Name == departmentName)
            .Code;

    private static bool IsOriginal2026Batch(BatchKey key) =>
        CycleYear(key.PmCycle) == 2026
        && int.Parse(key.PmCycle.AsSpan(5), CultureInfo.InvariantCulture) != 2
        && Departments.Any(department => department.Name == key.Department);

    private static int CycleYear(string pmCycle) =>
        int.Parse(pmCycle.AsSpan(0, 4), CultureInfo.InvariantCulture);

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
        string Name,
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
