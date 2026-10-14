using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV2SqlServerTests
{
    private const string AssetCategory = "fire-extinguisher";
    private static readonly DateTimeOffset AsOf = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [SqlServer2019Fact]
    public async Task V2_queries_translate_on_sql_server_and_keep_population_and_access_contracts()
    {
        var baseConnectionString = RequireSqlServer2019Connection();
        await AssertSqlServer2019AndFullTextAsync(baseConnectionString);
        var database = await TestDatabase.CreateAsync(baseConnectionString);
        try
        {
            await using (var context = database.CreateContext())
            {
                await context.Database.MigrateAsync();
                Assert.Equal(150, await database.GetCompatibilityLevelAsync());
                await SeedAsync(context);
            }

            await using var gsdApplication = new AnalyticsApplicationFactory(
                database.ConnectionString,
                AsOf,
                authenticated: true,
                AuthRoleCatalog.Gsd);
            using var gsdClient = gsdApplication.CreateClient();

            using (var interpretation = await gsdClient.PostAsJsonAsync(
                "/api/v1/analytics/pm/v2/interpret",
                new PmAnalyticsV2InterpretRequest(
                    "Show water drinking station on-time compliance for 2025 by department")))
            {
                Assert.Equal(HttpStatusCode.OK, interpretation.StatusCode);
                var interpreted = await interpretation.Content.ReadFromJsonAsync<PmAnalyticsV2InterpretationResponse>();
                Assert.NotNull(interpreted);
                Assert.Equal("Valid", interpreted.Status);
                Assert.Equal(PmAnalyticsV2GroupBy.Department, interpreted.Plan!.GroupBy);
            }

            using var annual = await QueryAsync(
                gsdClient,
                CreatePlan(
                    PmAnalyticsV2Metric.OnTimeCompliance,
                    PmAnalyticsV2Presentation.Percent,
                    new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025)));
            Assert.Equal(HttpStatusCode.OK, annual.StatusCode);
            var annualResult = await annual.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
            Assert.NotNull(annualResult);
            Assert.Equal(80, annualResult.Result!.Numerator);
            Assert.Equal(107, annualResult.Result.Denominator);
            Assert.Equal(74.77m, annualResult.Result.Value);
            Assert.Equal(107, annualResult.Population.EligibleAssetCycleCount);
            Assert.Contains(annualResult.Sources, source => source.FormStatus == "Draft");
            Assert.All(annualResult.Cycles, cycle => Assert.Equal(0, cycle.LegacyDateFallbackCount));

            var leapMonth = await QueryPayloadAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Percent,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Month, Year: 2024, Month: 2)));
            var leapCycle = Assert.Single(leapMonth.Cycles);
            Assert.Equal(PreventiveMaintenanceCycle.DeadlineForCycle("2024-02"), leapCycle.Deadline);
            Assert.Equal(TimeSpan.FromHours(8), leapCycle.Deadline.Offset);

            var legacyFallback = await QueryPayloadAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Percent,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Month, Year: 2023, Month: 2)));
            Assert.Equal(1, Assert.Single(legacyFallback.Cycles).LegacyDateFallbackCount);

            using (var comparisonResponse = await gsdClient.PostAsJsonAsync(
                "/api/v1/analytics/pm/v2/query",
                new PmAnalyticsV2QueryRequest(CreatePlan(
                    PmAnalyticsV2Metric.OnTimeCompliance,
                    PmAnalyticsV2Presentation.Percent,
                    period: null,
                    comparison: new PmAnalyticsV2Comparison("2025-02", "2025-05")))))
            {
                Assert.Equal(HttpStatusCode.OK, comparisonResponse.StatusCode);
                var comparison = await comparisonResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(comparison?.Comparison);
                Assert.Equal(92.59m, comparison.Comparison.Delta);
                Assert.Equal("PercentagePoints", comparison.Comparison.DeltaUnit);
            }

            using (var fractionalResponse = await gsdClient.PostAsJsonAsync(
                "/api/v1/analytics/pm/v2/query",
                new PmAnalyticsV2QueryRequest(CreatePlan(
                    PmAnalyticsV2Metric.OnTimeCompliance,
                    PmAnalyticsV2Presentation.Percent,
                    period: null,
                    comparison: new PmAnalyticsV2Comparison("2024-02", "2024-05")))))
            {
                Assert.Equal(HttpStatusCode.OK, fractionalResponse.StatusCode);
                var fractional = await fractionalResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(fractional?.Comparison);
                Assert.Equal(33.33m, fractional.Comparison.Delta);
            }

            var pageTwoPlan = CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Records,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025));
            using (var pageTwoResponse = await QueryAsync(
                gsdClient,
                pageTwoPlan,
                page: 2,
                pageSize: 100))
            {
                Assert.Equal(HttpStatusCode.OK, pageTwoResponse.StatusCode);
                var pageTwo = await pageTwoResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(pageTwo);
                Assert.Equal(107, pageTwo.Population.MatchingAssetCycleCount);
                Assert.Equal(2, pageTwo.Population.TotalPages);
                Assert.Equal(2, pageTwo.Page);
                Assert.Equal(7, pageTwo.Sources.Count);
            }

            var distinctAssetsPlan = CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Count,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                countBasis: PmAnalyticsV2CountBasis.DistinctAssets,
                groupBy: PmAnalyticsV2GroupBy.Department);
            using (var distinctResponse = await QueryAsync(gsdClient, distinctAssetsPlan))
            {
                Assert.Equal(HttpStatusCode.OK, distinctResponse.StatusCode);
                var distinct = await distinctResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(distinct);
                Assert.Equal(27, distinct.Result!.Numerator);
                Assert.Equal(27, distinct.Result.Denominator);
                Assert.Equal(2, distinct.Groups.Count);
                Assert.Contains(distinct.Groups, group => group.Value == "DEMO DEPT A" && group.EligibleAssetCycleCount == 56);
                Assert.Contains(distinct.Groups, group => group.Value == "DEMO DEPT B" && group.EligibleAssetCycleCount == 51);
            }

            using (var buildingResponse = await QueryAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Count,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                groupBy: PmAnalyticsV2GroupBy.Building)))
            {
                Assert.Equal(HttpStatusCode.OK, buildingResponse.StatusCode);
                var building = await buildingResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(building);
                Assert.Equal(2, building.Groups.Count);
            }

            using (var cycleGroupResponse = await QueryAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Percent,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                groupBy: PmAnalyticsV2GroupBy.PmCycle)))
            {
                Assert.Equal(HttpStatusCode.OK, cycleGroupResponse.StatusCode);
                var cycleGroups = await cycleGroupResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(cycleGroups);
                Assert.Equal(4, cycleGroups.Groups.Count);
                Assert.All(cycleGroups.Groups, group => Assert.True(group.Result!.IsMeasurable));
            }

            var rankedPlan = CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Percent,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                groupBy: PmAnalyticsV2GroupBy.Department,
                ranking: new PmAnalyticsV2Ranking(1, PmAnalyticsV2Direction.Highest));
            using (var rankedResponse = await QueryAsync(gsdClient, rankedPlan))
            {
                Assert.Equal(HttpStatusCode.OK, rankedResponse.StatusCode);
                var ranked = await rankedResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(ranked);
                Assert.Single(ranked.Groups);
                Assert.Equal("DEMO DEPT A", ranked.Groups[0].Value);
                Assert.Equal(107, ranked.Population.SourceTotalCount);
                Assert.Empty(ranked.RankingExclusions);
            }

            var andFilterPlan = CreatePlan(
                PmAnalyticsV2Metric.CompletedLate,
                PmAnalyticsV2Presentation.Count,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                condition: PmAnalyticsV2Condition.NonOperational,
                timeliness: PmAnalyticsV2Timeliness.Late);
            using (var andFilterResponse = await QueryAsync(gsdClient, andFilterPlan))
            {
                Assert.Equal(HttpStatusCode.OK, andFilterResponse.StatusCode);
                var andFilter = await andFilterResponse.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(andFilter);
                Assert.Equal(2, andFilter.Population.MatchingAssetCycleCount);
                Assert.Equal(2, andFilter.Result!.Numerator);
            }

            var remaining = await QueryPayloadAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.Remaining,
                PmAnalyticsV2Presentation.Count,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2026)));
            Assert.Equal(1, remaining.Population.MatchingAssetCycleCount);
            Assert.Contains(remaining.Sources, source => source.PmCycle == "2026-08" && source.Timeliness == "Remaining");

            var missed = await QueryPayloadAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.Missed,
                PmAnalyticsV2Presentation.Count,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2026)));
            Assert.Equal(2, missed.Population.MatchingAssetCycleCount);
            Assert.All(missed.Sources, source => Assert.Equal("Missed", source.Timeliness));

            var currentAnnual = await QueryPayloadAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.OnTimeCompliance,
                PmAnalyticsV2Presentation.Percent,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2026),
                groupBy: PmAnalyticsV2GroupBy.PmCycle));
            Assert.Null(currentAnnual.Result!.Value);
            Assert.False(currentAnnual.Result.IsMeasurable);
            Assert.Equal(0, currentAnnual.Cycles.Single(cycle => cycle.PmCycle == "2026-11").EligibleAssetCycleCount);
            Assert.True(currentAnnual.Groups.Single(group => group.Value == "2026-02").Result!.IsMeasurable);
            Assert.True(currentAnnual.Groups.Single(group => group.Value == "2026-05").Result!.IsMeasurable);
            Assert.False(currentAnnual.Groups.Single(group => group.Value == "2026-08").Result!.IsMeasurable);

            var emptyDenominator = await QueryPayloadAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.OnTimeCompliance,
                PmAnalyticsV2Presentation.Percent,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                assetCode: "V2-FE-EMPTY"));
            Assert.Equal(0, emptyDenominator.Result!.Denominator);
            Assert.Null(emptyDenominator.Result.Value);
            Assert.False(emptyDenominator.Result.IsMeasurable);

            var exactAsset = await QueryPayloadAsync(gsdClient, CreatePlan(
                PmAnalyticsV2Metric.Progress,
                PmAnalyticsV2Presentation.Count,
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                assetCode: "V2-FE-00"));
            Assert.Equal(4, exactAsset.Population.EligibleAssetCycleCount);

            using (var calculationScope = await QueryAsync(
                gsdClient,
                CreatePlan(
                    PmAnalyticsV2Metric.NonOperational,
                    PmAnalyticsV2Presentation.Count,
                    new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025)),
                sourceScope: PmAnalyticsV2SourceScope.Calculation))
            {
                Assert.Equal(HttpStatusCode.OK, calculationScope.StatusCode);
                var scope = await calculationScope.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
                Assert.NotNull(scope);
                Assert.Equal(3, scope.Population.MatchingAssetCycleCount);
                Assert.Equal(107, scope.Population.SourceTotalCount);
                Assert.Equal(PmAnalyticsV2SourceScope.Calculation, scope.SourceScope);
            }

            using (var unknownContext = await QueryAsync(
                gsdClient,
                CreatePlan(
                    PmAnalyticsV2Metric.Progress,
                    PmAnalyticsV2Presentation.Count,
                    new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
                    department: "UNKNOWN DEPARTMENT")))
            {
                Assert.Equal(HttpStatusCode.BadRequest, unknownContext.StatusCode);
            }

            using (var invalidPlan = await QueryAsync(
                gsdClient,
                CreatePlan(
                    PmAnalyticsV2Metric.Progress,
                    PmAnalyticsV2Presentation.Count,
                    new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025)) with { SchemaVersion = 1 }))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalidPlan.StatusCode);
            }

            await using var supervisorApplication = new AnalyticsApplicationFactory(
                database.ConnectionString,
                AsOf,
                authenticated: true,
                AuthRoleCatalog.Supervisor);
            using var supervisorClient = supervisorApplication.CreateClient();
            using var forbidden = await QueryAsync(supervisorClient, pageTwoPlan);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

            await using var anonymousApplication = new AnalyticsApplicationFactory(
                database.ConnectionString,
                AsOf,
                authenticated: false);
            using var anonymousClient = anonymousApplication.CreateClient();
            using var unauthorized = await QueryAsync(anonymousClient, pageTwoPlan);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        var userId = TestAuthenticationHandler.UserId;
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "pm-analytics-v2-test@unipm.local",
            NormalizedUserName = "PM-ANALYTICS-V2-TEST@UNIPM.LOCAL",
            Email = "pm-analytics-v2-test@unipm.local",
            NormalizedEmail = "PM-ANALYTICS-V2-TEST@UNIPM.LOCAL",
            EmailConfirmed = true,
            DisplayName = "Synthetic PM Analytics V2 Inspector",
            IsActive = true
        });

        var assets = Enumerable.Range(0, 27)
            .Select(index => new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"V2-FE-{index:D2}",
                AssetCategory = AssetCategory,
                Department = index < 14 ? "Demo Dept A" : "Demo Dept B",
                Building = index % 2 == 0 ? "North Building" : "South Building",
                Location = "Synthetic analytics test area",
                Status = AssetStatusCatalog.Active,
                CreatedAt = AsOf.AddYears(-2),
                UpdatedAt = AsOf
            })
            .ToArray();
        var schedules = new List<PreventiveMaintenanceSchedule>();
        var inspections = new List<InspectionRecord>();
        var months = new[] { 2, 5, 8, 11 };
        for (var assetIndex = 0; assetIndex < assets.Length; assetIndex++)
        {
            foreach (var month in months)
            {
                var cycle = $"2025-{month:D2}";
                var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(cycle);
                var isCancelled = month == 11 && assetIndex == 26;
                var completedOnTime = month switch
                {
                    2 => assetIndex == 0,
                    5 => assetIndex < 26,
                    _ => true
                };
                var schedule = new PreventiveMaintenanceSchedule
                {
                    Id = Guid.NewGuid(),
                    AssetId = assets[assetIndex].Id,
                    ScheduleDate = new DateTimeOffset(2025, month, 1, 0, 0, 0, TimeSpan.FromHours(8)),
                    PmCycle = cycle,
                    PeriodType = "Quarter",
                    Quarter = $"Q{((month - 1) / 3) + 1}",
                    Year = 2025,
                    Status = isCancelled
                        ? ScheduleStatusCatalog.Cancelled
                        : ScheduleStatusCatalog.Completed,
                    CreatedAt = AsOf.AddYears(-1),
                    UpdatedAt = AsOf
                };
                schedules.Add(schedule);
                if (isCancelled)
                {
                    continue;
                }

                var isOperational = !((month == 2 && assetIndex is 1 or 2)
                    || (month == 8 && assetIndex == 0));
                var completedAt = completedOnTime ? deadline.AddDays(-1) : deadline.AddDays(1);
                var inspection = new InspectionRecord
                {
                    Id = Guid.NewGuid(),
                    ScheduleId = schedule.Id,
                    AssetId = assets[assetIndex].Id,
                    InspectorUserId = userId,
                    DateInspected = completedAt,
                    CompletedAt = completedAt,
                    DateAccomplished = null,
                    IsOperational = isOperational,
                    CreatedAt = completedAt,
                    UpdatedAt = AsOf
                };
                inspections.Add(inspection);
                if (month == 2 && assetIndex == 0)
                {
                    var form = new PreventiveMaintenanceForm
                    {
                        Id = Guid.NewGuid(),
                        AssetCategory = AssetCategory,
                        Building = assets[assetIndex].Building,
                        Department = assets[assetIndex].Department,
                        PmCycle = cycle,
                        PeriodType = "Quarter",
                        Quarter = "Q1",
                        Year = 2025,
                        Status = PreventiveMaintenanceFormStatusCatalog.Draft,
                        CreatedByUserId = userId,
                        CreatedAt = completedAt,
                        UpdatedAt = AsOf
                    };
                    inspection.PreventiveMaintenanceFormId = form.Id;
                    context.PreventiveMaintenanceForms.Add(form);
                }
            }
        }

        foreach (var (month, status) in new[]
        {
            (2, ScheduleStatusCatalog.Due),
            (5, ScheduleStatusCatalog.Due),
            (8, ScheduleStatusCatalog.Due)
        })
        {
            schedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = assets[0].Id,
                ScheduleDate = new DateTimeOffset(2026, month, 1, 0, 0, 0, TimeSpan.FromHours(8)),
                PmCycle = $"2026-{month:D2}",
                PeriodType = "Quarter",
                Quarter = $"Q{((month - 1) / 3) + 1}",
                Year = 2026,
                Status = status,
                CreatedAt = AsOf,
                UpdatedAt = AsOf
            });
        }

        var legacyAsset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "V2-FE-LEAP",
            AssetCategory = AssetCategory,
            Department = "Leap Test Department",
            Building = "Leap Test Building",
            Location = "Synthetic analytics test area",
            Status = AssetStatusCatalog.Active,
            CreatedAt = AsOf.AddYears(-2),
            UpdatedAt = AsOf
        };
        var legacySchedule = new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = legacyAsset.Id,
            ScheduleDate = new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            PmCycle = string.Empty,
            PeriodType = "Quarter",
            Quarter = "Q1",
            Year = 2023,
            Status = ScheduleStatusCatalog.Completed,
            CreatedAt = AsOf.AddYears(-2),
            UpdatedAt = AsOf
        };
        var legacyDeadline = PreventiveMaintenanceCycle.DeadlineForCycle("2023-02");
        var legacyCompletedAt = legacyDeadline.AddDays(-1);
        context.Assets.Add(legacyAsset);
        context.PreventiveMaintenanceSchedules.Add(legacySchedule);
        context.InspectionRecords.Add(new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = legacySchedule.Id,
            AssetId = legacyAsset.Id,
            InspectorUserId = userId,
            DateInspected = legacyCompletedAt,
            CompletedAt = legacyCompletedAt,
            DateAccomplished = null,
            IsOperational = true,
            CreatedAt = legacyCompletedAt,
            UpdatedAt = AsOf
        });

        var emptyAsset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "V2-FE-EMPTY",
            AssetCategory = AssetCategory,
            Department = "Empty Test Department",
            Building = "Empty Test Building",
            Location = "Synthetic analytics test area",
            Status = AssetStatusCatalog.Active,
            CreatedAt = AsOf.AddYears(-2),
            UpdatedAt = AsOf
        };
        context.Assets.Add(emptyAsset);

        foreach (var index in Enumerable.Range(0, 3))
        {
            var fractionalAsset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"V2-FRACT-{index:D2}",
                AssetCategory = AssetCategory,
                Department = "Fraction Test Department",
                Building = "Fraction Test Building",
                Location = "Synthetic analytics test area",
                Status = AssetStatusCatalog.Active,
                CreatedAt = AsOf.AddYears(-2),
                UpdatedAt = AsOf
            };
            context.Assets.Add(fractionalAsset);
            foreach (var month in new[] { 2, 5 })
            {
                var cycle = $"2024-{month:D2}";
                var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(cycle);
                var completedOnTime = month == 2 ? index == 0 : index < 2;
                var completedAt = completedOnTime ? deadline.AddDays(-1) : deadline.AddDays(1);
                var schedule = new PreventiveMaintenanceSchedule
                {
                    Id = Guid.NewGuid(),
                    AssetId = fractionalAsset.Id,
                    ScheduleDate = new DateTimeOffset(2024, month, 1, 0, 0, 0, TimeSpan.FromHours(8)),
                    PmCycle = cycle,
                    PeriodType = "Quarter",
                    Quarter = month == 2 ? "Q1" : "Q2",
                    Year = 2024,
                    Status = ScheduleStatusCatalog.Completed,
                    CreatedAt = AsOf.AddYears(-2),
                    UpdatedAt = AsOf
                };
                schedules.Add(schedule);
                inspections.Add(new InspectionRecord
                {
                    Id = Guid.NewGuid(),
                    ScheduleId = schedule.Id,
                    AssetId = fractionalAsset.Id,
                    InspectorUserId = userId,
                    DateInspected = completedAt,
                    CompletedAt = completedAt,
                    DateAccomplished = null,
                    IsOperational = true,
                    CreatedAt = completedAt,
                    UpdatedAt = AsOf
                });
            }
        }

        context.Assets.AddRange(assets);
        context.PreventiveMaintenanceSchedules.AddRange(schedules);
        context.InspectionRecords.AddRange(inspections);
        await context.SaveChangesAsync();
    }

    private static PmAnalyticsV2Plan CreatePlan(
        PmAnalyticsV2Metric metric,
        PmAnalyticsV2Presentation presentation,
        PmAnalyticsV2Period? period,
        PmAnalyticsV2Comparison? comparison = null,
        PmAnalyticsV2CountBasis countBasis = PmAnalyticsV2CountBasis.AssetCycles,
        string? department = null,
        string? building = null,
        string? assetCode = null,
        PmAnalyticsV2Condition? condition = null,
        PmAnalyticsV2Timeliness? timeliness = null,
        PmAnalyticsV2GroupBy groupBy = PmAnalyticsV2GroupBy.None,
        PmAnalyticsV2Ranking? ranking = null) =>
        new(
            2,
            metric,
            AssetCategory,
            period,
            comparison,
            presentation,
            countBasis,
            department,
            building,
            assetCode,
            condition,
            timeliness,
            groupBy,
            ranking);

    private static Task<HttpResponseMessage> QueryAsync(
        HttpClient client,
        PmAnalyticsV2Plan plan,
        PmAnalyticsV2SourceScope sourceScope = PmAnalyticsV2SourceScope.Matching,
        int page = 1,
        int pageSize = 25) =>
        client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/query",
            new PmAnalyticsV2QueryRequest(plan, sourceScope, page, pageSize));

    private static async Task<PmAnalyticsV2QueryResponse> QueryPayloadAsync(
        HttpClient client,
        PmAnalyticsV2Plan plan)
    {
        using var response = await QueryAsync(client, plan);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private static string RequireSqlServer2019Connection() =>
        Environment.GetEnvironmentVariable("UNIPM_SQLSERVER2019_TEST_CONNECTION")!;

    private static async Task AssertSqlServer2019AndFullTextAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion')), CONVERT(int, SERVERPROPERTY('IsFullTextInstalled'));";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(15, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    private sealed class AnalyticsApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly DateTimeOffset now;
        private readonly bool authenticated;
        private readonly string[] roles;

        public AnalyticsApplicationFactory(
            string connectionString,
            DateTimeOffset now,
            bool authenticated,
            params string[] roles)
        {
            this.connectionString = connectionString;
            this.now = now;
            this.authenticated = authenticated;
            this.roles = roles;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PreventiveMaintenanceScheduleGeneration:WorkerEnabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                if (authenticated)
                {
                    services.AddTestAuthentication(roles);
                }

                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseUniPmSqlServer(connectionString));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset utcNow = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string databaseName;

        private TestDatabase(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            this.databaseName = databaseName;
        }

        public string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync(string baseConnectionString)
        {
            var databaseName = $"UniPMPmAnalyticsV2_{Guid.NewGuid():N}";
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = databaseName
            };
            var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "master"
            };
            var created = false;
            try
            {
                await using var connection = new SqlConnection(masterBuilder.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}];";
                await command.ExecuteNonQueryAsync();
                created = true;
                command.CommandText = $"ALTER DATABASE [{databaseName}] SET COMPATIBILITY_LEVEL = 150;";
                await command.ExecuteNonQueryAsync();
                return new TestDatabase(databaseBuilder.ConnectionString, databaseName);
            }
            catch (Exception createError)
            {
                if (created)
                {
                    try
                    {
                        await using var cleanupConnection = new SqlConnection(masterBuilder.ConnectionString);
                        await cleanupConnection.OpenAsync();
                        await using var cleanupCommand = cleanupConnection.CreateCommand();
                        cleanupCommand.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
                        await cleanupCommand.ExecuteNonQueryAsync();
                    }
                    catch (Exception cleanupError)
                    {
                        throw new AggregateException("Test database creation failed and its owned database cleanup also failed.", createError, cleanupError);
                    }
                }

                throw;
            }
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseUniPmSqlServer(ConnectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task<int> GetCompatibilityLevelAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME();";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async ValueTask DisposeAsync()
        {
            using (var pooledConnection = new SqlConnection(ConnectionString))
            {
                SqlConnection.ClearPool(pooledConnection);
            }

            var masterBuilder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            };
            await using var connection = new SqlConnection(masterBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
            await command.ExecuteNonQueryAsync();
        }
    }
}
