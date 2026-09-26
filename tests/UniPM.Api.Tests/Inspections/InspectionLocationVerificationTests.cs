using System.Net;
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
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class InspectionLocationVerificationTests
{
    [Fact]
    public void Haversine_distance_uses_meters_and_handles_longitude_wraparound()
    {
        var oneDegreeAtEquator = InspectionLocationClassifier.HaversineDistanceMeters(0, 0, 0, 1);
        var acrossDateLine = InspectionLocationClassifier.HaversineDistanceMeters(0, 179.9, 0, -179.9);

        Assert.InRange(oneDegreeAtEquator, 111_194, 111_196);
        Assert.InRange(acrossDateLine, 22_238, 22_240);
    }

    [Fact]
    public void Classifier_includes_accuracy_boundary_and_marks_overlap_uncertain()
    {
        var inside = InspectionLocationClassifier.Classify(0, 0, 100, 0, 0, true, 100);
        var uncertain = InspectionLocationClassifier.Classify(0, 0, 1_000, 0, 0.01, true, 150);
        var outside = InspectionLocationClassifier.Classify(0, 0, 1_000, 0, 0.01, true, 50);
        var unavailableAccuracy = InspectionLocationClassifier.Classify(0, 0, 100, 0, 0, false, null);

        Assert.Equal("Inside", inside.Outcome);
        Assert.Equal("Uncertain", uncertain.Outcome);
        Assert.Equal("Outside", outside.Outcome);
        Assert.Equal("Uncertain", unavailableAccuracy.Outcome);
        Assert.Null(InspectionLocationClassifier.Classify(null, null, null, 0, 0, false, null).DistanceMeters);
        Assert.Equal("NotConfigured", InspectionLocationClassifier.Classify(null, null, null, 0, 0, false, null).Outcome);
    }

    [Theory]
    [InlineData(null, 0d, 100d)]
    [InlineData(0d, null, 100d)]
    [InlineData(0d, 0d, null)]
    [InlineData(-90.01d, 0d, 100d)]
    [InlineData(0d, 180.01d, 100d)]
    [InlineData(0d, 0d, 0d)]
    public void Asset_verification_configuration_rejects_partial_or_out_of_range_values(
        double? latitude,
        double? longitude,
        double? radiusMeters)
    {
        var dto = new CreateAssetDto
        {
            AssetCode = "LOC-001",
            AssetCategory = "fire-extinguisher",
            VerificationLatitude = latitude,
            VerificationLongitude = longitude,
            VerificationRadiusMeters = radiusMeters
        };

        Assert.NotEmpty(dto.Validate());
    }

    [Fact]
    public void Location_configuration_and_attempt_validation_reject_non_finite_values()
    {
        var asset = new CreateAssetDto
        {
            AssetCode = "LOC-NAN-001",
            AssetCategory = "fire-extinguisher",
            VerificationLatitude = double.NaN,
            VerificationLongitude = 0,
            VerificationRadiusMeters = 100
        };
        var attempt = ValidLocationAttemptDto(
            longitude: double.PositiveInfinity,
            accuracyMeters: double.NaN);

        Assert.Contains(nameof(CreateAssetDto.VerificationLatitude), asset.Validate().Keys);
        Assert.Contains(nameof(CreateInspectionLocationAttemptDto.Longitude), attempt.Validate().Keys);
        Assert.Contains(nameof(CreateInspectionLocationAttemptDto.AccuracyMeters), attempt.Validate().Keys);
    }

    [Theory]
    [InlineData(91d, 0d, 0d)]
    [InlineData(0d, -181d, 0d)]
    [InlineData(0d, 0d, -1d)]
    public void Location_attempt_request_rejects_invalid_coordinates_or_accuracy(
        double latitude,
        double longitude,
        double accuracyMeters)
    {
        var dto = ValidLocationAttemptDto(
            latitude: latitude,
            longitude: longitude,
            accuracyMeters: accuracyMeters);

        Assert.NotEmpty(dto.Validate());
    }

    [Fact]
    public async Task Asset_location_configuration_is_gsd_only_and_hidden_from_ordinary_asset_reads()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var asset = await CreateAssetAsync(client, "LOC-READ-001", 14.5, 121.01, 75);
        Assert.True(asset.HasVerificationLocation);

        var ordinaryDetailResponse = await client.GetAsync($"/api/v1/assets/{asset.Id}");
        ordinaryDetailResponse.EnsureSuccessStatusCode();
        using var ordinaryDetail = System.Text.Json.JsonDocument.Parse(
            await ordinaryDetailResponse.Content.ReadAsStringAsync());
        var detailJson = ordinaryDetail.RootElement;
        Assert.True(detailJson.GetProperty("hasVerificationLocation").GetBoolean());
        Assert.False(detailJson.TryGetProperty("verificationLatitude", out _));
        Assert.False(detailJson.TryGetProperty("verificationLongitude", out _));
        Assert.False(detailJson.TryGetProperty("verificationRadiusMeters", out _));

        var initialConfiguration = await client.GetFromJsonAsync<AssetVerificationLocationResponse>(
            $"/api/v1/assets/{asset.Id}/verification-location");
        Assert.Equal(14.5, initialConfiguration?.VerificationLatitude);
        Assert.Equal(121.01, initialConfiguration?.VerificationLongitude);
        Assert.Equal(75, initialConfiguration?.VerificationRadiusMeters);

        var update = await client.PutAsJsonAsync(
            $"/api/v1/assets/{asset.Id}/verification-location",
            new { verificationLatitude = 14.6, verificationLongitude = 121.02, verificationRadiusMeters = 80 });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<AssetVerificationLocationResponse>();
        Assert.NotNull(updated);
        Assert.Equal(14.6, updated.VerificationLatitude);
        Assert.Equal(121.02, updated.VerificationLongitude);
        Assert.Equal(80, updated.VerificationRadiusMeters);

        var list = await client.GetFromJsonAsync<List<AssetResponse>>("/api/v1/assets");
        Assert.True(Assert.Single(list!).HasVerificationLocation);

        var clear = await client.PutAsJsonAsync(
            $"/api/v1/assets/{asset.Id}/verification-location",
            new { verificationLatitude = (double?)null, verificationLongitude = (double?)null, verificationRadiusMeters = (double?)null });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Null((await clear.Content.ReadFromJsonAsync<AssetVerificationLocationResponse>())?.VerificationLatitude);

        var clearedAsset = await client.GetFromJsonAsync<AssetResponse>($"/api/v1/assets/{asset.Id}");
        Assert.False(clearedAsset?.HasVerificationLocation);

        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor);
        using var supervisorClient = supervisorApplication.CreateClient();
        var getForbidden = await supervisorClient.GetAsync(
            $"/api/v1/assets/{asset.Id}/verification-location");
        var forbidden = await supervisorClient.PutAsJsonAsync(
            $"/api/v1/assets/{asset.Id}/verification-location",
            new { verificationLatitude = 14.6, verificationLongitude = 121.02, verificationRadiusMeters = 80 });

        Assert.Equal(HttpStatusCode.Forbidden, getForbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Location_attempt_uses_schedule_access_and_links_once_to_the_matching_inspection()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await application.SeedUserAsync(TestAuthenticationHandler.UserId, "Location Inspector");
        var asset = await CreateAssetAsync(client, "LOC-LINK-001", 0, 0, 100);
        var firstSchedule = await CreateScheduleAsync(client, asset.Id, 10);
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 11);

        var attemptResponse = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{firstSchedule.Id}/location-verification-attempts",
            new
            {
                latitude = 0,
                longitude = 0,
                accuracyMeters = 100,
                hasAccuracy = true,
                devicePositionTimestamp = new DateTimeOffset(2026, 9, 26, 1, 0, 0, TimeSpan.Zero),
                isMocked = true,
                accuracyMode = "Reduced",
                acquisitionDurationMs = 325
            });

        Assert.Equal(HttpStatusCode.OK, attemptResponse.StatusCode);
        Assert.Null(attemptResponse.Headers.Location);
        var attempt = await attemptResponse.Content.ReadFromJsonAsync<InspectionLocationAttemptResponse>();
        Assert.NotNull(attempt);
        Assert.NotEqual(Guid.Empty, attempt.Id);
        Assert.Equal("Inside", attempt.Outcome);
        Assert.Equal(0, attempt.DistanceMeters);
        Assert.Equal(100, attempt.AccuracyMeters);
        Assert.True(attempt.HasAccuracy);
        Assert.True(attempt.IsMocked);
        Assert.Equal("Reduced", attempt.AccuracyMode);
        Assert.Equal(325, attempt.AcquisitionDurationMs);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 1, 0, 0, TimeSpan.Zero), attempt.DevicePositionTimestamp);
        Assert.False(System.Text.Json.JsonDocument.Parse(
            await attemptResponse.Content.ReadAsStringAsync()).RootElement.TryGetProperty("expectedLatitude", out _));
        Assert.InRange(attempt.CapturedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));

        var changedLocation = await client.PutAsJsonAsync(
            $"/api/v1/assets/{asset.Id}/verification-location",
            new { verificationLatitude = 1d, verificationLongitude = 1d, verificationRadiusMeters = 150d });
        Assert.Equal(HttpStatusCode.OK, changedLocation.StatusCode);

        var form = await CreateFormAsync(client, asset.AssetCategory);
        var wrongSchedule = await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, attempt.Id);
        Assert.Equal(HttpStatusCode.Conflict, wrongSchedule.StatusCode);

        var otherInspectorId = Guid.NewGuid();
        await application.SeedUserAsync(otherInspectorId, "Other Inspector");
        var wrongActor = await AddInspectionRowAsync(client, form.Id, firstSchedule.Id, attempt.Id, otherInspectorId);
        Assert.Equal(HttpStatusCode.Conflict, wrongActor.StatusCode);

        var linked = await AddInspectionRowAsync(client, form.Id, firstSchedule.Id, attempt.Id);
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        var reused = await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, attempt.Id);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var inspection = await context.InspectionRecords.SingleAsync(row => row.ScheduleId == firstSchedule.Id);
        Assert.Equal(attempt.Id, inspection.LocationAttemptId);
        var persistedAttempt = await context.InspectionLocationAttempts.SingleAsync(row => row.Id == attempt.Id);
        Assert.Equal(0, persistedAttempt.ExpectedLatitude);
        Assert.Equal(0, persistedAttempt.ExpectedLongitude);
        Assert.Equal(100, persistedAttempt.ExpectedRadiusMeters);
        Assert.Equal(attempt.DevicePositionTimestamp, persistedAttempt.DevicePositionTimestamp);
        Assert.True(persistedAttempt.HasAccuracy);
        Assert.Equal(100, persistedAttempt.AccuracyMeters);
        Assert.True(persistedAttempt.IsMocked);
        Assert.Equal("Reduced", persistedAttempt.AccuracyMode);
        Assert.Equal(325, persistedAttempt.AcquisitionDurationMs);
        Assert.Equal(ScheduleStatusCatalog.Completed,
            (await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == firstSchedule.Id)).Status);
        Assert.Equal(ScheduleStatusCatalog.Due,
            (await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == secondSchedule.Id)).Status);
    }

    [Fact]
    public async Task Inspector_can_capture_only_for_an_assigned_schedule_and_Gsd_can_capture_unassigned_schedules()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var client = application.CreateClient();
        var (assignedScheduleId, otherAssignedScheduleId, _) =
            await application.SeedSchedulesAsync();

        var allowed = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{assignedScheduleId}/location-verification-attempts",
            ValidLocationAttemptDto(latitude: 90, longitude: 180));
        var otherAssignedDenied = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{otherAssignedScheduleId}/location-verification-attempts",
            ValidLocationAttemptDto());
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Null(allowed.Headers.Location);
        var attempt = await allowed.Content.ReadFromJsonAsync<InspectionLocationAttemptResponse>();
        Assert.NotNull(attempt);
        Assert.Equal("NotConfigured", attempt.Outcome);
        Assert.Null(attempt.DistanceMeters);
        Assert.Equal(HttpStatusCode.Forbidden, otherAssignedDenied.StatusCode);

        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var gsdClient = gsdApplication.CreateClient();
        var (_, _, gsdUnassignedScheduleId) =
            await gsdApplication.SeedSchedulesAsync();
        var unassignedAllowed = await gsdClient.PostAsJsonAsync(
            $"/api/v1/schedules/{gsdUnassignedScheduleId}/location-verification-attempts",
            ValidLocationAttemptDto());

        Assert.Equal(HttpStatusCode.OK, unassignedAllowed.StatusCode);
        var unassignedAttempt = await unassignedAllowed.Content
            .ReadFromJsonAsync<InspectionLocationAttemptResponse>();
        Assert.NotNull(unassignedAttempt);
        Assert.Equal("NotConfigured", unassignedAttempt.Outcome);
    }

    [Fact]
    public async Task Location_attempt_accepts_pre_start_and_draft_resume_but_rejects_canceled_or_ineligible_schedules()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await application.SeedUserAsync(TestAuthenticationHandler.UserId, "Location Inspector");
        var asset = await CreateAssetAsync(client, "LOC-STATUS-001", 0, 0, 100);
        var preStartSchedule = await CreateScheduleAsync(client, asset.Id, 12);
        var resumeSchedule = await CreateScheduleAsync(client, asset.Id, 13);
        var canceledSchedule = await CreateScheduleAsync(client, asset.Id, 14);
        var completedSchedule = await CreateScheduleAsync(client, asset.Id, 15);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var preStart = await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == preStartSchedule.Id);
            preStart.ScheduleDate = DateTimeOffset.UtcNow.AddDays(7);
            var canceled = await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == canceledSchedule.Id);
            canceled.Status = ScheduleStatusCatalog.Cancelled;
            var completed = await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == completedSchedule.Id);
            completed.Status = ScheduleStatusCatalog.Completed;
            completed.CompletedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        var preStartAttempt = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{preStartSchedule.Id}/location-verification-attempts",
            ValidLocationAttemptDto());
        Assert.Equal(HttpStatusCode.OK, preStartAttempt.StatusCode);

        var initialAttempt = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{resumeSchedule.Id}/location-verification-attempts",
            ValidLocationAttemptDto());
        Assert.Equal(HttpStatusCode.OK, initialAttempt.StatusCode);
        var attempt = (await initialAttempt.Content.ReadFromJsonAsync<InspectionLocationAttemptResponse>())!;
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var row = await AddInspectionRowAsync(client, form.Id, resumeSchedule.Id, attempt.Id);
        Assert.Equal(HttpStatusCode.Created, row.StatusCode);

        var resumedAttempt = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{resumeSchedule.Id}/location-verification-attempts",
            ValidLocationAttemptDto());
        var canceledAttempt = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{canceledSchedule.Id}/location-verification-attempts",
            ValidLocationAttemptDto());
        var ineligibleAttempt = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{completedSchedule.Id}/location-verification-attempts",
            ValidLocationAttemptDto());

        Assert.Equal(HttpStatusCode.OK, resumedAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, canceledAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ineligibleAttempt.StatusCode);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            Assert.Equal(ScheduleStatusCatalog.Due,
                (await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == preStartSchedule.Id)).Status);
            Assert.Equal(ScheduleStatusCatalog.Completed,
                (await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == resumeSchedule.Id)).Status);
            Assert.Equal(ScheduleStatusCatalog.Cancelled,
                (await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == canceledSchedule.Id)).Status);
            Assert.Equal(ScheduleStatusCatalog.Completed,
                (await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == completedSchedule.Id)).Status);
            Assert.Equal(3, await context.InspectionLocationAttempts.CountAsync());
        }
    }

    [Fact]
    public async Task Location_attempt_persists_precise_and_reduced_accuracy_metadata()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var (preciseScheduleId, reducedScheduleId, _) = await application.SeedSchedulesAsync();
        var deviceTimestamp = new DateTimeOffset(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);

        var preciseResponse = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{preciseScheduleId}/location-verification-attempts",
            ValidLocationAttemptDto(
                devicePositionTimestamp: deviceTimestamp,
                accuracyMode: "Precise",
                acquisitionDurationMs: 120));
        var reducedResponse = await client.PostAsJsonAsync(
            $"/api/v1/schedules/{reducedScheduleId}/location-verification-attempts",
            ValidLocationAttemptDto(
                hasAccuracy: false,
                accuracyMeters: null,
                devicePositionTimestamp: deviceTimestamp,
                isMocked: true,
                accuracyMode: "Reduced",
                acquisitionDurationMs: 480));

        Assert.Equal(HttpStatusCode.OK, preciseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reducedResponse.StatusCode);
        var precise = (await preciseResponse.Content.ReadFromJsonAsync<InspectionLocationAttemptResponse>())!;
        var reduced = (await reducedResponse.Content.ReadFromJsonAsync<InspectionLocationAttemptResponse>())!;
        Assert.Equal("Precise", precise.AccuracyMode);
        Assert.True(precise.HasAccuracy);
        Assert.Equal("Reduced", reduced.AccuracyMode);
        Assert.False(reduced.HasAccuracy);
        Assert.Null(reduced.AccuracyMeters);
        Assert.True(reduced.IsMocked);
        Assert.Equal("NotConfigured", reduced.Outcome);

        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var savedPrecise = await context.InspectionLocationAttempts.SingleAsync(row => row.Id == precise.Id);
        var savedReduced = await context.InspectionLocationAttempts.SingleAsync(row => row.Id == reduced.Id);
        Assert.Equal(deviceTimestamp, savedPrecise.DevicePositionTimestamp);
        Assert.Equal(120, savedPrecise.AcquisitionDurationMs);
        Assert.Equal("Precise", savedPrecise.AccuracyMode);
        Assert.Equal(deviceTimestamp, savedReduced.DevicePositionTimestamp);
        Assert.Equal(480, savedReduced.AcquisitionDurationMs);
        Assert.Equal("Reduced", savedReduced.AccuracyMode);
        Assert.True(savedReduced.IsMocked);
    }

    private static CreateInspectionLocationAttemptDto ValidLocationAttemptDto(
        double latitude = 0,
        double longitude = 0,
        double? accuracyMeters = 0,
        bool hasAccuracy = true,
        DateTimeOffset? devicePositionTimestamp = null,
        bool isMocked = false,
        string accuracyMode = "Precise",
        int acquisitionDurationMs = 0)
        => new()
        {
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracyMeters,
            HasAccuracy = hasAccuracy,
            DevicePositionTimestamp = devicePositionTimestamp,
            IsMocked = isMocked,
            AccuracyMode = accuracyMode,
            AcquisitionDurationMs = acquisitionDurationMs
        };

    private static async Task<AssetResponse> CreateAssetAsync(
        HttpClient client,
        string assetCode,
        double? latitude = null,
        double? longitude = null,
        double? radiusMeters = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode,
            assetCategory = "fire-extinguisher",
            building = "Main",
            department = "GSD",
            location = "Lobby",
            verificationLatitude = latitude,
            verificationLongitude = longitude,
            verificationRadiusMeters = radiusMeters
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetResponse>())!;
    }

    private static async Task<ScheduleResponse> CreateScheduleAsync(HttpClient client, Guid assetId, int day)
    {
        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate = new DateTimeOffset(2026, 1, day, 8, 0, 0, TimeSpan.FromHours(8)),
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScheduleResponse>())!;
    }

    private static async Task<PreventiveMaintenanceFormResponse> CreateFormAsync(HttpClient client, string assetCategory)
    {
        var response = await client.PostAsJsonAsync("/api/v1/preventive-maintenance-forms/", new
        {
            assetCategory,
            building = "Main",
            department = "GSD",
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>())!;
    }

    private static Task<HttpResponseMessage> AddInspectionRowAsync(
        HttpClient client,
        Guid formId,
        Guid scheduleId,
        Guid locationAttemptId,
        Guid? inspectorUserId = null)
        => client.PostAsJsonAsync($"/api/v1/preventive-maintenance-forms/{formId}/inspections", new
        {
            scheduleId,
            inspectorUserId = inspectorUserId ?? TestAuthenticationHandler.UserId,
            dateInspected = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            isOperational = true,
            locationAttemptId
        });

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName;
        private readonly string[] roles;

        public TestApplicationFactory(params string[] roles)
        {
            databaseName = $"unipm-location-{Guid.NewGuid():N}";
            this.roles = roles.Length == 0 ? [AuthRoleCatalog.Gsd] : roles;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task SeedUserAsync(Guid userId, string displayName)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            if (!await context.Users.AnyAsync(user => user.Id == userId))
            {
                context.Users.Add(new ApplicationUser
                {
                    Id = userId,
                    UserName = $"{userId:N}@unipm.local",
                    NormalizedUserName = $"{userId:N}@UNIPM.LOCAL".ToUpperInvariant(),
                    Email = $"{userId:N}@unipm.local",
                    NormalizedEmail = $"{userId:N}@UNIPM.LOCAL".ToUpperInvariant(),
                    EmailConfirmed = true,
                    DisplayName = displayName,
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }
        }

        public async Task<(
            Guid AssignedScheduleId,
            Guid OtherAssignedScheduleId,
            Guid UnassignedScheduleId)> SeedSchedulesAsync()
        {
            await SeedUserAsync(TestAuthenticationHandler.UserId, "Location Inspector");
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "LOC-INSPECTOR-001",
                AssetCategory = "fire-extinguisher",
                Building = "Main",
                Department = "GSD",
                Location = "Lobby",
                Status = "Active"
            };
            var assigned = NewSchedule(asset.Id, TestAuthenticationHandler.UserId);
            var otherAssigned = NewSchedule(asset.Id, Guid.NewGuid());
            var unassigned = NewSchedule(asset.Id, null);
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.AddRange(assigned, otherAssigned, unassigned);
            await context.SaveChangesAsync();
            return (assigned.Id, otherAssigned.Id, unassigned.Id);
        }

        private static PreventiveMaintenanceSchedule NewSchedule(Guid assetId, Guid? assignedToUserId)
            => new()
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                ScheduleDate = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.FromHours(8)),
                PmCycle = "2026-01",
                PeriodType = "Quarter",
                Quarter = "Q1",
                Year = 2026,
                Status = ScheduleStatusCatalog.Due,
                AssignedToUserId = assignedToUserId
            };
    }

    private sealed record AssetResponse(
        Guid Id,
        string AssetCode,
        string AssetCategory,
        string? Building,
        string? Department,
        string? Location,
        string? QrCodeValue,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        bool HasVerificationLocation);

    private sealed record ScheduleResponse(Guid Id);

    private sealed record PreventiveMaintenanceFormResponse(Guid Id, string AssetCategory);

    private sealed record InspectionLocationAttemptResponse(
        Guid Id,
        DateTimeOffset CapturedAt,
        double? AccuracyMeters,
        bool HasAccuracy,
        DateTimeOffset? DevicePositionTimestamp,
        bool IsMocked,
        string AccuracyMode,
        int AcquisitionDurationMs,
        double? DistanceMeters,
        string Outcome);
}
