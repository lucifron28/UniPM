using Microsoft.EntityFrameworkCore;

namespace UniPM.Api.Data;

/// <summary>
/// Centralizes the SQL Server dialect used by UniPM database contexts.
/// </summary>
public static class SqlServerCompatibility
{
    /// <summary>
    /// SQL Server 2019 database compatibility level. This preserves a shared
    /// provider boundary for runtime, tools, and SQL Server integration tests.
    /// </summary>
    public const int CompatibilityLevel = 150;

    public static DbContextOptionsBuilder UseUniPmSqlServer(
        this DbContextOptionsBuilder options,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return options.UseSqlServer(
            connectionString,
            sqlServer => sqlServer.UseCompatibilityLevel(CompatibilityLevel));
    }

    public static DbContextOptionsBuilder<TContext> UseUniPmSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string connectionString)
        where TContext : DbContext
    {
        UseUniPmSqlServer((DbContextOptionsBuilder)options, connectionString);
        return options;
    }
}
