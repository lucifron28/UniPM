using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Data.Seeding;

internal sealed record GsdValidationUserSeedResult(int RolesCreated, int UsersCreated);

internal sealed record GsdValidationDatasetSeedResult(
    int Assets,
    int Schedules,
    int Inspections,
    int Forms,
    int Acknowledgements,
    int WmsReferrals,
    int WmsReferralAudits,
    int OnTimeInspections,
    int LateInspections,
    int NonOperationalInspections,
    int AcknowledgedNonOperationalInspections,
    int AwaitingWmsReferral,
    int ReferredToWms);

internal sealed class GsdValidationUserSeeder(
    ApplicationDbContext context,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    internal async Task<GsdValidationUserSeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        GsdValidationSeedGuard.RequireOptInAndTarget(context, configuration, environment);
        var passwords = StagingDemoInitialization.Passwords(configuration);

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        await GsdValidationSeedGuard.EnsureCurrentMigrationHistoryAsync(context, cancellationToken);
        await GsdValidationSeedGuard.EnsureSqlTargetConstraintsAsync(context, cancellationToken);
        await GsdValidationSeedGuard.EnsureMappedRowCountsAsync(context, EmptyCounts, cancellationToken);
        await ValidatePasswordsAsync(passwords);

        var rolesCreated = 0;
        foreach (var roleName in AuthRoleCatalog.Values)
        {
            var role = new IdentityRole<Guid>
            {
                Id = GsdValidationDatasetBuilder.StableId($"role:{roleName}"),
                Name = roleName
            };
            EnsureSucceeded(await roleManager.CreateAsync(role), $"create role '{roleName}'");
            rolesCreated++;
        }

        var usersCreated = 0;
        foreach (var roleName in StagingDemoInitialization.Roles)
        {
            var email = StagingDemoInitialization.Email(roleName);
            var user = new ApplicationUser
            {
                Id = GsdValidationDatasetBuilder.StableId($"user:{roleName}"),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = DisplayNameFor(roleName),
                IsActive = true
            };
            EnsureSucceeded(await userManager.CreateAsync(user, passwords[roleName]), $"create role account '{roleName}'");
            EnsureSucceeded(await userManager.AddToRoleAsync(user, roleName), $"assign role '{roleName}'");
            usersCreated++;
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new GsdValidationUserSeedResult(rolesCreated, usersCreated);
    }

    private async Task ValidatePasswordsAsync(IReadOnlyDictionary<string, string> passwords)
    {
        foreach (var roleName in StagingDemoInitialization.Roles)
        foreach (var validator in userManager.PasswordValidators)
        {
            var email = StagingDemoInitialization.Email(roleName);
            var user = new ApplicationUser { UserName = email, Email = email };
            EnsureSucceeded(
                await validator.ValidateAsync(userManager, user, passwords[roleName]),
                $"validate the configured password for role '{roleName}'");
        }
    }

    private static string DisplayNameFor(string roleName) => roleName switch
    {
        AuthRoleCatalog.Admin => "UniPM Administrator",
        AuthRoleCatalog.Gsd => "GSD Personnel",
        AuthRoleCatalog.Inspector => "Maintenance Inspector",
        AuthRoleCatalog.Supervisor => "Maintenance Supervisor",
        _ => throw new InvalidOperationException("The GSD validation account role is not supported.")
    };

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Code))}");
        }
    }

    private static readonly IReadOnlyDictionary<string, long> EmptyCounts =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class GsdValidationDatasetSeeder(
    ApplicationDbContext context,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    internal async Task<GsdValidationDatasetSeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        GsdValidationSeedGuard.RequireOptInAndTarget(context, configuration, environment);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        await GsdValidationSeedGuard.EnsureCurrentMigrationHistoryAsync(context, cancellationToken);
        await GsdValidationSeedGuard.EnsureSqlTargetConstraintsAsync(context, cancellationToken);
        await GsdValidationSeedGuard.EnsureMappedRowCountsAsync(context, ExpectedAccountCounts, cancellationToken);
        var accounts = await GsdValidationSeedGuard.RequireRoleAccountsAsync(context, cancellationToken);

        var dataset = GsdValidationDatasetBuilder.Build(
            accounts[AuthRoleCatalog.Inspector],
            accounts[AuthRoleCatalog.Supervisor],
            accounts[AuthRoleCatalog.Gsd]);
        context.Assets.AddRange(dataset.Assets);
        context.PreventiveMaintenanceSchedules.AddRange(dataset.Schedules);
        context.PreventiveMaintenanceForms.AddRange(dataset.Forms);
        context.InspectionRecords.AddRange(dataset.Inspections);
        context.PreventiveMaintenanceAcknowledgements.AddRange(dataset.Acknowledgements);
        context.InspectionWmsReferrals.AddRange(dataset.WmsReferrals);
        context.InspectionWmsReferralAudits.AddRange(dataset.WmsReferralAudits);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new GsdValidationDatasetSeedResult(
            dataset.Assets.Count,
            dataset.Schedules.Count,
            dataset.Inspections.Count,
            dataset.Forms.Count,
            dataset.Acknowledgements.Count,
            dataset.WmsReferrals.Count,
            dataset.WmsReferralAudits.Count,
            dataset.OnTimeInspections,
            dataset.LateInspections,
            dataset.NonOperationalInspections,
            dataset.AcknowledgedNonOperationalInspections,
            dataset.AwaitingWmsReferral,
            dataset.ReferredToWms);
    }

    private static readonly IReadOnlyDictionary<string, long> ExpectedAccountCounts =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["AspNetRoles"] = AuthRoleCatalog.Values.Count,
            ["AspNetUsers"] = StagingDemoInitialization.Roles.Length,
            ["AspNetUserRoles"] = StagingDemoInitialization.Roles.Length
        };
}

