using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

namespace UniPM.Api.Tests;

public sealed class StagingDemoInitializationTests
{
    [Theory]
    [InlineData("dbo", "Assets", "SELECT COUNT_BIG(*) FROM [dbo].[Assets]")]
    [InlineData("dbo", "Asset]History", "SELECT COUNT_BIG(*) FROM [dbo].[Asset]]History]")]
    public void Exact_count_command_quotes_validated_identifiers(string schema, string table, string expected)
    {
        Assert.Equal(expected, StagingDemoInitialization.BuildExactCountCommandText(schema, table));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\nname")]
    public void Exact_count_command_rejects_invalid_identifiers(string identifier)
    {
        Assert.Throws<InvalidOperationException>(
            () => StagingDemoInitialization.QuoteSqlIdentifier(identifier));
    }

    [Fact]
    public void Exact_count_command_rejects_identifiers_longer_than_sql_server_limit()
    {
        Assert.Throws<InvalidOperationException>(
            () => StagingDemoInitialization.QuoteSqlIdentifier(new string('x', 129)));
    }

    private static Dictionary<string, string?> Credentials(bool shared = false)
    {
        var values = new Dictionary<string, string?> { [StagingDemoInitialization.ModeSetting] = "true" };
        if (shared)
        {
            values[StagingDemoInitialization.SharedSetting] = "true";
            values["UNIPM_STAGING_DEMO_PASSWORD"] = NewPassword();
        }
        else
            foreach (var role in StagingDemoInitialization.Roles)
                values[StagingDemoInitialization.PasswordSetting(role)] = NewPassword();
        return values;
    }

    private static string NewPassword() => $"Aa1!{Guid.NewGuid():N}";
    private static IConfigurationRoot Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Credentials_require_explicit_shared_opt_in_and_reject_missing_invalid_or_duplicate_values()
    {
        var distinct = Credentials();
        Assert.Equal(4, StagingDemoInitialization.Passwords(Config(distinct)).Values.Distinct().Count());
        var shared = Credentials(true);
        Assert.Single(StagingDemoInitialization.Passwords(Config(shared)).Values.Distinct());
        shared.Remove(StagingDemoInitialization.SharedSetting);
        Assert.Throws<InvalidOperationException>(() => StagingDemoInitialization.Passwords(Config(shared)));
        foreach (var invalid in new[] { "", "short", new string('a', 20) })
        {
            var values = Credentials();
            values[StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Gsd)] = invalid;
            Assert.Throws<InvalidOperationException>(() => StagingDemoInitialization.Passwords(Config(values)));
        }
        distinct[StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Gsd)] =
            distinct[StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Admin)];
        Assert.Throws<InvalidOperationException>(() => StagingDemoInitialization.Passwords(Config(distinct)));
        shared = Credentials(true);
        shared[StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Gsd)] = NewPassword();
        var error = Assert.Throws<InvalidOperationException>(() => StagingDemoInitialization.Passwords(Config(shared)));
        Assert.True(shared.Values.Where(value => value?.Length >= 12).All(value => !error.Message.Contains(value!)));
        Assert.Throws<InvalidOperationException>(() => StagingDemoInitialization.Enabled(
            Config(new() { [StagingDemoInitialization.ModeSetting] = "invalid" })));
    }

    [SqlServer2019Fact]
    public Task Distinct_credentials_seed_four_accounts_and_fictional_data_without_migration_or_replacement() =>
        VerifyLocalSqlAsync(false);

    [SqlServer2019Fact]
    public Task Explicit_shared_mode_seeds_only_four_accounts_and_rejects_reinitialization() =>
        VerifyLocalSqlAsync(true);

    private async Task VerifyLocalSqlAsync(bool shared)
    {
        var baseConnection = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("UNIPM_SQLSERVER2019_TEST_CONNECTION"));
        // This fixture must never accept a cloud or arbitrary remote target.
        Assert.Contains(baseConnection.DataSource.ToLowerInvariant(),
            new[] { ".", "(local)", "localhost", Environment.MachineName.ToLowerInvariant() });
        baseConnection.InitialCatalog = "master";
        await using var master = new SqlConnection(baseConnection.ConnectionString);
        await master.OpenAsync();
        await using var command = master.CreateCommand();
        command.CommandText = "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'))";
        Assert.Equal(15, (int)(await command.ExecuteScalarAsync())!);
        command.CommandText = "SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')";
        Assert.Equal(1, (int)(await command.ExecuteScalarAsync())!);
        var name = $"UniPM_StagingSeedTest_{Guid.NewGuid():N}";
        var created = false;
        try
        {
            command.CommandText = $"CREATE DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
            created = true;
            command.CommandText = $"ALTER DATABASE [{name}] SET COMPATIBILITY_LEVEL = 150";
            await command.ExecuteNonQueryAsync();
            var connection = new SqlConnectionStringBuilder(baseConnection.ConnectionString) { InitialCatalog = name };
            var values = Credentials(shared);
            var configuration = Config(values);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IHostEnvironment>(new DevelopmentEnvironment());
            services.AddDbContextFactory<ApplicationDbContext>(options => options.UseUniPmSqlServer(connection.ConnectionString));
            services.AddUniPmAuthentication(configuration,
                JwtRuntimeConfiguration.Create(new JwtOptions(), true),
                AuthSessionRuntimeConfiguration.Create(new AuthSessionOptions { WebOrigin = "http://localhost:5173" }));
            services.AddScoped<DevelopmentDemoSeeder>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userSeeder = scope.ServiceProvider.GetRequiredService<DevelopmentUserSeeder>();
            var demoSeeder = scope.ServiceProvider.GetRequiredService<DevelopmentDemoSeeder>();
            var migrations = context.Database.GetMigrations().ToArray();
            // Only fixture setup applies migrations, to the owned local disposable database.
            await context.GetService<IMigrator>().MigrateAsync(migrations[^2]);
            await Assert.ThrowsAsync<InvalidOperationException>(userSeeder.SeedAsync);
            Assert.Equal(21, (await context.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await context.Users.ToListAsync());
            await context.Database.MigrateAsync();
            Assert.Equal(22, (await context.Database.GetAppliedMigrationsAsync()).Count());
            await StagingDemoInitialization.EnsureReadyAsync(context, accountsCreated: false);
            var credentialName = shared ? "UNIPM_STAGING_DEMO_PASSWORD" : StagingDemoInitialization.PasswordSetting(AuthRoleCatalog.Gsd);
            var originalPassword = configuration[credentialName];
            configuration[credentialName] = "invalid";
            await Assert.ThrowsAsync<InvalidOperationException>(userSeeder.SeedAsync);
            Assert.Empty(await context.Roles.ToListAsync());
            configuration[credentialName] = originalPassword;
            var result = await userSeeder.SeedAsync();
            Assert.Equal(4, result.UsersCreated);
            Assert.Equal(5, result.RolesCreated); // Legacy role contract retained, without its login.
            var users = await context.Users.ToListAsync();
            Assert.Equal(4, users.Count);
            var hashes = users.ToDictionary(user => user.Id, user => user.PasswordHash);
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var passwords = StagingDemoInitialization.Passwords(configuration);
            foreach (var role in StagingDemoInitialization.Roles)
            {
                var user = users.Single(user => user.Email == StagingDemoInitialization.Email(role));
                Assert.True(await manager.IsInRoleAsync(user, role));
                Assert.True(await manager.CheckPasswordAsync(user, passwords[role]));
                if (!shared)
                    foreach (var otherRole in StagingDemoInitialization.Roles.Where(other => other != role))
                        Assert.False(await manager.CheckPasswordAsync(user, passwords[otherRole]));
            }
            await StagingDemoInitialization.EnsureReadyAsync(context, accountsCreated: true);
            // A users-only partial initialization cannot restart from the beginning.
            await Assert.ThrowsAsync<InvalidOperationException>(userSeeder.SeedAsync);
            // Partial operational data is rejected, without deleting it.
            var partialAsset = new Asset
            {
                Id = Guid.NewGuid(), AssetCode = "FICTIONAL-PARTIAL", AssetCategory = "fire-extinguisher",
                Building = "Fictional", Department = "Fictional", Location = "Fictional", Status = "Active"
            };
            context.Assets.Add(partialAsset);
            await context.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => demoSeeder.SeedAsync());
            Assert.Equal(1, await context.Assets.CountAsync());
            // Fixture-only cleanup restores the expected users-only state, not an operator recovery path.
            context.Assets.Remove(partialAsset);
            await context.SaveChangesAsync();
            Assert.Equal(new DevelopmentDemoSeedResult(25, 105, 77, 42, 29), await demoSeeder.SeedAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => demoSeeder.SeedAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => demoSeeder.ResetAsync());
            Assert.Equal(25, await context.Assets.CountAsync());
            Assert.Equal(77, await context.InspectionRecords.CountAsync());
            Assert.Equal(42, await context.PreventiveMaintenanceForms.CountAsync());
            Assert.Equal(29, await context.PreventiveMaintenanceAcknowledgements.CountAsync());
            Assert.Empty(await context.InspectionPhotoEvidence.ToListAsync());
            var schedules = await context.PreventiveMaintenanceSchedules.Include(schedule => schedule.Asset).ToListAsync();
            Assert.Equal(105, schedules.Count);
            Assert.All(schedules, schedule =>
            {
                Assert.True(PreventiveMaintenanceCycle.TryParse(schedule.PmCycle, out _, out var month));
                Assert.True(CpmpScheduleFrequency.IsValid(schedule.Asset!.AssetCategory, month));
            });
            Assert.Equal(migrations, (await context.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.True((await context.Users.AsNoTracking().ToListAsync()).All(user => user.PasswordHash == hashes[user.Id]));

            await context.Database.ExecuteSqlRawAsync(
                "CREATE SCHEMA [UniPmStagingGuardTest]; ALTER SCHEMA [UniPmStagingGuardTest] TRANSFER [dbo].[RefreshSessions];");
            await Assert.ThrowsAsync<SqlException>(
                () => StagingDemoInitialization.EnsureReadyAsync(context, accountsCreated: true));
            await context.Database.ExecuteSqlRawAsync(
                "DROP TABLE [UniPmStagingGuardTest].[RefreshSessions]");
            await Assert.ThrowsAsync<SqlException>(
                () => StagingDemoInitialization.EnsureReadyAsync(context, accountsCreated: true));
        }
        finally
        {
            if (created)
            {
                SqlConnection.ClearAllPools();
                command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "UniPM.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
