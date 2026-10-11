using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class RegistryReadEndpointsTests
{
    [Fact]
    public async Task Asset_registry_is_bounded_searchable_and_keeps_legacy_array_contract()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.SeedAssetsAsync(1_000);

        var page = await client.GetFromJsonAsync<AssetRegistryPageResponse>(
            "/api/v1/assets/registry?page=3&pageSize=100");

        Assert.NotNull(page);
        Assert.Equal(3, page.Page);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(1_000, page.TotalCount);
        Assert.Equal(100, page.Items.Count);
        Assert.Equal("REG-0200", page.Items[0].AssetCode);
        Assert.Equal("REG-0299", page.Items[^1].AssetCode);

        var matchingAssets = await client.GetFromJsonAsync<AssetRegistryPageResponse>(
            "/api/v1/assets/registry?search=Control%20Room%20999");
        Assert.NotNull(matchingAssets);
        Assert.Equal("REG-0999", Assert.Single(matchingAssets.Items).AssetCode);

        var options = await client.GetFromJsonAsync<AssetRegistryOptionsResponse>(
            "/api/v1/assets/registry/options?assetCategory=fire-extinguisher&department=GSD");
        Assert.NotNull(options);
        Assert.Equal(4, options.Categories.Count);
        Assert.Contains("GSD", options.Departments);
        Assert.NotEmpty(options.Buildings);

        var summary = await client.GetFromJsonAsync<AssetRegistrySummaryResponse>(
            "/api/v1/assets/registry/summary");
        Assert.NotNull(summary);
        Assert.Equal(1_000, summary.TotalCount);
        Assert.Equal(250, Assert.Single(summary.Categories, item =>
            item.AssetCategory == "fire-alarm").TotalCount);
        Assert.Equal(
            summary.TotalCount,
            summary.ActiveCount + summary.InactiveCount + summary.RetiredCount);

        var legacyAssets = await client.GetFromJsonAsync<List<AssetResponse>>(
            "/api/v1/assets?limit=10");
        Assert.NotNull(legacyAssets);
        Assert.Equal(10, legacyAssets.Count);
    }

    [Fact]
    public async Task Schedule_asset_lookup_applies_department_eligibility_before_limit()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.SeedScheduleLookupAssetsAsync();

        var eligibleAssets = await client.GetFromJsonAsync<List<AssetResponse>>(
            "/api/v1/assets?status=Active&hasDepartment=true&limit=2");

        Assert.NotNull(eligibleAssets);
        Assert.Equal(
            ["ZZZ-ELIGIBLE-00", "ZZZ-ELIGIBLE-01"],
            eligibleAssets.Select(asset => asset.AssetCode).ToArray());
        Assert.All(eligibleAssets, asset =>
        {
            Assert.Equal("Active", asset.Status);
            Assert.False(string.IsNullOrWhiteSpace(asset.Department));
        });
    }

    [Fact]
    public async Task Schedule_registry_pages_canonical_periods_and_preserves_inspector_scope()
    {
        await using var application = new TestApplicationFactory([AuthRoleCatalog.Inspector]);
        using var client = application.CreateClient();
        await application.SeedScheduleRegistryAsync(TestAuthenticationHandler.UserId);

        var secondPage = await client.GetFromJsonAsync<ScheduleRegistryPageResponse>(
            "/api/v1/schedules/registry?page=2&pageSize=100");
        Assert.NotNull(secondPage);
        Assert.Equal(110, secondPage.TotalCount);
        Assert.Equal(110, secondPage.OverallCount);
        Assert.Equal(10, secondPage.Items.Count);

        var quarter = await client.GetFromJsonAsync<ScheduleRegistryPageResponse>(
            "/api/v1/schedules/registry?quarter=Q2");
        Assert.NotNull(quarter);
        Assert.Equal(4, quarter.TotalCount);
        Assert.Equal(110, quarter.OverallCount);
        Assert.Equal(
            new[]
            {
                "emergency-light",
                "fire-alarm",
                "fire-extinguisher",
                "water-drinking-station"
            },
            quarter.Items
                .Select(item => item.Asset!.AssetCategory)
                .OrderBy(category => category, StringComparer.Ordinal)
                .ToArray());

        var exactCycle = await client.GetFromJsonAsync<ScheduleRegistryPageResponse>(
            "/api/v1/schedules/registry?pmCycle=2026-06");
        Assert.NotNull(exactCycle);
        Assert.Equal(2, exactCycle.TotalCount);

        var options = await client.GetFromJsonAsync<ScheduleRegistryOptionsResponse>(
            "/api/v1/schedules/registry/options");
        Assert.NotNull(options);
        Assert.Equal(new[] { "GSD" }, options.Departments);
        Assert.Equal(new[] { "2026" }, options.Years);

        var summary = await client.GetFromJsonAsync<ScheduleRegistrySummaryResponse>(
            "/api/v1/schedules/registry/summary");
        Assert.NotNull(summary);
        Assert.Equal(110, summary.TotalCount);
        Assert.Equal(110, summary.DueCount);

        var legacySchedules = await client.GetFromJsonAsync<List<ScheduleResponse>>(
            "/api/v1/schedules?quarter=Q2");
        Assert.NotNull(legacySchedules);
        Assert.Equal(4, legacySchedules.Count);
    }

    [Fact]
    public async Task Inspection_registry_filters_linked_cycle_and_publishes_only_official_rows()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        var seeded = await application.SeedInspectionRegistryAsync();

        var exactCycle = await client.GetFromJsonAsync<InspectionRegistryPageResponse>(
            "/api/v1/inspections/registry?year=2026&quarter=Q2&pmCycle=2026-06&pageSize=100");
        Assert.NotNull(exactCycle);
        Assert.Equal(101, exactCycle.TotalCount);
        Assert.Equal(102, exactCycle.OverallCount);
        Assert.Equal(100, exactCycle.Items.Count);
        var lateInspection = Assert.Single(exactCycle.Items, item =>
            item.Id == seeded.LateInspectionId);
        Assert.Equal(2027, lateInspection.DateInspected.Year);
        Assert.Equal("2026-06", lateInspection.Schedule?.PmCycle);
        Assert.Equal("FA-LATE", lateInspection.Asset?.AssetCode);

        var calendarQuarter = await client.GetFromJsonAsync<InspectionRegistryPageResponse>(
            "/api/v1/inspections/registry?year=2026&quarter=Q2&pageSize=100");
        Assert.NotNull(calendarQuarter);
        Assert.Equal(102, calendarQuarter.TotalCount);

        var lateDate = Uri.EscapeDataString("2027-01-01T00:00:00+08:00");
        var lateDateOnly = await client.GetFromJsonAsync<InspectionRegistryPageResponse>(
            $"/api/v1/inspections/registry?pmCycle=2026-06&dateFrom={lateDate}");
        Assert.NotNull(lateDateOnly);
        Assert.Equal(seeded.LateInspectionId, Assert.Single(lateDateOnly.Items).Id);

        var options = await client.GetFromJsonAsync<InspectionRegistryOptionsResponse>(
            "/api/v1/inspections/registry/options");
        Assert.NotNull(options);
        Assert.Equal(new[] { "2026" }, options.Years);

        var summary = await client.GetFromJsonAsync<InspectionRegistrySummaryResponse>(
            "/api/v1/inspections/registry/summary");
        Assert.NotNull(summary);
        Assert.Equal(102, summary.TotalCount);
        Assert.Equal(51, summary.OperationalCount);
        Assert.Equal(51, summary.NonOperationalCount);

        var legacyInspections = await client.GetFromJsonAsync<List<InspectionResponse>>(
            "/api/v1/inspections");
        Assert.NotNull(legacyInspections);
        Assert.Equal(102, legacyInspections.Count);
        Assert.DoesNotContain(legacyInspections, item =>
            seeded.DraftInspectionIds.Contains(item.Id));
    }

    private sealed class TestApplicationFactory(string[]? roles = null)
        : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"unipm-registry-reads-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.DisableScheduleGenerationWorker();
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(roles ?? [AuthRoleCatalog.Gsd]);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }

        public async Task SeedAssetsAsync(int count)
        {
            string[] categories =
            [
                "fire-extinguisher",
                "fire-alarm",
                "emergency-light",
                "water-drinking-station"
            ];
            string[] statuses = ["Active", "Inactive", "Retired"];
            var now = DateTimeOffset.UtcNow;
            var assets = Enumerable.Range(0, count)
                .Select(index => new Asset
                {
                    Id = Guid.NewGuid(),
                    AssetCode = $"REG-{index:D4}",
                    AssetCategory = categories[index % categories.Length],
                    Building = (index % 3) switch
                    {
                        0 => "Main Building",
                        1 => "Science Building",
                        _ => "Library"
                    },
                    Department = index % 2 == 0 ? "GSD" : "Facilities",
                    Location = index == 999 ? "Control Room 999" : $"Floor {index % 12 + 1}",
                    Status = statuses[index % statuses.Length],
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            await using var context = await CreateContextAsync();
            context.Assets.AddRange(assets);
            await context.SaveChangesAsync();
        }

        public async Task SeedScheduleLookupAssetsAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var ineligibleAssets = Enumerable.Range(0, 32)
                .Select(index => new Asset
                {
                    Id = Guid.NewGuid(),
                    AssetCode = $"AAA-NO-DEPARTMENT-{index:D2}",
                    AssetCategory = "fire-alarm",
                    Building = "Main Building",
                    Department = index % 2 == 0 ? null : "  ",
                    Location = "Ineligible asset",
                    Status = "Active",
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .Append(new Asset
                {
                    Id = Guid.NewGuid(),
                    AssetCode = "BBB-INACTIVE",
                    AssetCategory = "fire-alarm",
                    Building = "Main Building",
                    Department = "GSD",
                    Location = "Inactive asset",
                    Status = "Inactive",
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();
            var eligibleAssets = Enumerable.Range(0, 3)
                .Select(index => new Asset
                {
                    Id = Guid.NewGuid(),
                    AssetCode = $"ZZZ-ELIGIBLE-{index:D2}",
                    AssetCategory = "fire-alarm",
                    Building = "Main Building",
                    Department = "GSD",
                    Location = "Eligible asset",
                    Status = "Active",
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            await using var context = await CreateContextAsync();
            context.Assets.AddRange(ineligibleAssets);
            context.Assets.AddRange(eligibleAssets);
            await context.SaveChangesAsync();
        }

        public async Task SeedScheduleRegistryAsync(Guid assignedUserId)
        {
            var assets = new List<Asset>();
            var schedules = new List<PreventiveMaintenanceSchedule>();
            var now = DateTimeOffset.UtcNow;
            var sequence = 0;

            void AddSchedule(
                string category,
                string cycle,
                string periodType,
                string? quarter,
                string? semester,
                Guid? assignedToUserId,
                string department = "GSD")
            {
                sequence++;
                var asset = new Asset
                {
                    Id = Guid.NewGuid(),
                    AssetCode = $"REG-SCH-{sequence:D3}",
                    AssetCategory = category,
                    Building = "Main Building",
                    Department = department,
                    Location = "Mechanical room",
                    Status = "Active",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var year = int.Parse(cycle[..4]);
                var month = int.Parse(cycle[^2..]);
                var schedule = new PreventiveMaintenanceSchedule
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    Asset = asset,
                    ScheduleDate = new DateTimeOffset(
                            year,
                            month,
                            DateTime.DaysInMonth(year, month),
                            23,
                            59,
                            59,
                            TimeSpan.FromHours(8))
                        .AddTicks(TimeSpan.TicksPerSecond - 1),
                    PmCycle = cycle,
                    PeriodType = periodType,
                    Status = "Due",
                    Quarter = quarter,
                    Semester = semester,
                    Year = year,
                    AssignedToUserId = assignedToUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                assets.Add(asset);
                schedules.Add(schedule);
            }

            for (var index = 0; index < 105; index++)
            {
                AddSchedule("fire-extinguisher", "2026-08", "Quarter", "Q3", null, assignedUserId);
            }

            AddSchedule("fire-alarm", "2026-06", "Semester", null, "First", assignedUserId);
            AddSchedule("emergency-light", "2026-06", "Semester", null, "First", assignedUserId);
            AddSchedule("water-drinking-station", "2026-05", "Quarter", "Q2", null, assignedUserId);
            AddSchedule("fire-extinguisher", "2026-05", "Quarter", "Q2", null, assignedUserId);
            AddSchedule("fire-extinguisher", "2026-11", "Quarter", "Q4", null, assignedUserId);
            AddSchedule("fire-extinguisher", "2025-05", "Quarter", "Q2", null, Guid.NewGuid(), "Other Department");

            await using var context = await CreateContextAsync();
            context.Assets.AddRange(assets);
            context.PreventiveMaintenanceSchedules.AddRange(schedules);
            await context.SaveChangesAsync();
        }

        public async Task<InspectionSeed> SeedInspectionRegistryAsync()
        {
            var assets = new List<Asset>();
            var schedules = new List<PreventiveMaintenanceSchedule>();
            var forms = new List<PreventiveMaintenanceForm>();
            var acknowledgements = new List<PreventiveMaintenanceAcknowledgement>();
            var inspections = new List<InspectionRecord>();
            var draftIds = new List<Guid>();
            var now = DateTimeOffset.UtcNow;
            var lateInspectionId = Guid.NewGuid();

            for (var index = 0; index < 100; index++)
            {
                var asset = NewAsset($"FA-LEGACY-{index:D3}", "fire-alarm", now);
                var schedule = NewSchedule(asset, "2026-06", "Semester", null, "First", now);
                var inspectedAt = new DateTimeOffset(
                    2026,
                    6,
                    15,
                    8,
                    0,
                    0,
                    TimeSpan.FromHours(8));
                assets.Add(asset);
                schedules.Add(schedule);
                inspections.Add(NewInspection(
                    schedule,
                    asset,
                    null,
                    inspectedAt,
                    index % 2 == 0,
                    $"Legacy official {index:D3}",
                    now));
            }

            var lateAsset = NewAsset("FA-LATE", "fire-alarm", now);
            var lateSchedule = NewSchedule(lateAsset, "2026-06", "Semester", null, "First", now);
            var lateForm = NewForm("fire-alarm", "2026-06", "Semester", null, "First", "Acknowledged", now);
            var lateAcknowledgement = NewAcknowledgement(lateForm, now);
            var lateInspection = NewInspection(
                lateSchedule,
                lateAsset,
                lateForm,
                new DateTimeOffset(2027, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
                false,
                "Late but official",
                now);
            lateInspection.Id = lateInspectionId;
            lateInspection.DateAccomplished = new DateTimeOffset(
                2027,
                1,
                16,
                8,
                0,
                0,
                TimeSpan.FromHours(8));
            assets.Add(lateAsset);
            schedules.Add(lateSchedule);
            forms.Add(lateForm);
            acknowledgements.Add(lateAcknowledgement);
            inspections.Add(lateInspection);

            var fireExtinguisherAsset = NewAsset("FE-MAY", "fire-extinguisher", now);
            var fireExtinguisherSchedule = NewSchedule(
                fireExtinguisherAsset,
                "2026-05",
                "Quarter",
                "Q2",
                null,
                now);
            var fireExtinguisherForm = NewForm(
                "fire-extinguisher",
                "2026-05",
                "Quarter",
                "Q2",
                null,
                "Acknowledged",
                now);
            assets.Add(fireExtinguisherAsset);
            schedules.Add(fireExtinguisherSchedule);
            forms.Add(fireExtinguisherForm);
            acknowledgements.Add(NewAcknowledgement(fireExtinguisherForm, now));
            inspections.Add(NewInspection(
                fireExtinguisherSchedule,
                fireExtinguisherAsset,
                fireExtinguisherForm,
                new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.FromHours(8)),
                true,
                "May official",
                now));

            var draftAsset = NewAsset("FA-DRAFT", "fire-alarm", now);
            draftAsset.Department = "Synthetic Draft Department";
            var draftSchedule = NewSchedule(
                draftAsset,
                "2026-06",
                "Semester",
                null,
                "First",
                now);
            var draftForm = NewForm(
                "fire-alarm",
                "2026-06",
                "Semester",
                null,
                "First",
                "Draft",
                now);
            draftForm.Department = "Synthetic Draft Department";
            var draftInspection = NewInspection(
                draftSchedule,
                draftAsset,
                draftForm,
                new DateTimeOffset(2027, 2, 15, 8, 0, 0, TimeSpan.FromHours(8)),
                false,
                "Unpublished draft",
                now);
            assets.Add(draftAsset);
            schedules.Add(draftSchedule);
            forms.Add(draftForm);
            inspections.Add(draftInspection);
            draftIds.Add(draftInspection.Id);

            await using var context = await CreateContextAsync();
            context.Assets.AddRange(assets);
            context.PreventiveMaintenanceSchedules.AddRange(schedules);
            context.PreventiveMaintenanceForms.AddRange(forms);
            context.PreventiveMaintenanceAcknowledgements.AddRange(acknowledgements);
            context.InspectionRecords.AddRange(inspections);
            await context.SaveChangesAsync();

            return new InspectionSeed(lateInspectionId, draftIds);
        }

        private async Task<ApplicationDbContext> CreateContextAsync()
        {
            return await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
        }

        private static Asset NewAsset(string code, string category, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            AssetCode = code,
            AssetCategory = category,
            Building = "Main Building",
            Department = "GSD",
            Location = "Inspection Area",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        };

        private static PreventiveMaintenanceSchedule NewSchedule(
            Asset asset,
            string cycle,
            string periodType,
            string? quarter,
            string? semester,
            DateTimeOffset now)
        {
            var year = int.Parse(cycle[..4]);
            var month = int.Parse(cycle[^2..]);
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                Asset = asset,
                ScheduleDate = new DateTimeOffset(
                        year,
                        month,
                        DateTime.DaysInMonth(year, month),
                        23,
                        59,
                        59,
                        TimeSpan.FromHours(8))
                    .AddTicks(TimeSpan.TicksPerSecond - 1),
                PmCycle = cycle,
                PeriodType = periodType,
                Status = "Completed",
                Quarter = quarter,
                Semester = semester,
                Year = year,
                CompletedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            return schedule;
        }

        private static PreventiveMaintenanceForm NewForm(
            string category,
            string cycle,
            string periodType,
            string? quarter,
            string? semester,
            string status,
            DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            AssetCategory = category,
            Building = "Main Building",
            Department = "GSD",
            PmCycle = cycle,
            PeriodType = periodType,
            Quarter = quarter,
            Semester = semester,
            Year = int.Parse(cycle[..4]),
            Status = status,
            CreatedByUserId = TestAuthenticationHandler.UserId,
            SubmittedByUserId = status == "Acknowledged" ? TestAuthenticationHandler.UserId : null,
            SubmittedAt = status == "Acknowledged" ? now : null,
            FieldWorkCompletedAt = status == "Acknowledged" ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        private static PreventiveMaintenanceAcknowledgement NewAcknowledgement(
            PreventiveMaintenanceForm form,
            DateTimeOffset now)
        {
            var acknowledgement = new PreventiveMaintenanceAcknowledgement
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                Form = form,
                SignatoryName = "Synthetic Department Head",
                SignatoryPosition = "Department Head",
                CapturedByUserId = TestAuthenticationHandler.UserId,
                AcknowledgedAt = now
            };
            form.Acknowledgement = acknowledgement;
            return acknowledgement;
        }

        private static InspectionRecord NewInspection(
            PreventiveMaintenanceSchedule schedule,
            Asset asset,
            PreventiveMaintenanceForm? form,
            DateTimeOffset dateInspected,
            bool operational,
            string remarks,
            DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            Schedule = schedule,
            AssetId = asset.Id,
            Asset = asset,
            PreventiveMaintenanceFormId = form?.Id,
            PreventiveMaintenanceForm = form,
            InspectorUserId = TestAuthenticationHandler.UserId,
            DateInspected = dateInspected,
            DateAccomplished = dateInspected,
            IsOperational = operational,
            Remarks = remarks,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed record InspectionSeed(Guid LateInspectionId, IReadOnlyList<Guid> DraftInspectionIds);
}