internal static class GsdValidationSeedGuard
{
    internal const string EnabledSetting = "UNIPM_GSD_VALIDATION_SEED_ENABLED";
    internal const string TargetConfirmationSetting = "UNIPM_GSD_VALIDATION_TARGET_CONFIRMATION";
    internal const string InMemoryTargetConfirmation = "INMEMORY/GSD_VALIDATION_TESTS";

    private static readonly MethodInfo DbContextSetMethod = typeof(DbContext)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(DbContext.Set)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 0);

    private static readonly MethodInfo InMemoryLongCountAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
        .GetMethods(BindingFlags.Static | BindingFlags.Public)
        .Single(method => method.Name == nameof(EntityFrameworkQueryableExtensions.LongCountAsync)
            && method.IsGenericMethodDefinition
            && method.GetParameters() is [{ ParameterType.IsGenericType: true }, { ParameterType: var tokenType }]
            && method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>)
            && tokenType == typeof(CancellationToken));

    internal static void RequireOptInAndTarget(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "GSD validation seed commands must run as a short-lived Development operator process.");
        }
        if (!StagingDemoInitialization.Enabled(configuration))
        {
            throw new InvalidOperationException(
                $"{StagingDemoInitialization.ModeSetting}=true is required for the four fictional staging accounts.");
        }
        if (!StagingDemoInitialization.Flag(configuration, EnabledSetting))
        {
            throw new InvalidOperationException($"{EnabledSetting}=true is required.");
        }

        var confirmation = configuration[TargetConfirmationSetting]?.Trim();
        if (context.Database.IsSqlServer())
        {
            var connection = context.Database.GetDbConnection();
            var target = $"{connection.DataSource}/{connection.Database}";
            if (string.IsNullOrWhiteSpace(connection.DataSource)
                || string.IsNullOrWhiteSpace(connection.Database)
                || !string.Equals(confirmation, target, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{TargetConfirmationSetting} must exactly match the configured SQL Server and database target.");
            }
            return;
        }

        if (!IsInMemory(context.Database)
            || !string.Equals(confirmation, InMemoryTargetConfirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "GSD validation seeding requires SQL Server or the explicitly named InMemory test provider.");
        }
    }

    internal static async Task EnsureCurrentMigrationHistoryAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (IsInMemory(context.Database))
        {
            return;
        }
        if (!context.Database.IsSqlServer())
        {
            throw new InvalidOperationException("GSD validation seeding requires SQL Server migrations.");
        }

        var expected = context.Database.GetMigrations().ToArray();
        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        if (expected.Length == 0 || !expected.SequenceEqual(applied, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "GSD validation seeding requires the exact current compiled migration history to be applied; it never migrates automatically.");
        }
    }

    internal static async Task EnsureSqlTargetConstraintsAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (IsInMemory(context.Database))
        {
            return;
        }
        if (!context.Database.IsSqlServer())
        {
            throw new InvalidOperationException("GSD validation seeding requires a SQL Server target.");
        }

        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CONVERT(int, compatibility_level), CONVERT(int, SERVERPROPERTY('EngineEdition')), CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')) FROM sys.databases WHERE name = DB_NAME()";
            if (context.Database.CurrentTransaction is { } transaction)
            {
                command.Transaction = transaction.GetDbTransaction();
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)
                || reader.IsDBNull(0)
                || reader.IsDBNull(1)
                || reader.IsDBNull(2))
            {
                throw new InvalidOperationException("GSD validation could not confirm the SQL Server compatibility level, engine edition, and Full-Text availability.");
            }

            var compatibilityLevel = reader.GetInt32(0);
            var engineEdition = reader.GetInt32(1);
            var fullTextInstalled = reader.GetInt32(2) == 1;
            if (compatibilityLevel != 150 || engineEdition is not (2 or 3 or 4 or 5) || !fullTextInstalled)
            {
                throw new InvalidOperationException(
                    "GSD validation seeding requires compatibility level 150, a local SQL Server or Azure SQL Database engine, and installed Full-Text Search.");
            }

            if (engineEdition == 5)
            {
                RequireExplicitAzureEncryption(connection.ConnectionString);
            }
        }
        finally
        {
            if (!wasOpen)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    private static void RequireExplicitAzureEncryption(string connectionString)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        var encrypt = Convert.ToString(settings.Encrypt, System.Globalization.CultureInfo.InvariantCulture);
        if (!settings.ShouldSerialize(nameof(SqlConnectionStringBuilder.Encrypt))
            || !settings.ShouldSerialize(nameof(SqlConnectionStringBuilder.TrustServerCertificate))
            || !(string.Equals(encrypt, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(encrypt, "mandatory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(encrypt, "strict", StringComparison.OrdinalIgnoreCase))
            || settings.TrustServerCertificate)
        {
            throw new InvalidOperationException(
                "Azure SQL validation seeding requires explicit Encrypt=True (or Mandatory/Strict) and TrustServerCertificate=False connection settings.");
        }
    }

    internal static async Task EnsureMappedRowCountsAsync(
        ApplicationDbContext context,
        IReadOnlyDictionary<string, long> expectedCounts,
        CancellationToken cancellationToken)
    {
        var mappings = context.Model.GetEntityTypes()
            .Select(entity =>
            {
                var table = entity.GetTableName();
                var schema = entity.GetSchema() ?? context.Model.GetDefaultSchema() ?? "dbo";
                return (Schema: schema, Table: table, Entity: entity);
            })
            .Where(mapping => mapping.Table is not null)
            .GroupBy(mapping => (mapping.Schema, Table: mapping.Table!), mapping => mapping.Entity)
            .ToArray();

        if (mappings.Any(mapping => !string.Equals(mapping.Key.Schema, "dbo", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("GSD validation seeding requires all mapped tables in the dbo schema.");
        }

        if (context.Database.IsSqlServer())
        {
            await EnsureSqlMappedRowCountsAsync(context, mappings, expectedCounts, cancellationToken);
            return;
        }

        foreach (var mapping in mappings)
        {
            long count = 0;
            foreach (var entity in mapping)
            {
                count += await CountInMemoryEntityAsync(context, entity.ClrType, cancellationToken);
            }

            if (count != expectedCounts.GetValueOrDefault(mapping.Key.Table))
            {
                throw new InvalidOperationException(
                    "GSD validation seed target is not in the expected empty state. Stop for partial-state review; do not rerun or reset.");
            }
        }
    }

    internal static async Task<IReadOnlyDictionary<string, Guid>> RequireRoleAccountsAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var users = await context.Users.AsNoTracking().ToListAsync(cancellationToken);
        var roles = await context.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var assignments = await (
            from assignment in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            select new { assignment.UserId, role.Name })
            .ToListAsync(cancellationToken);

        if (roles.Count != AuthRoleCatalog.Values.Count
            || !AuthRoleCatalog.Values.All(name => roles.Any(role => role.Name == name))
            || users.Count != StagingDemoInitialization.Roles.Length
            || assignments.Count != StagingDemoInitialization.Roles.Length)
        {
            throw new InvalidOperationException("GSD validation seed requires exactly the four configured operational role accounts.");
        }

        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var roleName in StagingDemoInitialization.Roles)
        {
            var email = StagingDemoInitialization.Email(roleName);
            var user = users.SingleOrDefault(candidate => string.Equals(candidate.Email, email, StringComparison.OrdinalIgnoreCase));
            if (user is null
                || !user.IsActive
                || !assignments.Any(item => item.UserId == user.Id && item.Name == roleName))
            {
                throw new InvalidOperationException("GSD validation role accounts must be active and assigned to their matching role.");
            }

            ids.Add(roleName, user.Id);
        }

        if (assignments.Any(item => !ids.Values.Contains(item.UserId)
            || !ids.Any(expected => expected.Value == item.UserId && expected.Key == item.Name)))
        {
            throw new InvalidOperationException("GSD validation role accounts must each have exactly one matching role assignment.");
        }

        return ids;
    }

    private static async Task EnsureSqlMappedRowCountsAsync(
        ApplicationDbContext context,
        IEnumerable<IGrouping<(string Schema, string Table), Microsoft.EntityFrameworkCore.Metadata.IEntityType>> mappings,
        IReadOnlyDictionary<string, long> expectedCounts,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            foreach (var mapping in mappings)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = StagingDemoInitialization.BuildExactCountCommandText(
                    mapping.Key.Schema,
                    mapping.Key.Table);
                if (context.Database.CurrentTransaction is { } transaction)
                {
                    command.Transaction = transaction.GetDbTransaction();
                }

                var result = await command.ExecuteScalarAsync(cancellationToken);
                if (result is null or DBNull)
                {
                    throw new InvalidOperationException("GSD validation row-count preflight returned no value.");
                }

                var count = Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
                if (count != expectedCounts.GetValueOrDefault(mapping.Key.Table))
                {
                    throw new InvalidOperationException(
                        "GSD validation seed target is not in the expected empty state. Stop for partial-state review; do not rerun or reset.");
                }
            }
        }
        finally
        {
            if (!wasOpen)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    private static async Task<long> CountInMemoryEntityAsync(
        ApplicationDbContext context,
        Type entityType,
        CancellationToken cancellationToken)
    {
        var set = (IQueryable)DbContextSetMethod.MakeGenericMethod(entityType).Invoke(context, null)!;
        var task = (Task<long>)InMemoryLongCountAsyncMethod
            .MakeGenericMethod(entityType)
            .Invoke(null, [set, cancellationToken])!;
        return await task;
    }

    private static bool IsInMemory(DatabaseFacade database) =>
        string.Equals(database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal);
}
