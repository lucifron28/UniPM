using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Data.Seeding;

internal sealed class DevelopmentDemoSeeder(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IHostEnvironment environment,
    IConfiguration? configuration = null)
{
    private static readonly TimeSpan ManilaOffset = TimeSpan.FromHours(8);
    private const string SignatureBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=";

    internal async Task<DevelopmentDemoSeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var (inspectorId, gsdId) = await ResolveUsersAsync(context, cancellationToken);
        var staging = StagingDemoInitialization.Enabled(configuration);
        if (staging) await StagingDemoInitialization.EnsureReadyAsync(context, true, cancellationToken);
        await ValidateConflictsAsync(context, cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);

        if (!staging) await RemoveDemoClosureAsync(context, inspectorId, cancellationToken);

        var timestamp = AtManila(2026, 6, 1, 8);
        context.Assets.AddRange(DevelopmentDemoCatalog.Assets.Select(asset => new Asset
        {
            Id = asset.Id,
            AssetCode = asset.AssetCode,
            AssetCategory = asset.AssetCategory,
            Building = asset.Building,
            Department = asset.Department,
            Location = asset.Location,
            QrCodeValue = asset.QrCodeValue,
            Status = "Active",
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        }));

        var schedules = BuildSchedules(inspectorId, timestamp);
        context.PreventiveMaintenanceSchedules.AddRange(schedules);

        var submittedForm = BuildSubmittedForm(inspectorId);
        var acknowledgedForm = BuildAcknowledgedForm(inspectorId);
        context.PreventiveMaintenanceForms.AddRange(submittedForm, acknowledgedForm);

        var inspections = BuildInspections(inspectorId, submittedForm.Id, acknowledgedForm.Id);
        context.InspectionRecords.AddRange(inspections);

        var signatureBytes = Convert.FromBase64String(SignatureBase64);
        context.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
        {
            Id = DevelopmentDemoCatalog.AcknowledgementId,
            FormId = acknowledgedForm.Id,
            SignatoryName = "Dr. Elena Mercado",
            SignatoryPosition = "Department representative",
            SignatureData = SignatureBase64,
            SignatureContentType = "image/png",
            SignatureChecksum = Convert.ToHexString(SHA256.HashData(signatureBytes)),
            CapturedByUserId = inspectorId,
            AcknowledgedAt = AtManila(2026, 7, 26, 9)
        });

        await context.SaveChangesAsync(cancellationToken);
        var history = await CpmpDemoHistorySeeder.SeedAsync(context, inspectorId, cancellationToken,
            staging ? StagingDemoInitialization.Email(AuthRoleCatalog.Supervisor) : "supervisor@unipm.local");

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new DevelopmentDemoSeedResult(
            DevelopmentDemoCatalog.Assets.Count + history.Assets,
            schedules.Count + history.Schedules,
            inspections.Count + history.Inspections,
            2 + history.Forms,
            1 + history.Acknowledgements);
    }

    internal async Task<DevelopmentDemoResetResult> ResetAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();
        if (StagingDemoInitialization.Enabled(configuration))
            throw new InvalidOperationException("Reset is unavailable in staging initialization mode.");
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var inspectorId = await ResolveDemoInspectorIdAsync(context, cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);
        var result = await RemoveDemoClosureAsync(context, inspectorId, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private static List<PreventiveMaintenanceSchedule> BuildSchedules(
        Guid inspectorId,
        DateTimeOffset timestamp)
    {
        var schedules = new List<PreventiveMaintenanceSchedule>();
        for (var index = 0; index < DevelopmentDemoCatalog.Assets.Count; index++)
        {
            var asset = DevelopmentDemoCatalog.Assets[index];
            var scheduleId = DevelopmentDemoCatalog.ScheduleIds[index];
            var pmCycle = asset.Scenario switch
            {
                "scenario-a" => "2026-11",
                "scenario-b" => "2026-08",
                "scenario-c" => "2026-06",
                _ => throw new InvalidOperationException($"Unsupported demo scenario '{asset.Scenario}'.")
            };
            if (!PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out var month)
                || !CpmpScheduleFrequency.IsValid(asset.AssetCategory, month))
            {
                throw new InvalidOperationException(
                    $"Demo PM cycle '{pmCycle}' is invalid for asset category '{asset.AssetCategory}'.");
            }

            var scheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle);
            DateTimeOffset? completedAt = asset.Scenario switch
            {
                "scenario-b" when index == 3 => AtManila(2026, 8, 20, 10),
                "scenario-b" when index == 4 => AtManila(2026, 8, 27, 11),
                "scenario-b" => AtManila(2026, 9, 2, 9),
                "scenario-c" when index == 6 => AtManila(2026, 6, 20, 10),
                "scenario-c" when index == 7 => AtManila(2026, 7, 3, 10),
                _ => null
            };

            schedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = scheduleId,
                AssetId = asset.Id,
                ScheduleDate = scheduleDate,
                PmCycle = pmCycle,
                PeriodType = "Quarter",
                Quarter = $"Q{(month - 1) / 3 + 1}",
                Year = year,
                AcademicYear = "2026-2027",
                Status = completedAt is null ? ScheduleStatusCatalog.Due : ScheduleStatusCatalog.Completed,
                AssignedToUserId = inspectorId,
                CompletedAt = completedAt,
                CreatedAt = timestamp,
                UpdatedAt = completedAt ?? timestamp
            });
        }

        return schedules;
    }

    private static PreventiveMaintenanceForm BuildSubmittedForm(Guid inspectorId)
    {
        return new PreventiveMaintenanceForm
        {
            Id = DevelopmentDemoCatalog.FormIds[0],
            FileNumber = "PM-2026-LIB-FE-08",
            AssetCategory = "fire-extinguisher",
            Building = "University Library Complex",
            Department = "Library",
            PmCycle = "2026-08",
            PeriodType = "Quarter",
            Quarter = "Q3",
            Year = 2026,
            AcademicYear = "2026-2027",
            Status = PreventiveMaintenanceFormStatusCatalog.Submitted,
            CreatedByUserId = inspectorId,
            SubmittedByUserId = inspectorId,
            FieldWorkCompletedAt = AtManila(2026, 9, 2, 9),
            SubmittedAt = AtManila(2026, 9, 2, 10),
            CreatedAt = AtManila(2026, 8, 20, 8),
            UpdatedAt = AtManila(2026, 9, 2, 10)
        };
    }

    private static PreventiveMaintenanceForm BuildAcknowledgedForm(Guid inspectorId)
    {
        return new PreventiveMaintenanceForm
        {
            Id = DevelopmentDemoCatalog.FormIds[1],
            FileNumber = "PM-2026-OSAS-EL-06",
            AssetCategory = "emergency-light",
            Building = "Administration Building",
            Department = "Office of Student Affairs and Services",
            PmCycle = "2026-06",
            PeriodType = "Quarter",
            Quarter = "Q2",
            Year = 2026,
            AcademicYear = "2026-2027",
            Status = PreventiveMaintenanceFormStatusCatalog.Acknowledged,
            CreatedByUserId = inspectorId,
            SubmittedByUserId = inspectorId,
            FieldWorkCompletedAt = AtManila(2026, 7, 3, 10),
            SubmittedAt = AtManila(2026, 7, 3, 11),
            CreatedAt = AtManila(2026, 6, 20, 8),
            UpdatedAt = AtManila(2026, 7, 26, 9)
        };
    }

    private static List<InspectionRecord> BuildInspections(
        Guid inspectorId,
        Guid submittedFormId,
        Guid acknowledgedFormId)
    {
        var completedAt = new[]
        {
            AtManila(2026, 8, 20, 10),
            AtManila(2026, 8, 27, 11),
            AtManila(2026, 9, 2, 9),
            AtManila(2026, 6, 20, 10),
            AtManila(2026, 7, 3, 10)
        };
        var remarks = new[]
        {
            "Pressure gauge below the green operating range.",
            "Safety pin seal is worn and should be replaced.",
            "Mounting bracket is loose in the library corridor.",
            "Battery backup remained illuminated during the functional test.",
            "Reduced battery duration observed during the functional test."
        };
        var recommendations = new[]
        {
            "Recharge the cylinder and verify pressure before returning it to service.",
            "Replace the tamper seal during corrective servicing.",
            "Secure or replace the mounting bracket.",
            null,
            "Schedule battery replacement and repeat the duration test."
        };
        var operational = new[] { false, true, false, true, false };
        var assetIndexes = new[] { 3, 4, 5, 6, 7 };
        var inspections = new List<InspectionRecord>();

        for (var index = 0; index < assetIndexes.Length; index++)
        {
            var assetIndex = assetIndexes[index];
            inspections.Add(new InspectionRecord
            {
                Id = DevelopmentDemoCatalog.InspectionIds[index],
                ScheduleId = DevelopmentDemoCatalog.ScheduleIds[assetIndex],
                PreventiveMaintenanceFormId = index < 3 ? submittedFormId : acknowledgedFormId,
                AssetId = DevelopmentDemoCatalog.Assets[assetIndex].Id,
                InspectorUserId = inspectorId,
                DateInspected = completedAt[index],
                StartedAt = completedAt[index].AddMinutes(-25),
                CompletedAt = completedAt[index],
                IsOperational = operational[index],
                Remarks = remarks[index],
                ActionsRecommendations = recommendations[index],
                CreatedAt = completedAt[index],
                UpdatedAt = completedAt[index]
            });
        }

        return inspections;
    }

    private async Task<(Guid InspectorId, Guid GsdId)> ResolveUsersAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var staging = StagingDemoInitialization.Enabled(configuration);
        var inspectorEmail = staging ? StagingDemoInitialization.Email(AuthRoleCatalog.Inspector) : DevelopmentDemoCatalog.InspectorEmail;
        var gsdEmail = staging ? StagingDemoInitialization.Email(AuthRoleCatalog.Gsd) : DevelopmentDemoCatalog.GsdEmail;
        var emails = new[] { inspectorEmail, gsdEmail };
        var matchingUsers = await context.Users
            .AsNoTracking()
            .Where(user => user.Email != null && emails.Contains(user.Email))
            .Select(user => new { user.Email, user.Id })
            .ToListAsync(cancellationToken);
        var users = matchingUsers.ToDictionary(
            user => user.Email!,
            user => user.Id,
            StringComparer.OrdinalIgnoreCase);

        if (!users.TryGetValue(inspectorEmail, out var inspectorId)
            || !users.TryGetValue(gsdEmail, out var gsdId))
        {
            throw new InvalidOperationException(
                "Demo seeding requires the configured Inspector and GSD accounts. Run the approved account initialization first.");
        }

        return (inspectorId, gsdId);
    }

    private static async Task<Guid> ResolveDemoInspectorIdAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var inspectorId = await context.Users
            .AsNoTracking()
            .Where(user => user.Email == DevelopmentDemoCatalog.InspectorEmail)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return inspectorId ?? throw new InvalidOperationException(
            "Development demo reset requires inspector@unipm.local. Run --seed-development-users first.");
    }

    private static async Task ValidateConflictsAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var assetIds = DevelopmentDemoCatalog.Assets.Select(asset => asset.Id).ToHashSet();
        var assetCodes = DevelopmentDemoCatalog.Assets.Select(asset => asset.AssetCode).ToHashSet(StringComparer.Ordinal);
        var qrValues = DevelopmentDemoCatalog.Assets.Select(asset => asset.QrCodeValue).ToHashSet(StringComparer.Ordinal);
        var conflicts = await context.Assets
            .AsNoTracking()
            .Where(asset => assetCodes.Contains(asset.AssetCode)
                || asset.QrCodeValue != null && qrValues.Contains(asset.QrCodeValue))
            .ToListAsync(cancellationToken);
        if (conflicts.Any(asset => !assetIds.Contains(asset.Id)))
        {
            throw new InvalidOperationException("A demo asset code or QR payload belongs to an unrelated asset.");
        }

        var formIds = DevelopmentDemoCatalog.FormIds.ToHashSet();
        var fileNumbers = new[]
        {
            "PM-2026-LIB-FE-08",
            "PM-2026-OSAS-EL-06",
            "PM-2026-OSAS-EL-07"
        };
        var formConflicts = await context.PreventiveMaintenanceForms
            .AsNoTracking()
            .Where(form => form.FileNumber != null && fileNumbers.Contains(form.FileNumber))
            .ToListAsync(cancellationToken);
        if (formConflicts.Any(form => !formIds.Contains(form.Id)))
        {
            throw new InvalidOperationException("A demo file number belongs to an unrelated form.");
        }
    }

    private static async Task<DevelopmentDemoResetResult> RemoveDemoClosureAsync(
        ApplicationDbContext context,
        Guid inspectorId,
        CancellationToken cancellationToken)
    {
        var cpmp = CpmpDemoHistorySeeder.GetOwnedIds();
        var assetCodes = DevelopmentDemoCatalog.Assets.ToDictionary(asset => asset.Id, asset => asset.AssetCode);
        foreach (var asset in cpmp.Assets)
        {
            assetCodes.Add(asset.Key, asset.Value);
        }
        var assetQrCodes = DevelopmentDemoCatalog.Assets.ToDictionary(asset => asset.Id, asset => asset.QrCodeValue);
        foreach (var asset in cpmp.AssetQrCodes)
        {
            assetQrCodes.Add(asset.Key, asset.Value);
        }
        var assetIds = assetCodes.Keys.ToHashSet();
        var assetCodeValues = assetCodes.Values.ToHashSet(StringComparer.Ordinal);
        var qrValues = assetQrCodes.Values.ToHashSet(StringComparer.Ordinal);

        var scheduleIdentities = BuildSchedules(inspectorId, AtManila(2026, 6, 1, 8))
            .ToDictionary(schedule => schedule.Id, schedule => (schedule.AssetId, schedule.PmCycle));
        foreach (var schedule in cpmp.Schedules)
        {
            scheduleIdentities.Add(schedule.Key, (schedule.Value.AssetId, schedule.Value.PmCycle));
        }
        var scheduleIds = scheduleIdentities.Keys.ToHashSet();

        var formIdentities = new Dictionary<Guid, DemoFormIdentity>
        {
            [DevelopmentDemoCatalog.FormIds[0]] = new(
                "fire-extinguisher", "University Library Complex", "Library", "2026-08", "PM-2026-LIB-FE-08"),
            [DevelopmentDemoCatalog.FormIds[1]] = new(
                "emergency-light", "Administration Building", "Office of Student Affairs and Services", "2026-06", "PM-2026-OSAS-EL-06")
        };
        var formFileNumbers = formIdentities.Values
            .Where(form => form.FileNumber is not null)
            .ToDictionary(form => form.FileNumber!, form => formIdentities.Single(pair => pair.Value == form).Key,
                StringComparer.Ordinal);
        foreach (var form in cpmp.Forms)
        {
            var building = form.Value.Department == "CNAHS" ? "CNAHS Building" : "University Gymnasium";
            var fileNumber = cpmp.FormFileNumbersById[form.Key];
            formIdentities.Add(form.Key, new(
                form.Value.AssetCategory,
                building,
                form.Value.Department,
                form.Value.PmCycle,
                fileNumber));
            if (fileNumber is not null)
            {
                formFileNumbers.Add(fileNumber, form.Key);
            }
        }
        var abandonedScenarioAFormIds = (await context.PreventiveMaintenanceForms
                .Where(form => form.CreatedByUserId == inspectorId
                    && form.Status == PreventiveMaintenanceFormStatusCatalog.Draft
                    && form.FileNumber == null
                    && form.AssetCategory == "fire-extinguisher"
                    && form.Building == null
                    && form.Department == "CCMS"
                    && form.PmCycle == null
                    && form.PeriodType == "Quarter"
                    && form.Quarter == "Q3"
                    && form.Semester == null
                    && form.Year == 2026
                    && form.AcademicYear == "2026-2027"
                    && form.SubmittedByUserId == null
                    && form.SubmittedAt == null
                    && form.FieldWorkCompletedAt == null
                    && form.Acknowledgement == null
                    && !form.Inspections.Any())
                .Select(form => form.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var formIds = formIdentities.Keys.Concat(abandonedScenarioAFormIds).ToHashSet();

        var inspectionIdentities = BuildInspections(
                inspectorId,
                DevelopmentDemoCatalog.FormIds[0],
                DevelopmentDemoCatalog.FormIds[1])
            .ToDictionary(
                inspection => inspection.Id,
                inspection => (inspection.ScheduleId, inspection.AssetId, inspection.PreventiveMaintenanceFormId));
        foreach (var inspection in cpmp.Inspections)
        {
            inspectionIdentities.Add(inspection.Key, (
                inspection.Value.ScheduleId,
                inspection.Value.AssetId,
                inspection.Value.FormId));
        }
        var inspectionIds = inspectionIdentities.Keys.ToHashSet();

        var acknowledgementForms = new Dictionary<Guid, Guid>
        {
            [DevelopmentDemoCatalog.AcknowledgementId] = DevelopmentDemoCatalog.FormIds[1]
        };
        foreach (var acknowledgement in cpmp.Acknowledgements)
        {
            acknowledgementForms.Add(acknowledgement.Key, acknowledgement.Value);
        }
        var acknowledgementIds = acknowledgementForms.Keys.ToHashSet();

        await ValidateOwnedDemoRowsAsync(
            context,
            assetIds,
            assetCodes,
            assetCodeValues,
            assetQrCodes,
            qrValues,
            scheduleIds,
            scheduleIdentities,
            formIds,
            abandonedScenarioAFormIds,
            inspectorId,
            formIdentities,
            formFileNumbers,
            inspectionIds,
            inspectionIdentities,
            acknowledgementIds,
            acknowledgementForms,
            cancellationToken);

        var acknowledgements = await context.PreventiveMaintenanceAcknowledgements
            .Where(row => acknowledgementIds.Contains(row.Id) || formIds.Contains(row.FormId))
            .ToListAsync(cancellationToken);
        var ownedInspections = await context.InspectionRecords
            .Where(row => inspectionIds.Contains(row.Id))
            .ToListAsync(cancellationToken);
        var forms = await context.PreventiveMaintenanceForms
            .Where(row => formIds.Contains(row.Id))
            .ToListAsync(cancellationToken);
        var schedules = await context.PreventiveMaintenanceSchedules
            .Where(row => scheduleIds.Contains(row.Id))
            .ToListAsync(cancellationToken);
        var assets = await context.Assets
            .Where(row => assetIds.Contains(row.Id))
            .ToListAsync(cancellationToken);

        context.PreventiveMaintenanceAcknowledgements.RemoveRange(acknowledgements);
        context.InspectionRecords.RemoveRange(ownedInspections);
        context.PreventiveMaintenanceForms.RemoveRange(forms);
        context.PreventiveMaintenanceSchedules.RemoveRange(schedules);
        context.Assets.RemoveRange(assets);
        await context.SaveChangesAsync(cancellationToken);

        return new DevelopmentDemoResetResult(
            assets.Count,
            schedules.Count,
            ownedInspections.Count,
            forms.Count,
            acknowledgements.Count);
    }

    private static async Task ValidateOwnedDemoRowsAsync(
        ApplicationDbContext context,
        HashSet<Guid> assetIds,
        IReadOnlyDictionary<Guid, string> assetCodes,
        HashSet<string> assetCodeValues,
        IReadOnlyDictionary<Guid, string> assetQrCodes,
        HashSet<string> qrValues,
        HashSet<Guid> scheduleIds,
        IReadOnlyDictionary<Guid, (Guid AssetId, string PmCycle)> scheduleIdentities,
        HashSet<Guid> formIds,
        HashSet<Guid> abandonedScenarioAFormIds,
        Guid inspectorId,
        IReadOnlyDictionary<Guid, DemoFormIdentity> formIdentities,
        IReadOnlyDictionary<string, Guid> formFileNumbers,
        HashSet<Guid> inspectionIds,
        IReadOnlyDictionary<Guid, (Guid ScheduleId, Guid AssetId, Guid? FormId)> inspectionIdentities,
        HashSet<Guid> acknowledgementIds,
        IReadOnlyDictionary<Guid, Guid> acknowledgementForms,
        CancellationToken cancellationToken)
    {
        var formFileNumberValues = formFileNumbers.Keys.ToArray();
        var assets = await context.Assets
            .Where(row => assetIds.Contains(row.Id)
                || assetCodeValues.Contains(row.AssetCode)
                || row.QrCodeValue != null && qrValues.Contains(row.QrCodeValue))
            .ToListAsync(cancellationToken);
        if (assets.Any(row => !assetCodes.TryGetValue(row.Id, out var code)
                || !StringComparer.Ordinal.Equals(row.AssetCode, code)
                || !assetQrCodes.TryGetValue(row.Id, out var qrValue)
                || !StringComparer.Ordinal.Equals(row.QrCodeValue, qrValue)))
        {
            throw new InvalidOperationException("Demo reset refused because a deterministic asset ID, code, or QR value conflicts with an unrelated asset.");
        }

        var schedules = await context.PreventiveMaintenanceSchedules
            .Where(row => scheduleIds.Contains(row.Id) || assetIds.Contains(row.AssetId))
            .ToListAsync(cancellationToken);
        if (schedules.Any(row => !scheduleIdentities.TryGetValue(row.Id, out var identity)
                || row.AssetId != identity.AssetId
                || row.PmCycle != identity.PmCycle))
        {
            throw new InvalidOperationException("Demo reset refused because a fixture asset has an unrelated schedule or a deterministic schedule ID conflict.");
        }

        var forms = await context.PreventiveMaintenanceForms
            .Where(row => formIds.Contains(row.Id)
                || row.FileNumber != null && formFileNumberValues.Contains(row.FileNumber))
            .ToListAsync(cancellationToken);
        if (forms.Any(row => abandonedScenarioAFormIds.Contains(row.Id)
                ? !IsAbandonedScenarioADraft(row, inspectorId)
                : !formIdentities.TryGetValue(row.Id, out var identity)
                    || row.AssetCategory != identity.AssetCategory
                    || row.Building != identity.Building
                    || row.Department != identity.Department
                    || row.PmCycle != identity.PmCycle
                    || row.FileNumber != identity.FileNumber))
        {
            throw new InvalidOperationException("Demo reset refused because a deterministic form ID or file number conflicts with an unrelated form.");
        }

        var inspections = await context.InspectionRecords
            .Where(row => inspectionIds.Contains(row.Id)
                || assetIds.Contains(row.AssetId)
                || scheduleIds.Contains(row.ScheduleId)
                || row.PreventiveMaintenanceFormId.HasValue
                    && formIds.Contains(row.PreventiveMaintenanceFormId.Value))
            .ToListAsync(cancellationToken);
        if (inspections.Any(row => !inspectionIdentities.TryGetValue(row.Id, out var identity)
                || row.ScheduleId != identity.ScheduleId
                || row.AssetId != identity.AssetId
                || row.PreventiveMaintenanceFormId != identity.FormId))
        {
            throw new InvalidOperationException("Demo reset refused because fixture assets, schedules, or forms have an unrelated inspection dependency or a deterministic inspection ID conflict.");
        }

        var acknowledgements = await context.PreventiveMaintenanceAcknowledgements
            .Where(row => acknowledgementIds.Contains(row.Id) || formIds.Contains(row.FormId))
            .ToListAsync(cancellationToken);
        if (acknowledgements.Any(row => !acknowledgementForms.TryGetValue(row.Id, out var formId)
                || row.FormId != formId))
        {
            throw new InvalidOperationException("Demo reset refused because a deterministic acknowledgement ID or form conflicts with an unrelated record.");
        }

        var hasPhotoEvidence = await context.InspectionPhotoEvidence
            .AnyAsync(row => inspectionIds.Contains(row.InspectionId), cancellationToken);
        var hasWmsReferral = await context.InspectionWmsReferrals
            .AnyAsync(row => inspectionIds.Contains(row.InspectionId), cancellationToken);
        var hasWmsAudit = await context.InspectionWmsReferralAudits
            .AnyAsync(row => inspectionIds.Contains(row.InspectionId), cancellationToken);
        var hasLocationAttempt = await context.InspectionLocationAttempts
            .AnyAsync(row => assetIds.Contains(row.AssetId) || scheduleIds.Contains(row.ScheduleId), cancellationToken);
        var hasLinkedLocationAttempt = await context.InspectionRecords
            .AnyAsync(row => inspectionIds.Contains(row.Id) && row.LocationAttemptId.HasValue, cancellationToken);
        var hasEnrollmentDeferral = await context.ScheduleEnrollmentDeferrals
            .AnyAsync(row => assetIds.Contains(row.AssetId), cancellationToken);
        if (hasPhotoEvidence || hasWmsReferral || hasWmsAudit || hasLocationAttempt
            || hasLinkedLocationAttempt || hasEnrollmentDeferral)
        {
            throw new InvalidOperationException(
                "Demo reset refused because fixture records have photo, WMS, location, or enrollment dependencies.");
        }
    }

    private static bool IsAbandonedScenarioADraft(PreventiveMaintenanceForm form, Guid inspectorId) =>
        form.CreatedByUserId == inspectorId
        && form.Status == PreventiveMaintenanceFormStatusCatalog.Draft
        && form.FileNumber is null
        && form.AssetCategory == "fire-extinguisher"
        && form.Building is null
        && form.Department == "CCMS"
        && form.PmCycle is null
        && form.PeriodType == "Quarter"
        && form.Quarter == "Q3"
        && form.Semester is null
        && form.Year == 2026
        && form.AcademicYear == "2026-2027"
        && form.SubmittedByUserId is null
        && form.SubmittedAt is null
        && form.FieldWorkCompletedAt is null
        && form.Acknowledgement is null;

    private async Task<ApplicationDbContext> CreateReadyContextAsync(CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException("The database is not reachable for demo seeding.");
            }

            if (context.Database.IsRelational() && !StagingDemoInitialization.Enabled(configuration))
            {
                await context.Database.MigrateAsync(cancellationToken);
            }

            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private void EnsureDevelopment()
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Development demo commands are available only in Development.");
        }
    }

    private static DateTimeOffset AtManila(int year, int month, int day, int hour)
    {
        return new DateTimeOffset(year, month, day, hour, 0, 0, ManilaOffset);
    }
}

internal sealed record DemoFormIdentity(
    string AssetCategory,
    string Building,
    string Department,
    string PmCycle,
    string? FileNumber);

internal sealed record DevelopmentDemoSeedResult(
    int Assets,
    int Schedules,
    int Inspections,
    int Forms,
    int Acknowledgements);

internal sealed record DevelopmentDemoResetResult(
    int AssetsRemoved,
    int SchedulesRemoved,
    int InspectionsRemoved,
    int FormsRemoved,
    int AcknowledgementsRemoved);
