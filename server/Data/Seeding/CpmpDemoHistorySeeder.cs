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

    internal static async Task<DevelopmentDemoSeedResult> SeedAsync(
        ApplicationDbContext context, Guid inspectorId, CancellationToken cancellationToken)
    {
        var supervisorId = await context.Users.Where(user => user.Email == "supervisor@unipm.local")
            .Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);
        var categories = new[] { "fire-extinguisher", "fire-alarm", "emergency-light", "water-drinking-station" };
        // Separate fictional departments avoid adding rows to existing submitted demo batches.
        var departments = new[] { "Demo CCMS", "Demo Library" };
        var assetCount = 0;
        var scheduleCount = 0;
        var inspectionCount = 0;
        var formCount = 0;
        var acknowledgementCount = 0;

        foreach (var category in categories)
        foreach (var department in departments)
        {
            var prefix = category switch
            {
                "fire-extinguisher" => "FE", "fire-alarm" => "FA",
                "emergency-light" => "EL", _ => "WDS"
            };
            var assets = new List<Asset>();
            for (var index = 1; index <= 2; index++)
            {
                var code = $"CPMP-DEMO-{department.ToUpperInvariant().Replace(' ', '-')}-{prefix}-{index:00}";
                var id = Id(code);
                var asset = await context.Assets.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
                if (asset is null)
                {
                    if (await context.Assets.AnyAsync(row => row.AssetCode == code, cancellationToken))
                        throw new InvalidOperationException("CPMP demo code conflicts with an unrelated asset.");
                    asset = new Asset
                    {
                        Id = id, AssetCode = code, AssetCategory = category, Department = department,
                        Building = department == "Demo CCMS" ? "CCMS Building" : "University Library",
                        Location = $"Fictional demo area {index}", Status = "Active",
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
                            Id = formId, AssetCategory = category, Department = department,
                            Building = assets[0].Building, PmCycle = cycle, PeriodType = "Quarter",
                            Quarter = $"Q{(month - 1) / 3 + 1}", Year = year,
                            AcademicYear = $"{year}-{year + 1}", Status = status,
                            FileNumber = draft ? null : $"PM-{year}-CPMP-{department.ToUpperInvariant()}-{prefix}-{month:00}",
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
                            Remarks = index == 0 ? "Fictional routine check: operational." : "Fictional inspection: non-operational; GSD review required.",
                            ActionsRecommendations = index == 0 ? null : "Prepare a corrective-action handoff for human review.",
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
                                SignatoryName = "Fictional Department Representative", SignatoryPosition = "Demo signatory",
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

    private static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"unipm-cpmp-demo-v2:{key}"))[..16]);
    private static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 10, 0, 0, Manila);
}
