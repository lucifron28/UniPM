using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

internal readonly record struct ScheduleBatchIdentity(
    string Department,
    string AssetCategory,
    string PmCycle)
{
    internal static bool TryCreate(
        string? department,
        string? assetCategory,
        string? pmCycle,
        out ScheduleBatchIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(department)
            || string.IsNullOrWhiteSpace(assetCategory)
            || !PreventiveMaintenanceCycle.TryParse(pmCycle, out _, out _))
        {
            return false;
        }

        identity = new ScheduleBatchIdentity(
            department.Trim().ToUpperInvariant(),
            assetCategory.Trim().ToUpperInvariant(),
            pmCycle!);
        return true;
    }

    internal static bool TryCreate(
        PreventiveMaintenanceSchedule schedule,
        out ScheduleBatchIdentity identity)
    {
        return TryCreate(
            schedule.Asset?.Department,
            schedule.Asset?.AssetCategory,
            PreventiveMaintenanceCycle.ForSchedule(schedule),
            out identity);
    }

    internal string CreateLockResource()
    {
        var serializedIdentity = JsonSerializer.SerializeToUtf8Bytes(
            new[] { Department, AssetCategory, PmCycle });
        var hash = Convert.ToHexString(SHA256.HashData(serializedIdentity));
        return $"UniPM:ScheduleBatch:{hash}";
    }
}

internal sealed class ScheduleBatchMutationLockLease : IAsyncDisposable
{
    private const int LockTimeoutMilliseconds = 5000;
    private readonly IDbContextTransaction? transaction;
    private readonly bool ownsTransaction;

    private ScheduleBatchMutationLockLease(
        IDbContextTransaction? transaction,
        bool acquired,
        bool ownsTransaction)
    {
        this.transaction = transaction;
        Acquired = acquired;
        this.ownsTransaction = ownsTransaction;
    }

    internal bool Acquired { get; }

    internal static async Task<ScheduleBatchMutationLockLease> AcquireAsync(
        ApplicationDbContext context,
        ScheduleBatchIdentity identity,
        CancellationToken cancellationToken,
        IDbContextTransaction? transaction = null)
    {
        if (!string.Equals(
                context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
        {
            return new ScheduleBatchMutationLockLease(null, acquired: true, ownsTransaction: false);
        }

        var ownsTransaction = transaction is null;
        transaction ??= await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = @lockTimeout,
                    @DbPrincipal = N'public';
                SELECT @lockResult;
                """;

            var resourceParameter = command.CreateParameter();
            resourceParameter.ParameterName = "@resource";
            resourceParameter.DbType = DbType.String;
            resourceParameter.Size = 128;
            resourceParameter.Value = identity.CreateLockResource();
            command.Parameters.Add(resourceParameter);

            var timeoutParameter = command.CreateParameter();
            timeoutParameter.ParameterName = "@lockTimeout";
            timeoutParameter.DbType = DbType.Int32;
            timeoutParameter.Value = LockTimeoutMilliseconds;
            command.Parameters.Add(timeoutParameter);

            var result = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            if (result >= 0)
            {
                return new ScheduleBatchMutationLockLease(
                    transaction, acquired: true, ownsTransaction: ownsTransaction);
            }

            if (ownsTransaction)
            {
                await transaction.DisposeAsync();
            }

            return new ScheduleBatchMutationLockLease(null, acquired: false, ownsTransaction: false);
        }
        catch
        {
            if (ownsTransaction)
            {
                await transaction.DisposeAsync();
            }

            throw;
        }
    }

    internal Task CommitAsync(CancellationToken cancellationToken)
    {
        return ownsTransaction && transaction is not null
            ? transaction.CommitAsync(cancellationToken)
            : Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ownsTransaction && transaction is not null
            ? transaction.DisposeAsync()
            : ValueTask.CompletedTask;
    }
}

internal sealed class ScheduleBatchMutationConflictException : Exception
{
}
