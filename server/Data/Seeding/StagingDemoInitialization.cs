using Microsoft.EntityFrameworkCore;
using UniPM.Api.Features.Auth;

namespace UniPM.Api.Data.Seeding;

internal static class StagingDemoInitialization
{
    internal const string ModeSetting = "UNIPM_STAGING_DEMO_INITIALIZATION";
    internal const string SharedSetting = "UNIPM_STAGING_ALLOW_SHARED_PASSWORD";
    internal static readonly string[] Roles =
        [AuthRoleCatalog.Admin, AuthRoleCatalog.Gsd, AuthRoleCatalog.Inspector, AuthRoleCatalog.Supervisor];

    internal static bool Enabled(IConfiguration? configuration) => Flag(configuration, ModeSetting);

    internal static bool Flag(IConfiguration? configuration, string name)
    {
        var value = configuration?[name];
        if (value is null) return false;
        if (!bool.TryParse(value, out var enabled))
            throw new InvalidOperationException($"{name} must be true or false.");
        return enabled;
    }

    internal static string Email(string role) => $"{role.ToLowerInvariant()}@unipm.test";
    internal static string PasswordSetting(string role) => $"UNIPM_STAGING_{role.ToUpperInvariant()}_PASSWORD";

    internal static Dictionary<string, string> Passwords(IConfiguration configuration)
    {
        var shared = Flag(configuration, SharedSetting);
        var values = Roles.ToDictionary(role => role, role =>
            configuration[shared ? "UNIPM_STAGING_DEMO_PASSWORD" : PasswordSetting(role)] ?? string.Empty);
        if (values.Values.Any(password => password.Length < 12
            || !password.Any(char.IsUpper) || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit)))
            throw new InvalidOperationException("Staging demo credentials are missing or do not satisfy the password policy.");
        if (!shared && values.Values.Distinct(StringComparer.Ordinal).Count() != Roles.Length)
            throw new InvalidOperationException("Distinct staging demo credentials are required unless shared mode is explicitly selected.");
        if (shared && Roles.Any(role => !string.IsNullOrEmpty(configuration[PasswordSetting(role)])))
            throw new InvalidOperationException("Do not combine shared and per-role staging credential modes.");
        return values;
    }

    internal static async Task EnsureReadyAsync(ApplicationDbContext context, bool accountsCreated,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlServer())
            throw new InvalidOperationException("Staging demo initialization requires the verified SQL Server schema.");
        var expected = context.Database.GetMigrations().ToArray();
        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        if (expected.Length != 22 || expected[^1] != "20261010141251_AddInspectionPhotoEvidence"
            || !expected.SequenceEqual(applied, StringComparer.Ordinal))
            throw new InvalidOperationException("Staging demo initialization requires the exact 22 applied migrations; it never migrates automatically.");

        var allowedCounts = accountsCreated ? new Dictionary<string, long>
        {
            ["AspNetRoles"] = AuthRoleCatalog.Values.Count,
            ["AspNetUsers"] = Roles.Length,
            ["AspNetUserRoles"] = Roles.Length
        } : [];
        foreach (var table in context.Model.GetEntityTypes().Select(entity => entity.GetTableName()).Distinct())
        {
            if (table is null) continue;
            FormattableString countQuery = $"""
                SELECT COALESCE(SUM(partitions.[rows]), CONVERT(bigint, 0)) AS [Value]
                FROM sys.tables AS tables
                INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
                INNER JOIN sys.partitions AS partitions ON partitions.object_id = tables.object_id
                WHERE schemas.name = {"dbo"} AND tables.name = {table}
                  AND partitions.index_id IN (0, 1)
                """;
            var count = await context.Database.SqlQuery<long>(
                countQuery).SingleAsync(cancellationToken);
            if (count != allowedCounts.GetValueOrDefault(table))
                throw new InvalidOperationException("Staging database is not in the expected empty initialization state. Stop for partial-state review; do not rerun or reset.");
        }
        if (!accountsCreated) return;
        var users = await context.Users.AsNoTracking().ToListAsync(cancellationToken);
        var assignments = await (from assignment in context.UserRoles
                                 join role in context.Roles on assignment.RoleId equals role.Id
                                 select new { assignment.UserId, role.Name }).ToListAsync(cancellationToken);
        foreach (var role in Roles)
        {
            var user = users.SingleOrDefault(user => user.Email == Email(role));
            if (user is null || !user.IsActive || !assignments.Any(item => item.UserId == user.Id && item.Name == role))
                throw new InvalidOperationException("Staging demo account identities or role assignments do not match the expected initialization state.");
        }
    }
}
