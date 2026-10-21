using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Seeding;

public sealed class GsdValidationDatasetSeederTests
{
    [Fact]
    public async Task User_bootstrap_and_dataset_seed_create_expected_guarded_validation_fixture()
    {
        var configuration = CreateSeedConfiguration();
        var environment = new DevelopmentHostEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"unipm-gsd-validation-{Guid.NewGuid():N}"));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<GsdValidationUserSeeder>();
        services.AddScoped<GsdValidationDatasetSeeder>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userSeeder = scope.ServiceProvider.GetRequiredService<GsdValidationUserSeeder>();
        var seeder = scope.ServiceProvider.GetRequiredService<GsdValidationDatasetSeeder>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userResult = await userSeeder.SeedAsync();
        Assert.Equal(new GsdValidationUserSeedResult(5, 4), userResult);
        var passwords = StagingDemoInitialization.Passwords(configuration);
        foreach (var roleName in StagingDemoInitialization.Roles)
        {
            var user = Assert.IsType<ApplicationUser>(
                await userManager.FindByEmailAsync(StagingDemoInitialization.Email(roleName)));
            Assert.True(user.IsActive);
            Assert.True(await userManager.IsInRoleAsync(user, roleName));
            Assert.True(await userManager.CheckPasswordAsync(user, passwords[roleName]));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => userSeeder.SeedAsync());
        var result = await seeder.SeedAsync();

        Assert.Equal(248, result.Assets);
        Assert.Equal(2_232, result.Schedules);
        Assert.Equal(1_932, result.Inspections);
        Assert.Equal(220, result.Forms);
        Assert.Equal(110, result.Acknowledgements);
        Assert.Equal(1_449, result.OnTimeInspections);
        Assert.Equal(483, result.LateInspections);
        Assert.Equal(387, result.NonOperationalInspections);
        Assert.Equal(
            result.AcknowledgedNonOperationalInspections,
            result.AwaitingWmsReferral + result.ReferredToWms);
        Assert.Equal(103, result.ReferredToWms);
        Assert.Equal(103, result.WmsReferrals);
        Assert.Equal(result.WmsReferrals, result.WmsReferralAudits);

        Assert.Equal(62, await context.Assets.CountAsync(asset => asset.AssetCategory == AssetCategoryCatalog.FireExtinguisher));
        Assert.Equal(62, await context.Assets.CountAsync(asset => asset.AssetCategory == AssetCategoryCatalog.FireAlarm));
        Assert.Equal(62, await context.Assets.CountAsync(asset => asset.AssetCategory == AssetCategoryCatalog.EmergencyLight));
        Assert.Equal(62, await context.Assets.CountAsync(asset => asset.AssetCategory == AssetCategoryCatalog.WaterDrinkingStation));
        Assert.Equal(110, await context.PreventiveMaintenanceForms.CountAsync(form => form.Status == "Submitted"));
        Assert.Equal(110, await context.PreventiveMaintenanceForms.CountAsync(form => form.Status == "Acknowledged"));
        Assert.Equal(300, await context.PreventiveMaintenanceSchedules.CountAsync(schedule => schedule.Status == ScheduleStatusCatalog.Due));

        var assets = await context.Assets.AsNoTracking().ToListAsync();
        var categoryPrefixes = new[]
        {
            (AssetCategoryCatalog.FireExtinguisher, "FE"),
            (AssetCategoryCatalog.FireAlarm, "FA"),
            (AssetCategoryCatalog.EmergencyLight, "EL"),
            (AssetCategoryCatalog.WaterDrinkingStation, "WDS")
        };
        var additionalDepartments = new[]
        {
            (Code: "GSD", Name: "General Services Department"),
            (Code: "PROC", Name: "Procurement"),
            (Code: "ICTD", Name: "ICTD")
        };
        foreach (var (category, prefix) in categoryPrefixes)
        foreach (var department in additionalDepartments)
        {
            var officeAssets = assets.Where(asset => asset.AssetCategory == category
                && asset.Department == department.Name).ToArray();
            Assert.Equal(4, officeAssets.Length);
            Assert.All(officeAssets, asset =>
            {
                Assert.Equal("Library Complex", asset.Building);
                Assert.StartsWith($"{prefix}-{department.Code}-", asset.AssetCode, StringComparison.Ordinal);
            });
        }
        Assert.All(assets.Where(asset => asset.Department == "Library"), asset =>
            Assert.Equal("Library Complex", asset.Building));

        await AssertLiveBatchAsync(context, AssetCategoryCatalog.FireExtinguisher, "CCMS", "2026-08");
        await AssertLiveBatchAsync(context, AssetCategoryCatalog.WaterDrinkingStation, "Administration", "2026-08");
        await AssertLiveBatchAsync(context, AssetCategoryCatalog.FireAlarm, "Library", "2026-06");
        await AssertLiveBatchAsync(context, AssetCategoryCatalog.EmergencyLight, "CNAHS", "2026-06");
        Assert.Contains(assets, asset => asset.AssetCode == "FE-CCMS-001");

        var acknowledgements = await context.PreventiveMaintenanceAcknowledgements.AsNoTracking().ToListAsync();
        Assert.Equal(4, acknowledgements.Select(item => item.SignatoryName).Distinct(StringComparer.Ordinal).Count());
        Assert.All(acknowledgements, acknowledgement =>
        {
            Assert.DoesNotContain("Fictional", acknowledgement.SignatoryName, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Department Head", acknowledgement.SignatoryPosition);
            Assert.NotNull(acknowledgement.SignatureData);
            Assert.Equal("image/png", acknowledgement.SignatureContentType);
            var signatureBytes = Convert.FromBase64String(acknowledgement.SignatureData!);
            Assert.Equal("89504E470D0A1A0A", Convert.ToHexString(signatureBytes[..8]));
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(signatureBytes)),
                acknowledgement.SignatureChecksum);
        });

        var schedules = await context.PreventiveMaintenanceSchedules.AsNoTracking().ToListAsync();
        var forms = await context.PreventiveMaintenanceForms.AsNoTracking().ToListAsync();
        var assetById = assets.ToDictionary(asset => asset.Id);
        Assert.Equal(2_232, schedules.Select(schedule => (schedule.AssetId, schedule.PmCycle)).Distinct().Count());
        Assert.Equal(248, assets.Select(asset => asset.QrCodeValue).Distinct(StringComparer.Ordinal).Count());
        foreach (var year in new[] { 2024, 2025 })
        {
            var historicalSchedules = schedules.Where(schedule => schedule.PmCycle.StartsWith($"{year}-", StringComparison.Ordinal)).ToArray();
            Assert.Equal(744, historicalSchedules.Length);
            Assert.Equal(124, historicalSchedules.Count(schedule => schedule.PmCycle == $"{year}-02"));
            Assert.All(historicalSchedules, schedule =>
            {
                var month = int.Parse(schedule.PmCycle.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture);
                var academicYear = month <= 6 ? $"{year - 1}-{year}" : $"{year}-{year + 1}";
                Assert.Equal(ScheduleStatusCatalog.Completed, schedule.Status);
                Assert.NotNull(schedule.CompletedAt);
                Assert.Equal(year, schedule.Year);
                Assert.Equal(academicYear, schedule.AcademicYear);
                Assert.Equal(PreventiveMaintenanceCycle.DeadlineForCycle(schedule.PmCycle), schedule.ScheduleDate);
                Assert.True(assetById[schedule.AssetId].CreatedAt < schedule.ScheduleDate);
            });
        }
        Assert.Equal(248, schedules.Count(schedule => schedule.PmCycle is "2026-11" or "2026-12"));
        Assert.All(forms, form =>
        {
            var year = int.Parse(form.PmCycle.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(year, form.Year);
            Assert.StartsWith($"PM-{year}-", form.FileNumber, StringComparison.Ordinal);
        });

        var referrals = await context.InspectionWmsReferrals.AsNoTracking().ToListAsync();
        var audits = await context.InspectionWmsReferralAudits.AsNoTracking().ToListAsync();
        var inspectionRows = await context.InspectionRecords.AsNoTracking().ToListAsync();
        var acknowledgementByForm = acknowledgements.ToDictionary(item => item.FormId);
        var scheduleById = schedules.ToDictionary(item => item.Id);
        Assert.All(referrals, referral =>
        {
            Assert.Equal(1, referral.Revision);
            var inspection = Assert.Single(inspectionRows, item => item.Id == referral.InspectionId);
            var cycleYear = scheduleById[inspection.ScheduleId].PmCycle[..4];
            Assert.StartsWith($"WMS-{cycleYear}-", referral.ExternalPmNumber, StringComparison.Ordinal);
            Assert.True(referral.RecordedAt > acknowledgementByForm[inspection.PreventiveMaintenanceFormId!.Value].AcknowledgedAt);
            Assert.Contains(audits, audit => audit.InspectionId == referral.InspectionId
                && audit.NewExternalPmNumber == referral.ExternalPmNumber
                && audit.Revision == referral.Revision
                && audit.ChangedAt == referral.RecordedAt);
        });
        foreach (var referralsForYear in referrals.GroupBy(referral => referral.ExternalPmNumber[4..8]))
        {
            var expectedNumbers = Enumerable.Range(1, referralsForYear.Count())
                .Select(number => $"WMS-{referralsForYear.Key}-{number:D4}")
                .OrderBy(number => number, StringComparer.Ordinal);
            Assert.Equal(expectedNumbers, referralsForYear.Select(referral => referral.ExternalPmNumber).OrderBy(number => number, StringComparer.Ordinal));
        }

        var scenarioAsset = Assert.Single(assets, asset => asset.AssetCode == "FE-ADMINISTRATION-001");
        Assert.Equal(
            GsdValidationDatasetBuilder.StableId($"asset:{AssetCategoryCatalog.FireExtinguisher}:Administration:001"),
            scenarioAsset.Id);
        var scenarioSchedule = Assert.Single(schedules, schedule =>
            schedule.AssetId == scenarioAsset.Id && schedule.PmCycle == "2026-05");
        Assert.Equal(
            GsdValidationDatasetBuilder.StableId($"schedule:{scenarioAsset.Id:D}:2026-05"),
            scenarioSchedule.Id);
        var scenarioForm = Assert.Single(forms, form => form.PmCycle == "2026-05"
            && form.Department == "Administration"
            && form.AssetCategory == AssetCategoryCatalog.FireExtinguisher);
        Assert.Equal("PM-2026-FE-ADMINISTRATION-05", scenarioForm.FileNumber);
        Assert.Equal("Acknowledged", scenarioForm.Status);
        Assert.Equal(
            GsdValidationDatasetBuilder.StableId($"form:{AssetCategoryCatalog.FireExtinguisher}:Administration:2026-05"),
            scenarioForm.Id);
        var scenarioInspection = Assert.Single(inspectionRows, inspection => inspection.ScheduleId == scenarioSchedule.Id);
        Assert.False(scenarioInspection.IsOperational);
        Assert.Equal("The pressure indicator was outside its expected range during inspection.", scenarioInspection.Remarks);
        Assert.Equal(
            GsdValidationDatasetBuilder.StableId($"inspection:{scenarioSchedule.Id:D}"),
            scenarioInspection.Id);
        Assert.Equal("WMS-2026-0001", Assert.Single(referrals, referral => referral.InspectionId == scenarioInspection.Id).ExternalPmNumber);

        var generatedIds = assets.Select(asset => asset.Id)
            .Concat(schedules.Select(schedule => schedule.Id))
            .Concat(inspectionRows.Select(inspection => inspection.Id))
            .Concat(forms.Select(form => form.Id))
            .Concat(acknowledgements.Select(acknowledgement => acknowledgement.Id))
            .Concat(audits.Select(audit => audit.Id));
        Assert.Equal(generatedIds.Count(), generatedIds.Distinct().Count());
        Assert.All(generatedIds, id => Assert.Matches(
            "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            id.ToString("D")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
        Assert.Equal(248, await context.Assets.CountAsync());
    }

    [Fact]
    public async Task Seed_rejects_partial_target_without_changing_it()
    {
        await using var context = new ApplicationDbContext(CreateOptions());
        await AddRoleAccountsAsync(context);
        context.Assets.Add(new Asset
        {
            Id = Guid.Parse("a0bf8e8f-7b18-4ee2-8e26-41d87097528b"),
            AssetCode = "FE-CCMS-001",
            AssetCategory = AssetCategoryCatalog.FireExtinguisher,
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var seeder = CreateSeeder(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
        Assert.Equal(1, await context.Assets.CountAsync());
        Assert.Empty(await context.PreventiveMaintenanceSchedules.ToListAsync());
    }

    private static GsdValidationDatasetSeeder CreateSeeder(ApplicationDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [StagingDemoInitialization.ModeSetting] = "true",
                [GsdValidationSeedGuard.EnabledSetting] = "true",
                [GsdValidationSeedGuard.TargetConfirmationSetting] = GsdValidationSeedGuard.InMemoryTargetConfirmation
            })
            .Build();
        return new GsdValidationDatasetSeeder(context, configuration, new DevelopmentHostEnvironment());
    }

    private static IConfiguration CreateSeedConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            [StagingDemoInitialization.ModeSetting] = "true",
            [GsdValidationSeedGuard.EnabledSetting] = "true",
            [GsdValidationSeedGuard.TargetConfirmationSetting] = GsdValidationSeedGuard.InMemoryTargetConfirmation,
            [StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Admin)] = "GsdAdminValidation2026!",
            [StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Gsd)] = "GsdGsdValidation2026!",
            [StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Inspector)] = "GsdInspectorValidation2026!",
            [StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Supervisor)] = "GsdSupervisorValidation2026!"
        })
        .Build();

    private static async Task<IReadOnlyDictionary<string, Guid>> AddRoleAccountsAsync(ApplicationDbContext context)
    {
        var roles = AuthRoleCatalog.Values.ToDictionary(
            name => name,
            name => new IdentityRole<Guid>(name)
            {
                Id = GsdValidationDatasetBuilder.StableId($"test-role:{name}"),
                NormalizedName = name.ToUpperInvariant()
            },
            StringComparer.Ordinal);
        context.Roles.AddRange(roles.Values);

        var users = StagingDemoInitialization.Roles.ToDictionary(
            name => name,
            name =>
            {
                var email = StagingDemoInitialization.Email(name);
                return new ApplicationUser
                {
                    Id = GsdValidationDatasetBuilder.StableId($"test-user:{name}"),
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    DisplayName = $"Fictional {name} user",
                    IsActive = true
                };
            },
            StringComparer.Ordinal);
        context.Users.AddRange(users.Values);
        context.UserRoles.AddRange(StagingDemoInitialization.Roles.Select(role =>
            new IdentityUserRole<Guid> { UserId = users[role].Id, RoleId = roles[role].Id }));
        await context.SaveChangesAsync();
        return users.ToDictionary(item => item.Key, item => item.Value.Id, StringComparer.Ordinal);
    }

    private static async Task AssertLiveBatchAsync(
        ApplicationDbContext context,
        string assetCategory,
        string department,
        string pmCycle)
    {
        var schedules = await context.PreventiveMaintenanceSchedules
            .Include(schedule => schedule.Asset)
            .Where(schedule => schedule.Asset!.AssetCategory == assetCategory
                && schedule.Asset.Department == department
                && schedule.PmCycle == pmCycle)
            .ToListAsync();
        Assert.Equal(13, schedules.Count);
        Assert.All(schedules, schedule =>
        {
            Assert.Equal(ScheduleStatusCatalog.Due, schedule.Status);
            Assert.Null(schedule.AssignedToUserId);
            Assert.Null(schedule.AssignedSupervisorUserId);
            Assert.Null(schedule.CompletedAt);
            Assert.True(schedule.Asset!.CreatedAt < schedule.ScheduleDate);
        });

        var scheduleIds = schedules.Select(schedule => schedule.Id).ToArray();
        Assert.Equal(0, await context.InspectionRecords.CountAsync(inspection => scheduleIds.Contains(inspection.ScheduleId)));
        Assert.Equal(0, await context.PreventiveMaintenanceForms.CountAsync(form => form.AssetCategory == assetCategory
            && form.Department == department && form.PmCycle == pmCycle));
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-gsd-validation-{Guid.NewGuid():N}")
            .Options;

    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "UniPM.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
