using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Data.Seeding;

// Fictional, stable demo history as of October 2026. Historical benchmark JSON is unchanged.
internal static class CpmpDemoHistorySeeder
{
    private static readonly TimeSpan Manila = TimeSpan.FromHours(8);
    private const string Signature = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=";
    private static readonly string[] Categories =
        ["fire-extinguisher", "fire-alarm", "emergency-light", "water-drinking-station"];
    // Immutable hash inputs preserve all previously assigned record IDs.
    // These keys are never persisted as display labels.
    private static readonly string[] Departments = ["Demo CCMS", "Demo Library"];

    internal static async Task<DevelopmentDemoSeedResult> SeedAsync(
        ApplicationDbContext context, Guid inspectorId, CancellationToken cancellationToken,
        string supervisorEmail = "supervisor@unipm.local")
    {
        await ValidateExistingFixtureRowsAsync(context, cancellationToken);
        var supervisorId = await context.Users.Where(user => user.Email == supervisorEmail)
            .Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);
        // Separate campus groups avoid colliding with baseline CCMS and Library batches.
        var assetCount = 0;
        var scheduleCount = 0;
        var inspectionCount = 0;
        var formCount = 0;
        var acknowledgementCount = 0;

        foreach (var category in Categories)
        foreach (var department in Departments)
        {
            var prefix = PrefixFor(category);
            var assets = new List<Asset>();
            for (var index = 1; index <= 2; index++)
            {
                var code = AssetCodeFor(department, prefix, index);
                var id = AssetIdFor(department, prefix, index);
                var asset = await context.Assets.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
                if (asset is null)
                {
                    if (await context.Assets.AnyAsync(row => row.AssetCode == code, cancellationToken))
                        throw new InvalidOperationException("CPMP demo code conflicts with an unrelated asset.");
                    asset = new Asset
                    {
                        Id = id, AssetCode = code, AssetCategory = category, Department = DepartmentNameFor(department),
                        Building = department == "Demo CCMS" ? "CNAHS Building" : "University Gymnasium",
                        Location = LocationFor(category, index), Status = "Active",
                        QrCodeValue = AssetQrCodeValue.Create(category, id),
                        CreatedAt = At(2025, 1, 1), UpdatedAt = At(2025, 1, 1)
                    };
                    context.Assets.Add(asset);
                }
                else if (asset.AssetCode != code)
                    throw new InvalidOperationException("CPMP demo asset ID conflicts with an unrelated record.");
                assets.Add(asset);
                assetCount++;
            }

            foreach (var year in new[] { 2025, 2026 })
            foreach (var month in CpmpScheduleFrequency.GetMonths(category))
            {
                var cycle = $"{year:D4}-{month:D2}";
                var key = $"{department}:{category}:{cycle}";
                var future = year == 2026 && month > 10;
                var lastClosed = year == 2026 && month == (category is "fire-alarm" or "emergency-light" ? 6 : 8);
                var draft = lastClosed;
                var submitted = year == 2026 && month == 5;
                var status = draft ? PreventiveMaintenanceFormStatusCatalog.Draft
                    : submitted ? PreventiveMaintenanceFormStatusCatalog.Submitted
                    : PreventiveMaintenanceFormStatusCatalog.Acknowledged;
                PreventiveMaintenanceForm? form = null;
                if (!future)
                {
                    var formId = Id($"form:{key}");
                    form = await context.PreventiveMaintenanceForms.SingleOrDefaultAsync(row => row.Id == formId, cancellationToken);
                    if (form is null)
                    {
                        form = new PreventiveMaintenanceForm
                        {
                            Id = formId, AssetCategory = category, Department = DepartmentNameFor(department),
                            Building = assets[0].Building, PmCycle = cycle, PeriodType = "Quarter",
                            Quarter = $"Q{(month - 1) / 3 + 1}", Year = year,
                            AcademicYear = $"{year}-{year + 1}", Status = status,
                            FileNumber = draft ? null : FileNumberFor(year, department, prefix, month),
                            CreatedByUserId = inspectorId, CreatedAt = At(year, month, 1),
                            UpdatedAt = At(year, month, 20)
                        };
                        context.PreventiveMaintenanceForms.Add(form);
                    }
                    formCount++;
                }

                for (var index = 0; index < assets.Count; index++)
                {
                    var asset = assets[index];
                    var scheduleId = Id($"schedule:{key}:{asset.Id}");
                    var completed = !future && !(draft && index == 1);
                    // One late row in complete 2025 batches and May 2026 batches.
                    var late = completed && index == 1 && (year == 2025 || submitted);
                    var completion = late ? At(year, month, 1).AddMonths(1).AddDays(1) : At(year, month, 20);
                    var schedule = await context.PreventiveMaintenanceSchedules.SingleOrDefaultAsync(row => row.Id == scheduleId, cancellationToken);
                    if (schedule is null)
                    {
                        schedule = new PreventiveMaintenanceSchedule
                        {
                            Id = scheduleId, AssetId = asset.Id, PmCycle = cycle,
                            ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(cycle),
                            PeriodType = "Quarter", Quarter = $"Q{(month - 1) / 3 + 1}",
                            Year = year, AcademicYear = $"{year}-{year + 1}",
                            Status = completed ? ScheduleStatusCatalog.Completed : ScheduleStatusCatalog.Due,
                            CompletedAt = completed ? completion : null, AssignedToUserId = inspectorId,
                            AssignedSupervisorUserId = supervisorId,
                            CreatedAt = At(year, month, 1), UpdatedAt = completed ? completion : At(year, month, 1)
                        };
                        context.PreventiveMaintenanceSchedules.Add(schedule);
                    }
                    scheduleCount++;
                    if (!completed) continue;
                    var inspectionId = Id($"inspection:{scheduleId}");
                    if (!await context.InspectionRecords.AnyAsync(row => row.ScheduleId == scheduleId, cancellationToken))
                    {
                        context.InspectionRecords.Add(new InspectionRecord
                        {
                            Id = inspectionId, ScheduleId = scheduleId, AssetId = asset.Id,
                            PreventiveMaintenanceFormId = form!.Id, InspectorUserId = inspectorId,
                            StartedAt = completion.AddMinutes(-30), DateInspected = completion.AddMinutes(-30),
                            CompletedAt = completion, IsOperational = index == 0,
                            Remarks = RemarksFor(category, index == 0),
                            ActionsRecommendations = index == 0 ? null : RecommendationFor(category),
                            CreatedAt = completion.AddMinutes(-30), UpdatedAt = completion
                        });
                    }
                    inspectionCount++;
                }

                if (form is not null && form.Status != PreventiveMaintenanceFormStatusCatalog.Draft)
                {
                    var finished = year == 2025 || submitted
                        ? At(year, month, 1).AddMonths(1).AddDays(1)
                        : At(year, month, 20);
                    form.FieldWorkCompletedAt ??= finished;
                    form.SubmittedAt ??= finished.AddHours(1);
                    form.SubmittedByUserId ??= inspectorId;
                    form.UpdatedAt = form.SubmittedAt.Value;
                    if (form.Status == PreventiveMaintenanceFormStatusCatalog.Acknowledged)
                    {
                        if (!await context.PreventiveMaintenanceAcknowledgements.AnyAsync(row => row.FormId == form.Id, cancellationToken))
                        {
                            var acknowledgementTime = finished.AddDays(1);
                            context.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
                            {
                                Id = Id($"ack:{key}"), FormId = form.Id,
                                SignatoryName = department == "Demo CCMS" ? "Adrian M. Reyes" : "Marina L. Santos",
                                SignatoryPosition = "Department representative",
                                CapturedByUserId = inspectorId, AcknowledgedAt = acknowledgementTime,
                                SignatureData = Signature, SignatureContentType = "image/png",
                                SignatureChecksum = Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(Signature)))
                            });
                            form.UpdatedAt = acknowledgementTime;
                        }
                        acknowledgementCount++;
                    }
                }
            }
        }

        await RepairLegacyFixtureAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return new DevelopmentDemoSeedResult(assetCount, scheduleCount, inspectionCount, formCount, acknowledgementCount);
    }

    internal static CpmpDemoHistoryOwnedIds GetOwnedIds()
    {
        var assets = new Dictionary<Guid, string>();
        var assetQrCodes = new Dictionary<Guid, string>();
        var schedules = new Dictionary<Guid, (Guid AssetId, string PmCycle)>();
        var forms = new Dictionary<Guid, (string AssetCategory, string Department, string PmCycle)>();
        var formFileNumbers = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var formFileNumbersById = new Dictionary<Guid, string?>();
        var inspections = new Dictionary<Guid, (Guid ScheduleId, Guid AssetId, Guid FormId)>();
        var acknowledgements = new Dictionary<Guid, Guid>();

        foreach (var category in Categories)
        foreach (var department in Departments)
        {
            var prefix = PrefixFor(category);
            var assetIds = new Guid[2];
            for (var index = 1; index <= assetIds.Length; index++)
            {
                var code = AssetCodeFor(department, prefix, index);
                var assetId = AssetIdFor(department, prefix, index);
                assetIds[index - 1] = assetId;
                assets.Add(assetId, code);
                assetQrCodes.Add(assetId, AssetQrCodeValue.Create(category, assetId));
            }

            foreach (var year in new[] { 2025, 2026 })
            foreach (var month in CpmpScheduleFrequency.GetMonths(category))
            {
                var cycle = $"{year:D4}-{month:D2}";
                var key = $"{department}:{category}:{cycle}";
                var future = year == 2026 && month > 10;
                var lastClosed = year == 2026 && month == (category is "fire-alarm" or "emergency-light" ? 6 : 8);
                var draft = lastClosed;
                var submitted = year == 2026 && month == 5;
                var formId = future ? (Guid?)null : Id($"form:{key}");
                if (formId is not null)
                {
                    forms.Add(formId.Value, (category, DepartmentNameFor(department), cycle));
                    formFileNumbersById.Add(
                        formId.Value,
                        draft ? null : FileNumberFor(year, department, prefix, month));
                    if (!draft)
                    {
                        formFileNumbers.Add(
                            FileNumberFor(year, department, prefix, month),
                            formId.Value);
                    }
                }

                for (var index = 0; index < assetIds.Length; index++)
                {
                    var assetId = assetIds[index];
                    var scheduleId = Id($"schedule:{key}:{assetId}");
                    schedules.Add(scheduleId, (assetId, cycle));
                    var completed = !future && !(draft && index == 1);
                    if (completed)
                    {
                        inspections.Add(
                            Id($"inspection:{scheduleId}"),
                            (scheduleId, assetId, formId!.Value));
                    }
                }

                if (!future && !draft && !submitted)
                {
                    acknowledgements.Add(Id($"ack:{key}"), formId!.Value);
                }
            }
        }

        return new CpmpDemoHistoryOwnedIds(
            assets,
            assetQrCodes,
            schedules,
            forms,
            formFileNumbers,
            formFileNumbersById,
            inspections,
            acknowledgements);
    }

    internal static async Task ValidateExistingFixtureRowsAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var owned = GetOwnedIds();
        var assetIds = owned.Assets.Keys.ToHashSet();
        var assetCodes = owned.Assets.Values.ToHashSet(StringComparer.Ordinal);
        var qrValues = owned.AssetQrCodes.Values.ToHashSet(StringComparer.Ordinal);
        var assets = await context.Assets
            .Where(row => assetIds.Contains(row.Id)
                || assetCodes.Contains(row.AssetCode)
                || row.QrCodeValue != null && qrValues.Contains(row.QrCodeValue))
            .ToListAsync(cancellationToken);
        if (assets.Any(row => !owned.Assets.TryGetValue(row.Id, out var code)
                || !StringComparer.Ordinal.Equals(row.AssetCode, code)))
        {
            throw new InvalidOperationException("A CPMP demo asset ID, code, or QR value conflicts with an unrelated asset.");
        }

        var scheduleIds = owned.Schedules.Keys.ToHashSet();
        var schedules = await context.PreventiveMaintenanceSchedules
            .Where(row => scheduleIds.Contains(row.Id) || assetIds.Contains(row.AssetId))
            .ToListAsync(cancellationToken);
        if (schedules.Any(row => !owned.Schedules.TryGetValue(row.Id, out var key)
                || row.AssetId != key.AssetId
                || row.PmCycle != key.PmCycle))
        {
            throw new InvalidOperationException("A CPMP demo asset has an unrelated schedule or a schedule ID conflicts with an unrelated record.");
        }

        var formIds = owned.Forms.Keys.ToHashSet();
        var fileNumbers = owned.FormFileNumbers.Keys.ToHashSet(StringComparer.Ordinal);
        var forms = await context.PreventiveMaintenanceForms
            .Where(row => formIds.Contains(row.Id)
                || row.FileNumber != null && fileNumbers.Contains(row.FileNumber))
            .ToListAsync(cancellationToken);
        if (forms.Any(row => !owned.Forms.TryGetValue(row.Id, out var key)
                || row.AssetCategory != key.AssetCategory
                || row.Department != key.Department
                || row.PmCycle != key.PmCycle
                || !owned.FormFileNumbersById.TryGetValue(row.Id, out var fileNumber)
                || row.FileNumber != fileNumber))
        {
            throw new InvalidOperationException("A CPMP demo form ID or file number conflicts with an unrelated form.");
        }

        var inspectionIds = owned.Inspections.Keys.ToHashSet();
        var inspections = await context.InspectionRecords
            .Where(row => inspectionIds.Contains(row.Id)
                || assetIds.Contains(row.AssetId)
                || scheduleIds.Contains(row.ScheduleId)
                || row.PreventiveMaintenanceFormId.HasValue
                    && formIds.Contains(row.PreventiveMaintenanceFormId.Value))
            .ToListAsync(cancellationToken);
        if (inspections.Any(row => !owned.Inspections.TryGetValue(row.Id, out var key)
                || row.ScheduleId != key.ScheduleId
                || row.AssetId != key.AssetId
                || row.PreventiveMaintenanceFormId != key.FormId))
        {
            throw new InvalidOperationException("A CPMP demo fixture has an unrelated inspection or an inspection ID conflicts with an unrelated record.");
        }

        var acknowledgementIds = owned.Acknowledgements.Keys.ToHashSet();
        var acknowledgements = await context.PreventiveMaintenanceAcknowledgements
            .Where(row => acknowledgementIds.Contains(row.Id) || formIds.Contains(row.FormId))
            .ToListAsync(cancellationToken);
        if (acknowledgements.Any(row => !owned.Acknowledgements.TryGetValue(row.Id, out var formId)
                || row.FormId != formId))
        {
            throw new InvalidOperationException("A CPMP demo acknowledgement ID or form conflicts with an unrelated record.");
        }
    }

    private static async Task RepairLegacyFixtureAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        // Limit repair to IDs in the immutable fictional fixture, not real/user-created rows.
        var dataset = await new SyntheticMaintenanceDatasetLoader(new SyntheticMaintenanceSeedOptions(),
            new SyntheticMaintenanceDatasetValidator()).LoadAsync(cancellationToken);
        var fixtureAssets = dataset.Assets.ToDictionary(row => row.Id);
        var fixtureSchedules = dataset.Schedules.ToDictionary(row => row.Id);
        var ids = fixtureSchedules.Keys.ToArray();
        var schedules = await context.PreventiveMaintenanceSchedules.Where(row => ids.Contains(row.Id))
            .ToListAsync(cancellationToken);
        var inspections = await context.InspectionRecords.Where(row => ids.Contains(row.ScheduleId))
            .ToListAsync(cancellationToken);
        var fixtureInspectionIds = dataset.Inspections.Select(row => row.Id).ToHashSet();
        foreach (var schedule in schedules)
        {
            var source = fixtureSchedules[schedule.Id];
            if (schedule.AssetId != source.AssetId) throw new InvalidOperationException("Legacy fixture ID conflict.");
            var months = CpmpScheduleFrequency.GetMonths(fixtureAssets[source.AssetId].AssetCategory);
            var month = source.ScheduleDate.Month;
            var year = source.ScheduleDate.Year;
            if (!months.Contains(month))
            {
                month = months.LastOrDefault(value => value < month);
                if (month == 0) { month = months[^1]; year--; }
            }
            schedule.PmCycle = $"{year:D4}-{month:D2}";
            schedule.ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(schedule.PmCycle);
            schedule.Year = year;
            schedule.Quarter = $"Q{(month - 1) / 3 + 1}";
            foreach (var inspection in inspections.Where(row => row.ScheduleId == schedule.Id && fixtureInspectionIds.Contains(row.Id)))
            {
                inspection.CompletedAt ??= source.CompletedAt;
                inspection.StartedAt ??= inspection.DateInspected;
                if (inspection.CompletedAt is not null)
                {
                    schedule.Status = ScheduleStatusCatalog.Completed;
                    schedule.CompletedAt = inspection.CompletedAt;
                }
            }
        }
    }

    private static Guid Id(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"unipm-cpmp-demo-v2:{key}"))[..16];
        // Guid uses little-endian bytes for the version field. Emit a valid
        // RFC UUID so client contract validation accepts these stable IDs.
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static string PrefixFor(string category) => category switch
    {
        "fire-extinguisher" => "FE",
        "fire-alarm" => "FA",
        "emergency-light" => "EL",
        "water-drinking-station" => "WDS",
        _ => throw new InvalidOperationException($"Unsupported CPMP demo category '{category}'.")
    };

    private static Guid AssetIdFor(string department, string prefix, int index) =>
        Id($"CPMP-DEMO-{department.ToUpperInvariant().Replace(' ', '-')}-{prefix}-{index:00}");

    private static string DepartmentNameFor(string department) =>
        department == "Demo CCMS" ? "CNAHS" : "General Services Department";

    private static string DepartmentCodeFor(string department) => department == "Demo CCMS" ? "CNAHS" : "GYM";

    private static string AssetCodeFor(string department, string prefix, int index) =>
        $"{prefix}-{DepartmentCodeFor(department)}-{100 + index:000}";

    private static string FileNumberFor(int year, string department, string prefix, int month) =>
        $"PM-{year}-{DepartmentCodeFor(department)}-{prefix}-{month:00}";

    // Room layouts and equipment placements are synthetic; no floor numbers are asserted.
    private static string LocationFor(string category, int index) =>
        (category, index) switch
        {
            ("fire-extinguisher", 1) => "Common area entrance",
            ("fire-extinguisher", _) => "Common corridor",
            ("fire-alarm", 1) => "Common area",
            ("fire-alarm", _) => "Entrance approach",
            ("emergency-light", 1) => "Exit approach",
            ("emergency-light", _) => "Common corridor",
            (_, 1) => "Common area",
            (_, _) => "Entrance approach"
        };

    private static string RemarksFor(string category, bool operational) => (category, operational) switch
    {
        ("fire-extinguisher", true) => "Pressure gauge within operating range; pin, seal and hose intact.",
        ("fire-extinguisher", false) => "Pressure gauge below operating range; unit requires servicing.",
        ("fire-alarm", true) => "Alarm activation test completed; audible signal and indicator responded.",
        ("fire-alarm", false) => "Audible alarm failed to activate during the functional test.",
        ("emergency-light", true) => "Lamp remained illuminated during the power interruption test.",
        ("emergency-light", false) => "Lamp failed to remain illuminated during the battery backup test.",
        (_, true) => "Water dispensing and cooling functions responded; no visible leaks observed.",
        (_, false) => "Water dispenser failed to cool; leakage observed at the outlet connection."
    };

    private static string RecommendationFor(string category) => category switch
    {
        "fire-extinguisher" => "Refer for cylinder servicing and pressure verification before returning to use.",
        "fire-alarm" => "Refer for circuit and sounder inspection, then repeat the activation test.",
        "emergency-light" => "Refer for battery and charging-circuit inspection, then repeat the backup test.",
        _ => "Refer for cooling-system and outlet repair, then verify dispensing and leak-free operation."
    };

    private static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 10, 0, 0, Manila);
}

internal sealed record CpmpDemoHistoryOwnedIds(
    IReadOnlyDictionary<Guid, string> Assets,
    IReadOnlyDictionary<Guid, string> AssetQrCodes,
    IReadOnlyDictionary<Guid, (Guid AssetId, string PmCycle)> Schedules,
    IReadOnlyDictionary<Guid, (string AssetCategory, string Department, string PmCycle)> Forms,
    IReadOnlyDictionary<string, Guid> FormFileNumbers,
    IReadOnlyDictionary<Guid, string?> FormFileNumbersById,
    IReadOnlyDictionary<Guid, (Guid ScheduleId, Guid AssetId, Guid FormId)> Inspections,
    IReadOnlyDictionary<Guid, Guid> Acknowledgements);
