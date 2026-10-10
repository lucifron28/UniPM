using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PreventiveMaintenanceScheduleGenerationTests
{
    [Fact]
    public async Task Asset_registration_creates_current_month_and_future_cycles_atomically()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = "GEN-FE-001",
            assetCategory = "fire-extinguisher",
            department = "CCMS",
            building = "Main",
            location = "Lobby"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var asset = await response.Content.ReadFromJsonAsync<AssetRef>();
        Assert.NotNull(asset);

        var schedules = await application.ReadSchedulesAsync(asset.Id);
        Assert.Equal(["2026-08", "2026-11"], schedules.Select(schedule => schedule.PmCycle));
        Assert.All(schedules, schedule => Assert.Equal("Due", schedule.Status));
        Assert.Equal(["Q3", "Q4"], schedules.Select(schedule => schedule.Quarter));
        Assert.All(schedules, schedule =>
        {
            Assert.Equal("Quarter", schedule.PeriodType);
            Assert.Null(schedule.AssignedToUserId);
            Assert.Null(schedule.AssignedSupervisorUserId);
        });

        using var duplicate = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle = "2026-11",
            periodType = "Quarter",
            quarter = "Q4"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Registration_enrolls_asset_in_an_untouched_existing_batch()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var existingAsset = await application.SeedAssetAsync(
            "GEN-FE-BATCH-BASE",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            department: "CCMS");
        await application.SeedScheduleAsync(
            existingAsset.Id, "2026-08", ScheduleStatusCatalog.Due, null, null, null);

        using var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = "GEN-FE-BATCH-NEW",
            assetCategory = "fire-extinguisher",
            department = "CCMS",
            building = "Main",
            location = "Lobby"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var asset = await response.Content.ReadFromJsonAsync<AssetRef>();
        Assert.NotNull(asset);
        Assert.Equal(["2026-08", "2026-11"],
            (await application.ReadSchedulesAsync(asset.Id)).Select(schedule => schedule.PmCycle));
        Assert.Empty(await application.ReadDeferralsAsync(asset.Id));
    }

    [Fact]
    public async Task Yearly_recovery_defers_missing_asset_when_its_pm_batch_is_locked()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var lockedAsset = await application.SeedAssetAsync(
            "GEN-RECOVERY-LOCKED",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            department: "CCMS");
        var missingAsset = await application.SeedAssetAsync(
            "GEN-RECOVERY-MISSING",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            department: "CCMS");
        await application.SeedScheduleAsync(
            lockedAsset.Id,
            "2026-08",
            ScheduleStatusCatalog.Due,
            assignedToUserId: null,
            assignedSupervisorUserId: Guid.NewGuid(),
            completedAt: null);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/schedules/generate",
            new { year = 2026 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            await application.ReadSchedulesAsync(missingAsset.Id),
            schedule => schedule.PmCycle == "2026-08");
        Assert.Contains(
            await application.ReadSchedulesAsync(missingAsset.Id),
            schedule => schedule.PmCycle == "2026-11");
        var deferral = Assert.Single(await application.ReadDeferralsAsync(missingAsset.Id));
        Assert.Equal("2026-08", deferral.PmCycle);
        Assert.Equal("BatchAssigned", deferral.ReasonCode);
        Assert.Equal("2026-11", deferral.NextEligiblePmCycle);

        var scheduleSnapshots = await application.ReadScheduleSnapshotsAsync(missingAsset.Id);
        using var repeated = await client.PostAsJsonAsync(
            "/api/v1/schedules/generate",
            new { year = 2026 });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(scheduleSnapshots, await application.ReadScheduleSnapshotsAsync(missingAsset.Id));
        Assert.Single(await application.ReadDeferralsAsync(missingAsset.Id));
    }

    [Fact]
    public async Task Requested_year_recovery_respects_registration_date_and_is_idempotent()
    {
        var now = new DateTimeOffset(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var asset = await application.SeedAssetAsync(
            "GEN-WDS-OLD",
            "water-drinking-station",
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));

        using var response = await client.PostAsJsonAsync("/api/v1/schedules/generate", new { year = 2026 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = await response.Content.ReadFromJsonAsync<GenerationResult>();
        Assert.NotNull(first);
        Assert.Equal(2026, first.Year);
        Assert.Equal(1, first.EligibleAssets);
        Assert.Equal(1, first.CreatedSchedules);
        Assert.Equal(0, first.ExistingSchedules);

        var generated = await application.ReadSchedulesAsync(asset.Id);
        Assert.Equal(["2026-11"], generated.Select(item => item.PmCycle));
        Assert.Equal("Due", Assert.Single(generated).Status);
        Assert.All(generated, schedule =>
        {
            Assert.Equal("Quarter", schedule.PeriodType);
            Assert.Null(schedule.Semester);
            Assert.Null(schedule.AcademicYear);
        });

        using var secondResponse = await client.PostAsJsonAsync("/api/v1/schedules/generate", new { year = 2026 });
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<GenerationResult>();
        Assert.NotNull(second);
        Assert.Equal(0, second.CreatedSchedules);
        Assert.Equal(1, second.ExistingSchedules);
        Assert.Single(await application.ReadSchedulesAsync(asset.Id));
    }

    [Fact]
    public async Task Yearly_recovery_keeps_upcoming_cycles_and_surfaces_missing_past_cycles_for_gsd_review()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var asset = await application.SeedAssetAsync(
            "GEN-COVERAGE-UNCONFIRMED",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        var existingCycleAsset = await application.SeedAssetAsync(
            "GEN-COVERAGE-EXISTING",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        await application.SeedScheduleAsync(
            existingCycleAsset.Id,
            "2026-02",
            ScheduleStatusCatalog.Due,
            assignedToUserId: null,
            assignedSupervisorUserId: null,
            completedAt: null);
        var deferredCycleAsset = await application.SeedAssetAsync(
            "GEN-COVERAGE-DEFERRED",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        await application.SeedDeferralAsync(
            deferredCycleAsset.Id,
            "2026-05",
            "2026-08",
            now);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/schedules/generate",
            new { year = 2026 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GenerationResult>();
        Assert.NotNull(result);
        Assert.Equal(6, result.CreatedSchedules);
        Assert.Equal(4, result.CyclesRequiringGsdCoverageReview);
        Assert.Equal(["2026-08", "2026-11"],
            (await application.ReadSchedulesAsync(asset.Id)).Select(schedule => schedule.PmCycle));
        Assert.Contains(
            await application.ReadSchedulesAsync(existingCycleAsset.Id),
            schedule => schedule.PmCycle == "2026-02");
        Assert.DoesNotContain(
            await application.ReadSchedulesAsync(deferredCycleAsset.Id),
            schedule => schedule.PmCycle == "2026-05");
        Assert.Single(await application.ReadDeferralsAsync(deferredCycleAsset.Id));
    }

    [Fact]
    public async Task Yearly_recovery_catches_up_only_cycles_on_or_after_approved_coverage_date()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(
            AuthRoleCatalog.Gsd,
            now,
            schedulingEffectiveDate: "2026-05-31");
        using var client = application.CreateClient();
        var asset = await application.SeedAssetAsync(
            "GEN-COVERAGE-APPROVED",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));

        using var response = await client.PostAsJsonAsync(
            "/api/v1/schedules/generate",
            new { year = 2026 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GenerationResult>();
        Assert.NotNull(result);
        Assert.Equal(3, result.CreatedSchedules);
        Assert.Equal(0, result.CyclesRequiringGsdCoverageReview);
        Assert.Equal(["2026-05", "2026-08", "2026-11"],
            (await application.ReadSchedulesAsync(asset.Id)).Select(schedule => schedule.PmCycle));
    }

    [Fact]
    public async Task January_recovery_never_backfills_the_previous_calendar_year()
    {
        var now = new DateTimeOffset(2027, 1, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var asset = await application.SeedAssetAsync(
            "GEN-COVERAGE-YEAR-BOUNDARY",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));

        using var response = await client.PostAsJsonAsync(
            "/api/v1/schedules/generate",
            new { year = 2027 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["2027-02", "2027-05", "2027-08", "2027-11"],
            (await application.ReadSchedulesAsync(asset.Id)).Select(schedule => schedule.PmCycle));
        Assert.DoesNotContain(
            await application.ReadSchedulesAsync(asset.Id),
            schedule => schedule.PmCycle.StartsWith("2026-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generation_uses_each_cpmp_frequency_and_preserves_ineligible_or_existing_records()
    {
        var now = new DateTimeOffset(2026, 1, 1, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(
            AuthRoleCatalog.Gsd,
            now,
            schedulingEffectiveDate: "2026-01-01");
        using var client = application.CreateClient();

        var fireExtinguisher = await application.SeedAssetAsync(
            "GEN-CPMP-FE", "fire-extinguisher", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        var fireAlarm = await application.SeedAssetAsync(
            "GEN-CPMP-FA", "fire-alarm", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        var emergencyLight = await application.SeedAssetAsync(
            "GEN-CPMP-EL", "emergency-light", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        var waterStation = await application.SeedAssetAsync(
            "GEN-CPMP-WDS", "water-drinking-station", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        var recentAsset = await application.SeedAssetAsync(
            "GEN-CPMP-RECENT", "fire-extinguisher", new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.FromHours(8)));
        var noDepartment = await application.SeedAssetAsync(
            "GEN-CPMP-NODEPT", "fire-extinguisher", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)), department: null);
        var inactive = await application.SeedAssetAsync(
            "GEN-CPMP-INACTIVE", "fire-extinguisher", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)), status: "Inactive");
        var retired = await application.SeedAssetAsync(
            "GEN-CPMP-RETIRED", "fire-extinguisher", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)), status: "Retired");
        var registeredLater = await application.SeedAssetAsync(
            "GEN-CPMP-LATER", "fire-extinguisher", new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));

        var workerId = Guid.NewGuid();
        var supervisorId = Guid.NewGuid();
        var completedAt = new DateTimeOffset(2025, 12, 31, 12, 0, 0, TimeSpan.FromHours(8));
        await application.SeedScheduleAsync(
            fireExtinguisher.Id, "2026-02", "Completed", workerId, supervisorId, completedAt);
        await application.SeedScheduleAsync(
            fireExtinguisher.Id, "2026-05", "Cancelled", workerId, supervisorId, completedAt: null);
        var existingSchedulesBefore = await application.ReadScheduleSnapshotsAsync(fireExtinguisher.Id);

        using var response = await client.PostAsJsonAsync("/api/v1/schedules/generate", new { year = 2026 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GenerationResult>();
        Assert.NotNull(result);
        Assert.Equal(5, result.EligibleAssets);
        Assert.Equal(2, result.ExistingSchedules);
        Assert.Equal(12, result.CreatedSchedules);

        var feCycles = await application.ReadSchedulesAsync(fireExtinguisher.Id);
        Assert.Equal(["2026-02", "2026-05", "2026-08", "2026-11"], feCycles.Select(schedule => schedule.PmCycle));
        Assert.Equal("Completed", feCycles[0].Status);
        Assert.Equal("Cancelled", feCycles[1].Status);
        Assert.All(feCycles.Skip(2), schedule =>
        {
            Assert.Equal("Due", schedule.Status);
            Assert.Equal("Quarter", schedule.PeriodType);
            Assert.Equal($"Q{((int.Parse(schedule.PmCycle[^2..]) - 1) / 3) + 1}", schedule.Quarter);
            Assert.Null(schedule.AssignedToUserId);
        });

        foreach (var asset in new[] { fireAlarm, emergencyLight })
        {
            var schedules = await application.ReadSchedulesAsync(asset.Id);
            Assert.Equal(["2026-06", "2026-12"], schedules.Select(schedule => schedule.PmCycle));
            Assert.All(schedules, schedule =>
            {
                Assert.Equal("Semester", schedule.PeriodType);
                Assert.Null(schedule.Quarter);
                Assert.Null(schedule.Semester);
                Assert.Null(schedule.AcademicYear);
                Assert.Equal("Due", schedule.Status);
            });
            Assert.Equal(new DateTimeOffset(2026, 6, 30, 23, 59, 59, 999, TimeSpan.FromHours(8)).AddTicks(9999), schedules[0].ScheduleDate);
            Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, 999, TimeSpan.FromHours(8)).AddTicks(9999), schedules[1].ScheduleDate);
        }

        var wdsCycles = await application.ReadSchedulesAsync(waterStation.Id);
        Assert.Equal(["2026-02", "2026-05", "2026-08", "2026-11"], wdsCycles.Select(schedule => schedule.PmCycle));
        Assert.Equal(["Q1", "Q2", "Q3", "Q4"], wdsCycles.Select(schedule => schedule.Quarter));
        Assert.All(wdsCycles, schedule => Assert.Equal("Quarter", schedule.PeriodType));

        Assert.Equal(["2026-08", "2026-11"],
            (await application.ReadSchedulesAsync(recentAsset.Id)).Select(schedule => schedule.PmCycle));
        Assert.Empty(await application.ReadSchedulesAsync(noDepartment.Id));
        Assert.Empty(await application.ReadSchedulesAsync(inactive.Id));
        Assert.Empty(await application.ReadSchedulesAsync(retired.Id));
        Assert.Empty(await application.ReadSchedulesAsync(registeredLater.Id));
        var existingScheduleIds = existingSchedulesBefore.Select(schedule => schedule.Id).ToHashSet();
        var existingSchedulesAfter = (await application.ReadScheduleSnapshotsAsync(fireExtinguisher.Id))
            .Where(schedule => existingScheduleIds.Contains(schedule.Id))
            .ToList();
        Assert.Equal(existingSchedulesBefore, existingSchedulesAfter);

        var historicalBefore = await application.ReadScheduleSnapshotsAsync(fireExtinguisher.Id);
        using var historicalRequest = await client.PostAsJsonAsync("/api/v1/schedules/generate", new { year = 2024 });
        Assert.Equal(HttpStatusCode.BadRequest, historicalRequest.StatusCode);
        var leapYearSchedules = await application.ReadSchedulesAsync(fireExtinguisher.Id);
        Assert.DoesNotContain(leapYearSchedules, schedule => schedule.PmCycle.StartsWith("2024-", StringComparison.Ordinal));
        Assert.Equal(historicalBefore, await application.ReadScheduleSnapshotsAsync(fireExtinguisher.Id));
    }

    [Fact]
    public async Task Generation_route_is_gsd_only_and_rejects_future_years()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        foreach (var role in new[] { AuthRoleCatalog.Supervisor, AuthRoleCatalog.Inspector, AuthRoleCatalog.Admin })
        {
            await using var restricted = new TestApplicationFactory(role, now);
            using var restrictedClient = restricted.CreateClient();
            using var forbidden = await restrictedClient.PostAsJsonAsync(
                "/api/v1/schedules/generate",
                new { year = 2026 });
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        await using var gsd = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var gsdClient = gsd.CreateClient();
        using var future = await gsdClient.PostAsJsonAsync("/api/v1/schedules/generate", new { year = 2027 });
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        using var historical = await gsdClient.PostAsJsonAsync("/api/v1/schedules/generate", new { year = 2025 });
        Assert.Equal(HttpStatusCode.BadRequest, historical.StatusCode);
    }

    [Theory]
    [InlineData("supervisor", "Due", true, false, null, null, "BatchAssigned")]
    [InlineData("inspector", "Due", false, true, null, null, "BatchAssigned")]
    [InlineData("inspection-started", "Ongoing", false, false, null, null, "WorkInProgress")]
    [InlineData("inspection-recorded", "Due", false, false, null, null, "InspectionStarted")]
    [InlineData("completed", "Completed", false, false, null, null, "CycleCompleted")]
    [InlineData("cancelled", "Cancelled", false, false, null, null, "CycleCancelled")]
    [InlineData("submitted", "Completed", false, false, null, "Submitted", "FormSubmitted")]
    [InlineData("acknowledged", "Completed", false, false, null, "Acknowledged", "FormAcknowledged")]
    public async Task Registration_defers_locked_cycle_and_recovery_does_not_recreate_it(
        string stage,
        string status,
        bool assignedSupervisor,
        bool assignedInspector,
        DateTimeOffset? completedAt,
        string? formStatus,
        string expectedReason)
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var existingAsset = await application.SeedAssetAsync(
            $"GEN-LOCK-{stage}",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        Guid? supervisorId = assignedSupervisor ? Guid.NewGuid() : null;
        Guid? workerId = assignedInspector ? Guid.NewGuid() : null;
        var completion = status == "Completed"
            ? completedAt ?? now
            : completedAt;
        var scheduleId = await application.SeedScheduleAsync(
            existingAsset.Id,
            "2026-08",
            status,
            workerId,
            supervisorId,
            completion);

        if (stage is "inspection-started" or "inspection-recorded")
        {
            await application.SeedInspectionAsync(existingAsset.Id, scheduleId, now);
        }

        if (formStatus is not null)
        {
            await application.SeedFormAsync("fire-extinguisher", "GSD", "2026-08", formStatus, now);
        }

        using var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = $"GEN-NEW-{stage}",
            assetCategory = "fire-extinguisher",
            department = "GSD",
            building = "Main",
            location = "Lobby"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var asset = await response.Content.ReadFromJsonAsync<AssetRef>();
        Assert.NotNull(asset);

        var schedules = await application.ReadSchedulesAsync(asset.Id);
        Assert.DoesNotContain(schedules, schedule => schedule.PmCycle == "2026-08");
        Assert.Contains(schedules, schedule => schedule.PmCycle == "2026-11");
        var deferred = await application.ReadDeferralsAsync(asset.Id);
        var single = Assert.Single(deferred);
        Assert.Equal("2026-08", single.PmCycle);
        Assert.Equal(expectedReason, single.ReasonCode);
        Assert.Equal("2026-11", single.NextEligiblePmCycle);

        using var reviewResponse = await client.GetAsync(
            "/api/v1/schedules/enrollment-deferrals?page=2147483647");
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        using var reviewPage = await JsonDocument.ParseAsync(await reviewResponse.Content.ReadAsStreamAsync());
        Assert.Equal(1, reviewPage.RootElement.GetProperty("page").GetInt32());
        var reviewItem = Assert.Single(reviewPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("2026-08", reviewItem.GetProperty("deferredPmCycle").GetString());
        Assert.Equal(expectedReason, reviewItem.GetProperty("reasonCode").GetString());
        Assert.Equal("Needs review", reviewItem.GetProperty("status").GetString());

        var before = await application.ReadScheduleSnapshotsAsync(asset.Id);
        using var recovery = await client.PostAsJsonAsync("/api/v1/schedules/generate", new { year = 2026 });
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
        Assert.Equal(before, await application.ReadScheduleSnapshotsAsync(asset.Id));
        Assert.Single(await application.ReadDeferralsAsync(asset.Id));

        var unchangedExisting = await application.ReadSchedulesAsync(existingAsset.Id);
        Assert.Equal(status, Assert.Single(unchangedExisting, schedule => schedule.PmCycle == "2026-08").Status);
    }

    [Theory]
    [InlineData(AuthRoleCatalog.Supervisor)]
    [InlineData(AuthRoleCatalog.Inspector)]
    [InlineData(AuthRoleCatalog.Admin)]
    public async Task Enrollment_deferral_review_is_gsd_only(string role)
    {
        await using var application = new TestApplicationFactory(
            role,
            new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/v1/schedules/enrollment-deferrals");
        using var reviewResponse = await client.PostAsJsonAsync(
            $"/api/v1/schedules/enrollment-deferrals/{Guid.NewGuid()}/2026-08/review",
            new { note = "review" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reviewResponse.StatusCode);
    }

    [Fact]
    public async Task Enrollment_deferral_review_requires_authentication()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(null, now);
        using var client = application.CreateClient();

        using var listResponse = await client.GetAsync("/api/v1/schedules/enrollment-deferrals");
        using var reviewResponse = await client.PostAsJsonAsync(
            $"/api/v1/schedules/enrollment-deferrals/{Guid.NewGuid()}/2026-08/review",
            new { note = "review" });

        Assert.Equal(HttpStatusCode.Unauthorized, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reviewResponse.StatusCode);
    }

    [Fact]
    public async Task Gsd_review_is_audited_filterable_and_does_not_create_or_complete_pm()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var asset = await application.SeedAssetAsync(
            "GEN-DEFERRAL-REVIEW",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            department: "CCMS");
        await application.SeedDeferralAsync(asset.Id, "2026-08", "2026-11", now);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/schedules/enrollment-deferrals/{asset.Id}/2026-08/review",
            new { note = "  Reviewed with GSD.  " });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await application.ReadSchedulesAsync(asset.Id));
        var reviewed = Assert.Single(await application.ReadDeferralsAsync(asset.Id));
        Assert.Equal("2026-08", reviewed.PmCycle);
        Assert.Equal(now, reviewed.DeferredAt);
        Assert.Equal("2026-11", reviewed.NextEligiblePmCycle);
        Assert.Equal("BatchAssigned", reviewed.ReasonCode);
        Assert.Equal(now, reviewed.ReviewedAt);
        Assert.Equal((Guid?)TestAuthenticationHandler.UserId, reviewed.ReviewedByUserId);
        Assert.Equal("Reviewed with GSD.", reviewed.ReviewNote);

        await using (var context = await application.Services
                     .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                     .CreateDbContextAsync())
        {
            var reviewedAsset = await context.Assets.SingleAsync(item => item.Id == asset.Id);
            Assert.Equal(AssetStatusCatalog.Active, reviewedAsset.Status);
            Assert.Empty(await context.InspectionRecords
                .Where(item => item.AssetId == asset.Id)
                .ToListAsync());
        }

        using var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/schedules/enrollment-deferrals/{asset.Id}/2026-08/review",
            new { note = "Second review" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Reviewed with GSD.", Assert.Single(await application.ReadDeferralsAsync(asset.Id)).ReviewNote);

        using var filtered = await client.GetAsync(
            "/api/v1/schedules/enrollment-deferrals?status=Reviewed&pmCycle=2026-08&assetCategory=fire-extinguisher&department=Library");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using var page = await JsonDocument.ParseAsync(await filtered.Content.ReadAsStreamAsync());
        Assert.Equal(0, page.RootElement.GetProperty("pendingCount").GetInt32());
        Assert.Equal(0, page.RootElement.GetProperty("reviewedCount").GetInt32());
        Assert.Empty(page.RootElement.GetProperty("items").EnumerateArray());

        using var matching = await client.GetAsync(
            "/api/v1/schedules/enrollment-deferrals?status=Reviewed&pmCycle=2026-08&assetCategory=fire-extinguisher&department=CCMS");
        using var matchingPage = await JsonDocument.ParseAsync(await matching.Content.ReadAsStreamAsync());
        Assert.Equal(0, matchingPage.RootElement.GetProperty("pendingCount").GetInt32());
        Assert.Equal(1, matchingPage.RootElement.GetProperty("reviewedCount").GetInt32());
        var item = Assert.Single(matchingPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Reviewed", item.GetProperty("status").GetString());
        Assert.Equal(now, item.GetProperty("reviewedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Gsd_deferral_review_rejects_notes_over_the_bounded_length()
    {
        var now = new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd, now);
        using var client = application.CreateClient();
        var asset = await application.SeedAssetAsync(
            "GEN-DEFERRAL-NOTE-LENGTH",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        await application.SeedDeferralAsync(asset.Id, "2026-08", "2026-11", now);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/schedules/enrollment-deferrals/{asset.Id}/2026-08/review",
            new { note = new string('x', ScheduleEnrollmentDeferralReviewRequest.MaximumNoteLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(Assert.Single(await application.ReadDeferralsAsync(asset.Id)).ReviewedAt);
    }

    [Fact]
    public async Task Manual_schedule_creation_rejects_past_and_future_years()
    {
        await using var application = new TestApplicationFactory(
            AuthRoleCatalog.Gsd,
            new DateTimeOffset(2026, 8, 15, 4, 0, 0, TimeSpan.Zero));
        using var client = application.CreateClient();
        var asset = await application.SeedAssetAsync(
            "GEN-FE-YEAR-GUARD",
            "fire-extinguisher",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));

        foreach (var cycle in new[] { "2025-11", "2027-02" })
        {
            using var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
            {
                assetId = asset.Id,
                pmCycle = cycle,
                periodType = "Quarter",
                quarter = cycle.EndsWith("11", StringComparison.Ordinal) ? "Q4" : "Q1"
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var overdue = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle = "2026-05",
            periodType = "Quarter",
            quarter = "Q2"
        });
        Assert.Equal(HttpStatusCode.Created, overdue.StatusCode);
        using var overdueDocument = JsonDocument.Parse(await overdue.Content.ReadAsStringAsync());
        Assert.Equal("Overdue", overdueDocument.RootElement.GetProperty("status").GetString());

        using var due = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle = "2026-11",
            periodType = "Quarter",
            quarter = "Q4"
        });
        Assert.Equal(HttpStatusCode.Created, due.StatusCode);
        using var dueDocument = JsonDocument.Parse(await due.Content.ReadAsStringAsync());
        Assert.Equal("Due", dueDocument.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Worker_retries_failed_startup_check_and_recovers_each_institutional_year_only()
    {
        var databaseName = $"unipm-schedule-worker-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new FailOnceDbContextFactory(options);
        await using (var seedContext = new ApplicationDbContext(options))
        {
            seedContext.Assets.Add(NewAsset(
                "WORKER-FE-001",
                "fire-extinguisher",
                new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(8))));
            await seedContext.SaveChangesAsync();
        }

        var timeProvider = new AdvancingTimeProvider(
            new DateTimeOffset(2026, 12, 30, 15, 0, 0, TimeSpan.Zero),
            advancesRemaining: 2);
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(factory);
        services.AddScoped<PreventiveMaintenanceScheduleGenerationService>();
        using var serviceProvider = services.BuildServiceProvider();
        var worker = new PreventiveMaintenanceScheduleGenerationWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            NullLogger<PreventiveMaintenanceScheduleGenerationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForSchedulesAsync(
                options,
                factory,
                timeProvider,
                expectedCount: 4,
                TimeSpan.FromSeconds(3));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var verify = new ApplicationDbContext(options);
        var schedules = await verify.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .OrderBy(schedule => schedule.PmCycle)
            .ToListAsync();
        Assert.Equal(4, schedules.Count);
        Assert.DoesNotContain(schedules, schedule => schedule.PmCycle.StartsWith("2025-", StringComparison.Ordinal));
        Assert.Equal(4, schedules.Count(schedule => schedule.PmCycle.StartsWith("2027-", StringComparison.Ordinal)));
        Assert.DoesNotContain(schedules, schedule => schedule.PmCycle.StartsWith("2026-", StringComparison.Ordinal));
        Assert.All(schedules.Where(schedule => schedule.PmCycle.StartsWith("2027-", StringComparison.Ordinal)),
            schedule => Assert.Equal("Due", schedule.Status));
        Assert.True(factory.CreateAttempts >= 3);
    }

    private static async Task WaitForSchedulesAsync(
        DbContextOptions<ApplicationDbContext> options,
        FailOnceDbContextFactory factory,
        AdvancingTimeProvider timeProvider,
        int expectedCount,
        TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var actualCount = 0;
        while (stopwatch.Elapsed < timeout)
        {
            await using var context = new ApplicationDbContext(options);
            actualCount = await context.PreventiveMaintenanceSchedules.CountAsync();
            if (actualCount == expectedCount)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"The worker did not create {expectedCount} schedules before the test timeout "
            + $"(found {actualCount}; factory attempts {factory.CreateAttempts}; "
            + $"timer advances {timeProvider.AdvanceCount}; time {timeProvider.CurrentTime:O}).");
    }

    private static Asset NewAsset(
        string assetCode,
        string category,
        DateTimeOffset createdAt,
        string? department = "GSD",
        string status = "Active")
    {
        return new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = assetCode,
            AssetCategory = category,
            Department = department,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    private sealed class TestApplicationFactory(
        string? role,
        DateTimeOffset now,
        string? schedulingEffectiveDate = null) : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"unipm-schedule-generation-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                if (role is not null)
                {
                    services.AddTestAuthentication(role);
                }
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.Configure<ScheduleGenerationOptions>(options =>
                    options.EffectiveDate = schedulingEffectiveDate);
            });
        }

        public async Task<AssetRef> SeedAssetAsync(
            string assetCode,
            string category,
            DateTimeOffset createdAt,
            string? department = "GSD",
            string status = "Active")
        {
            var asset = NewAsset(assetCode, category, createdAt, department, status);
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            context.Assets.Add(asset);
            await context.SaveChangesAsync();
            return new AssetRef(asset.Id);
        }

        public async Task<Guid> SeedScheduleAsync(
            Guid assetId,
            string pmCycle,
            string status,
            Guid? assignedToUserId,
            Guid? assignedSupervisorUserId,
            DateTimeOffset? completedAt)
        {
            PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out var month);
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle),
                PmCycle = pmCycle,
                PeriodType = SchedulePeriodTypeCatalog.Quarter,
                Status = status,
                Quarter = $"Q{((month - 1) / 3) + 1}",
                Year = year,
                AssignedToUserId = assignedToUserId,
                AssignedSupervisorUserId = assignedSupervisorUserId,
                CompletedAt = completedAt,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync();
            return schedule.Id;
        }

        public async Task SeedDeferralAsync(
            Guid assetId,
            string pmCycle,
            string nextEligiblePmCycle,
            DateTimeOffset deferredAt)
        {
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            var asset = await context.Assets.SingleAsync(item => item.Id == assetId);
            context.ScheduleEnrollmentDeferrals.Add(new ScheduleEnrollmentDeferral
            {
                AssetId = assetId,
                PmCycle = pmCycle,
                DepartmentAtDeferral = asset.Department!.Trim().ToUpperInvariant(),
                AssetCategoryAtDeferral = asset.AssetCategory,
                ReasonCode = "BatchAssigned",
                DeferredAt = deferredAt,
                NextEligiblePmCycle = nextEligiblePmCycle
            });
            await context.SaveChangesAsync();
        }

        public async Task SeedInspectionAsync(Guid assetId, Guid scheduleId, DateTimeOffset now)
        {
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            context.InspectionRecords.Add(new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = scheduleId,
                AssetId = assetId,
                InspectorUserId = Guid.NewGuid(),
                DateInspected = now,
                IsOperational = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }

        public async Task SeedFormAsync(
            string assetCategory,
            string department,
            string pmCycle,
            string status,
            DateTimeOffset now)
        {
            PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out var month);
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            context.PreventiveMaintenanceForms.Add(new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                AssetCategory = assetCategory,
                Department = department,
                PmCycle = pmCycle,
                PeriodType = SchedulePeriodTypeCatalog.Quarter,
                Quarter = $"Q{((month - 1) / 3) + 1}",
                Year = year,
                Status = status,
                CreatedByUserId = Guid.NewGuid(),
                SubmittedByUserId = status == "Draft" ? null : Guid.NewGuid(),
                SubmittedAt = status == "Draft" ? null : now,
                FieldWorkCompletedAt = status == "Draft" ? null : now,
                CreatedAt = now,
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }

        public async Task<List<ScheduleRef>> ReadSchedulesAsync(Guid assetId)
        {
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            return await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Where(schedule => schedule.AssetId == assetId)
                .OrderBy(schedule => schedule.PmCycle)
                .Select(schedule => new ScheduleRef(
                    schedule.PmCycle,
                    schedule.ScheduleDate,
                    schedule.PeriodType,
                    schedule.Status,
                    schedule.Quarter,
                    schedule.Semester,
                    schedule.AcademicYear,
                    schedule.AssignedToUserId,
                    schedule.AssignedSupervisorUserId))
                .ToListAsync();
        }

        public async Task<List<ScheduleSnapshot>> ReadScheduleSnapshotsAsync(Guid assetId)
        {
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            return await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Where(schedule => schedule.AssetId == assetId)
                .OrderBy(schedule => schedule.PmCycle)
                .Select(schedule => new ScheduleSnapshot(
                    schedule.Id,
                    schedule.AssetId,
                    schedule.PmCycle,
                    schedule.ScheduleDate,
                    schedule.PeriodType,
                    schedule.Quarter,
                    schedule.Semester,
                    schedule.AcademicYear,
                    schedule.Year,
                    schedule.Status,
                    schedule.AssignedToUserId,
                    schedule.AssignedSupervisorUserId,
                    schedule.CompletedAt,
                    schedule.CreatedAt,
                    schedule.UpdatedAt))
                .ToListAsync();
        }

        public async Task<List<ScheduleEnrollmentDeferral>> ReadDeferralsAsync(Guid assetId)
        {
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            return await context.ScheduleEnrollmentDeferrals
                .AsNoTracking()
                .Where(deferral => deferral.AssetId == assetId)
                .OrderBy(deferral => deferral.PmCycle)
                .ToListAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private sealed record AssetRef(Guid Id);

    private sealed record ScheduleRef(
        string PmCycle,
        DateTimeOffset ScheduleDate,
        string PeriodType,
        string Status,
        string? Quarter,
        string? Semester,
        string? AcademicYear,
        Guid? AssignedToUserId,
        Guid? AssignedSupervisorUserId);

    private sealed record ScheduleSnapshot(
        Guid Id,
        Guid AssetId,
        string PmCycle,
        DateTimeOffset ScheduleDate,
        string PeriodType,
        string? Quarter,
        string? Semester,
        string? AcademicYear,
        int? Year,
        string Status,
        Guid? AssignedToUserId,
        Guid? AssignedSupervisorUserId,
        DateTimeOffset? CompletedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record GenerationResult(
        int Year,
        int EligibleAssets,
        int ExistingSchedules,
        int CreatedSchedules,
        int DeferredSchedules = 0,
        int CyclesRequiringGsdCoverageReview = 0);

    private sealed class FailOnceDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        private int _failuresRemaining = 1;
        private int _createAttempts;

        public int CreateAttempts => Volatile.Read(ref _createAttempts);

        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _createAttempts);
            if (Interlocked.CompareExchange(ref _failuresRemaining, 0, 1) == 1)
            {
                throw new InvalidOperationException("Synthetic transient context failure.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class AdvancingTimeProvider(DateTimeOffset now, int advancesRemaining) : TimeProvider
    {
        private readonly object _gate = new();
        private DateTimeOffset _now = now;
        private int _advancesRemaining = advancesRemaining;
        private int _advanceCount;

        public int AdvanceCount => Volatile.Read(ref _advanceCount);

        public DateTimeOffset CurrentTime
        {
            get
            {
                lock (_gate)
                {
                    return _now;
                }
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _now;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new AdvancingTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        private bool TryAdvance(TimeSpan interval)
        {
            lock (_gate)
            {
                if (_advancesRemaining == 0)
                {
                    return false;
                }

                _advancesRemaining--;
                _now += interval;
                Interlocked.Increment(ref _advanceCount);
                return true;
            }
        }

        private sealed class AdvancingTimer(
            AdvancingTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private int _generation;
            private int _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (dueTime == Timeout.InfiniteTimeSpan || Volatile.Read(ref _disposed) != 0)
                {
                    Interlocked.Increment(ref _generation);
                    return true;
                }

                var generation = Interlocked.Increment(ref _generation);
                _ = FireAsync(dueTime, generation);
                return true;
            }

            public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            private async Task FireAsync(TimeSpan dueTime, int generation)
            {
                await Task.Delay(5);
                if (Volatile.Read(ref _disposed) == 0
                    && Volatile.Read(ref _generation) == generation
                    && owner.TryAdvance(dueTime))
                {
                    callback(state);
                }
            }
        }
    }
}
