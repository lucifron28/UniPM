using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Data.Seeding;

internal sealed class DevelopmentDemoSeeder(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    MaintenanceSearchDocumentProjector searchDocumentProjector,
    IHostEnvironment environment)
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
        await ValidateConflictsAsync(context, cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);

        await RemoveDemoClosureAsync(context, cancellationToken);

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
            SignatoryPosition = "Fictional Department Head",
            SignatureData = SignatureBase64,
            SignatureContentType = "image/png",
            SignatureChecksum = Convert.ToHexString(SHA256.HashData(signatureBytes)),
            CapturedByUserId = gsdId,
            AcknowledgedAt = AtManila(2026, 7, 26, 9)
        });

        await context.SaveChangesAsync(cancellationToken);
        await searchDocumentProjector.RebuildAsync(
            context,
            inspections
                .Where(inspection => inspection.PreventiveMaintenanceFormId == acknowledgedForm.Id)
                .Select(inspection => inspection.Id)
                .ToHashSet(),
            cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new DevelopmentDemoSeedResult(
            DevelopmentDemoCatalog.Assets.Count,
            schedules.Count,
            inspections.Count,
            2,
            1);
    }

    internal async Task<DevelopmentDemoResetResult> ResetAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();
        await using var context = await CreateReadyContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);
        var result = await RemoveDemoClosureAsync(context, cancellationToken);

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
            var scheduleDate = asset.Scenario switch
            {
                "scenario-a" => AtManila(2026, 9, 15, 8),
                "scenario-b" => AtManila(2026, 8, 15, 8),
                _ => AtManila(2026, 7, 15, 8)
            };
            DateTimeOffset? completedAt = asset.Scenario switch
            {
                "scenario-b" when index == 3 => AtManila(2026, 8, 20, 10),
                "scenario-b" when index == 4 => AtManila(2026, 8, 27, 11),
                "scenario-b" => AtManila(2026, 9, 2, 9),
                "scenario-c" => AtManila(2026, 7, 20 + (index - 6), 10),
                _ => null
            };

            schedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = scheduleId,
                AssetId = asset.Id,
                ScheduleDate = scheduleDate,
                PmCycle = scheduleDate.ToString("yyyy-MM"),
                PeriodType = "Quarter",
                Quarter = "Q3",
                Year = 2026,
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
            FileNumber = "PM-2026-DEMO-0801",
            AssetCategory = "fire-extinguisher",
            Building = "University Library",
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
            CreatedAt = AtManila(2026, 8, 15, 8),
            UpdatedAt = AtManila(2026, 9, 2, 10)
        };
    }

    private static PreventiveMaintenanceForm BuildAcknowledgedForm(Guid inspectorId)
    {
        return new PreventiveMaintenanceForm
        {
            Id = DevelopmentDemoCatalog.FormIds[1],
            FileNumber = "PM-2026-DEMO-0701",
            AssetCategory = "emergency-light",
            Building = "Administration Building",
            Department = "Student Affairs Office",
            PmCycle = "2026-07",
            PeriodType = "Quarter",
            Quarter = "Q3",
            Year = 2026,
            AcademicYear = "2026-2027",
            Status = PreventiveMaintenanceFormStatusCatalog.Acknowledged,
            CreatedByUserId = inspectorId,
            SubmittedByUserId = inspectorId,
            FieldWorkCompletedAt = AtManila(2026, 7, 22, 10),
            SubmittedAt = AtManila(2026, 7, 22, 11),
            CreatedAt = AtManila(2026, 7, 15, 8),
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
            AtManila(2026, 7, 20, 10),
            AtManila(2026, 7, 21, 10),
            AtManila(2026, 7, 22, 10)
        };
        var remarks = new[]
        {
            "Pressure gauge below the green operating range.",
            "Safety pin seal is worn and should be replaced.",
            "Mounting bracket is loose near the archives room entrance.",
            "Emergency light passed the simulated power interruption test.",
            "Battery duration was below the expected inspection interval.",
            "Exit illumination remained stable during the test."
        };
        var recommendations = new[]
        {
            "Recharge the cylinder and verify pressure before returning it to service.",
            "Replace the tamper seal during corrective servicing.",
            "Secure or replace the mounting bracket.",
            null,
            "Schedule battery replacement and repeat the duration test.",
            null
        };
        var operational = new[] { false, true, false, true, false, true };
        var inspections = new List<InspectionRecord>();

        for (var index = 0; index < DevelopmentDemoCatalog.InspectionIds.Count; index++)
        {
            var assetIndex = index + 3;
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

    private static async Task<(Guid InspectorId, Guid GsdId)> ResolveUsersAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var emails = new[] { DevelopmentDemoCatalog.InspectorEmail, DevelopmentDemoCatalog.GsdEmail };
        var matchingUsers = await context.Users
            .AsNoTracking()
            .Where(user => user.Email != null && emails.Contains(user.Email))
            .Select(user => new { user.Email, user.Id })
            .ToListAsync(cancellationToken);
        var users = matchingUsers.ToDictionary(
            user => user.Email!,
            user => user.Id,
            StringComparer.OrdinalIgnoreCase);

        if (!users.TryGetValue(DevelopmentDemoCatalog.InspectorEmail, out var inspectorId)
            || !users.TryGetValue(DevelopmentDemoCatalog.GsdEmail, out var gsdId))
        {
            throw new InvalidOperationException(
                "Development demo seeding requires inspector@unipm.local and gsd@unipm.local. Run --seed-development-users first.");
        }

        return (inspectorId, gsdId);
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
        var fileNumbers = new[] { "PM-2026-DEMO-0801", "PM-2026-DEMO-0701" };
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
        CancellationToken cancellationToken)
    {
        var assetIds = DevelopmentDemoCatalog.Assets.Select(asset => asset.Id).ToHashSet();
        var fixedScheduleIds = DevelopmentDemoCatalog.ScheduleIds.ToHashSet();
        var fixedFormIds = DevelopmentDemoCatalog.FormIds.ToHashSet();
        var scheduleIds = (await context.PreventiveMaintenanceSchedules
                .Where(schedule => assetIds.Contains(schedule.AssetId) || fixedScheduleIds.Contains(schedule.Id))
                .Select(schedule => schedule.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var ownedInspections = await context.InspectionRecords
            .Where(inspection => assetIds.Contains(inspection.AssetId)
                || scheduleIds.Contains(inspection.ScheduleId)
                || inspection.PreventiveMaintenanceFormId.HasValue
                    && fixedFormIds.Contains(inspection.PreventiveMaintenanceFormId.Value))
            .ToListAsync(cancellationToken);
        var formIds = ownedInspections
            .Where(inspection => inspection.PreventiveMaintenanceFormId.HasValue)
            .Select(inspection => inspection.PreventiveMaintenanceFormId.GetValueOrDefault())
            .Concat(fixedFormIds)
            .ToHashSet();
        var mixedFormDependency = await context.InspectionRecords
            .AnyAsync(inspection => inspection.PreventiveMaintenanceFormId.HasValue
                && formIds.Contains(inspection.PreventiveMaintenanceFormId.Value)
                && !assetIds.Contains(inspection.AssetId)
                && !scheduleIds.Contains(inspection.ScheduleId), cancellationToken);
        if (mixedFormDependency)
        {
            throw new InvalidOperationException(
                "Demo reset refused because a demo form contains a non-demo inspection row.");
        }

        var inspectionIds = ownedInspections.Select(inspection => inspection.Id).ToHashSet();
        var embeddings = await context.MaintenanceSearchDocumentEmbeddings
            .Where(embedding => inspectionIds.Contains(embedding.InspectionId))
            .ToListAsync(cancellationToken);
        var documents = await context.MaintenanceSearchDocuments
            .Where(document => inspectionIds.Contains(document.InspectionId))
            .ToListAsync(cancellationToken);
        var acknowledgements = await context.PreventiveMaintenanceAcknowledgements
            .Where(acknowledgement => formIds.Contains(acknowledgement.FormId))
            .ToListAsync(cancellationToken);
        var forms = await context.PreventiveMaintenanceForms
            .Where(form => formIds.Contains(form.Id))
            .ToListAsync(cancellationToken);
        var schedules = await context.PreventiveMaintenanceSchedules
            .Where(schedule => scheduleIds.Contains(schedule.Id))
            .ToListAsync(cancellationToken);
        var assets = await context.Assets
            .Where(asset => assetIds.Contains(asset.Id))
            .ToListAsync(cancellationToken);

        context.MaintenanceSearchDocumentEmbeddings.RemoveRange(embeddings);
        context.MaintenanceSearchDocuments.RemoveRange(documents);
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

    private async Task<ApplicationDbContext> CreateReadyContextAsync(CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException("The database is not reachable for demo seeding.");
            }

            if (context.Database.IsRelational())
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
