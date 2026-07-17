using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;

namespace UniPM.Api.Features.Retrieval;

internal sealed class SqlServerLexicalMaintenanceRetriever(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : ILexicalMaintenanceRetriever
{
    private const string SearchSql = """
        SELECT TOP (@limit)
            document.InspectionId,
            document.AssetId,
            document.ScheduleId,
            document.AssetCode,
            document.AssetCategory,
            document.Building,
            document.Department,
            document.Location,
            document.DateInspected,
            document.IsOperational,
            CONVERT(int, matches.[RANK]) AS RawLexicalRank
        FROM CONTAINSTABLE(
            [dbo].[MaintenanceSearchDocuments],
            [SearchText],
            @searchCondition) AS matches
        INNER JOIN [dbo].[MaintenanceSearchDocuments] AS document
            ON document.InspectionId = matches.[KEY]
        WHERE (@assetId IS NULL OR document.AssetId = @assetId)
          AND (@assetCategory IS NULL OR document.AssetCategory = @assetCategory)
          AND (@building IS NULL OR document.Building = @building)
          AND (@department IS NULL OR document.Department = @department)
          AND (@location IS NULL OR document.Location = @location)
          AND (@isOperational IS NULL OR document.IsOperational = @isOperational)
          AND (@dateFrom IS NULL OR document.DateInspected >= @dateFrom)
          AND (@dateTo IS NULL OR document.DateInspected <= @dateTo)
        ORDER BY matches.[RANK] DESC, document.DateInspected DESC, document.InspectionId ASC;
        """;

    private const string FullTextReadinessSql = """
        SELECT CONVERT(bit,
            CASE
                WHEN ISNULL(TRY_CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')), 0) <> 1 THEN 0
                WHEN NOT EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.tables AS tables
                        ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas
                        ON schemas.schema_id = tables.schema_id
                    INNER JOIN sys.fulltext_catalogs AS catalog
                        ON catalog.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                    WHERE schemas.name = N'dbo'
                      AND tables.name = N'MaintenanceSearchDocuments'
                      AND catalog.name = N'UniPMMaintenanceRetrieval'
                      AND fullTextIndex.is_enabled = 1
                      AND EXISTS (
                          SELECT 1
                          FROM sys.fulltext_index_columns AS indexColumn
                          INNER JOIN sys.columns AS columns
                              ON columns.object_id = indexColumn.object_id
                             AND columns.column_id = indexColumn.column_id
                          WHERE indexColumn.object_id = fullTextIndex.object_id
                            AND columns.name = N'SearchText')) THEN 0
                ELSE 1
            END);
        """;

    public async Task<IReadOnlyList<LexicalMaintenanceSearchResult>> SearchAsync(
        LexicalMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = LexicalMaintenanceQueryBuilder.Build(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!context.Database.IsSqlServer())
        {
            throw new LexicalMaintenanceAvailabilityException(
                "Lexical maintenance retrieval requires the SQL Server EF Core provider.");
        }

        try
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = context.Database.GetDbConnection();
            await EnsureFullTextReadyAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = SearchSql;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@limit", query.Limit, DbType.Int32);
            AddParameter(command, "@searchCondition", query.SearchCondition, DbType.String);
            AddParameter(command, "@assetId", query.AssetId, DbType.Guid);
            AddParameter(command, "@assetCategory", query.AssetCategory, DbType.String);
            AddParameter(command, "@building", query.Building, DbType.String);
            AddParameter(command, "@department", query.Department, DbType.String);
            AddParameter(command, "@location", query.Location, DbType.String);
            AddParameter(command, "@isOperational", query.IsOperational, DbType.Boolean);
            AddParameter(command, "@dateFrom", query.DateFrom, DbType.DateTimeOffset);
            AddParameter(command, "@dateTo", query.DateTo, DbType.DateTimeOffset);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<LexicalMaintenanceSearchResult>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new LexicalMaintenanceSearchResult(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.GetFieldValue<DateTimeOffset>(8),
                    reader.GetBoolean(9),
                    reader.GetInt32(10)));
            }

            return results;
        }
        catch (LexicalMaintenanceRetrievalException)
        {
            throw;
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LexicalMaintenanceExecutionException(
                "SQL Server could not execute the lexical maintenance retrieval query.",
                exception);
        }
    }

    private static async Task EnsureFullTextReadyAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = FullTextReadinessSql;
        command.CommandType = CommandType.Text;

        object? value;
        try
        {
            value = await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LexicalMaintenanceAvailabilityException(
                "SQL Server Full-Text Search is unavailable for lexical maintenance retrieval.",
                exception);
        }

        if (value is not bool isReady || !isReady)
        {
            throw new LexicalMaintenanceAvailabilityException(
                "The UniPM maintenance full-text catalog or SearchText index is missing or unavailable.");
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object? value,
        DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
