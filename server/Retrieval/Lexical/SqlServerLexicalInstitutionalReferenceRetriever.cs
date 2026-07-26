using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;

namespace UniPM.Api.Features.Retrieval;

/// <summary>
/// Searches only current, applicable institutional sections. It deliberately
/// does not combine reference evidence with maintenance history or OEM sources.
/// </summary>
internal sealed class SqlServerLexicalInstitutionalReferenceRetriever(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : ILexicalInstitutionalReferenceRetriever
{
    private const string SearchSql = """
        SELECT TOP (@limit)
            document.Id,
            section.Id,
            document.SourceKey,
            document.Revision,
            document.Title,
            document.PublisherAuthority,
            document.EffectiveDate,
            section.Sequence,
            section.Heading,
            section.SectionText,
            section.SourceLocator,
            section.PageStart,
            section.PageEnd,
            match.AssetCategory,
            match.ScopeLabel,
            CONVERT(int, matches.[RANK]) AS RawLexicalRank
        FROM CONTAINSTABLE(
            [dbo].[ReferenceDocumentSections],
            ([Heading], [SectionText]),
            @searchCondition) AS matches
        INNER JOIN [dbo].[ReferenceDocumentSections] AS section
            ON section.Id = matches.[KEY]
        INNER JOIN [dbo].[ReferenceDocuments] AS document
            ON document.Id = section.ReferenceDocumentId
        CROSS APPLY (
            SELECT TOP (1) applicability.AssetCategory, applicability.ScopeLabel
            FROM [dbo].[ReferenceDocumentApplicabilities] AS applicability
            WHERE applicability.ReferenceDocumentId = document.Id
              AND (applicability.AssetCategory = @assetCategory OR applicability.AssetCategory IS NULL)
            ORDER BY CASE WHEN applicability.AssetCategory = @assetCategory THEN 0 ELSE 1 END,
                     COALESCE(applicability.ScopeLabel, N'') ASC
        ) AS match
        WHERE document.SourceType = N'Institutional'
          AND document.LifecycleStatus = N'Active'
          AND (document.EffectiveDate IS NULL OR document.EffectiveDate <= @asOfDate)
          AND EXISTS (
              SELECT 1
              FROM [dbo].[ReferenceDocumentApplicabilities] AS applicability
              WHERE applicability.ReferenceDocumentId = document.Id
                AND (applicability.AssetCategory = @assetCategory OR applicability.AssetCategory IS NULL)
          )
        ORDER BY matches.[RANK] DESC,
                 CASE WHEN document.EffectiveDate IS NULL THEN 0 ELSE 1 END DESC,
                 document.EffectiveDate DESC,
                 document.SourceKey ASC,
                 document.Revision ASC,
                 section.Sequence ASC,
                 section.Id ASC;
        """;

    private const string FullTextReadinessSql = """
        SELECT CONVERT(bit,
            CASE
                WHEN ISNULL(TRY_CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')), 0) <> 1 THEN 0
                WHEN NOT EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.tables AS tables ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
                    INNER JOIN sys.fulltext_catalogs AS catalog ON catalog.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                    WHERE schemas.name = N'dbo'
                      AND tables.name = N'ReferenceDocumentSections'
                      AND catalog.name = N'UniPMReferenceRetrieval'
                      AND fullTextIndex.is_enabled = 1
                      AND EXISTS (
                          SELECT 1
                          FROM sys.fulltext_index_columns AS indexColumn
                          INNER JOIN sys.columns AS columns
                              ON columns.object_id = indexColumn.object_id
                             AND columns.column_id = indexColumn.column_id
                          WHERE indexColumn.object_id = fullTextIndex.object_id
                            AND columns.name = N'Heading')
                      AND EXISTS (
                          SELECT 1
                          FROM sys.fulltext_index_columns AS indexColumn
                          INNER JOIN sys.columns AS columns
                              ON columns.object_id = indexColumn.object_id
                             AND columns.column_id = indexColumn.column_id
                          WHERE indexColumn.object_id = fullTextIndex.object_id
                            AND columns.name = N'SectionText')
                ) THEN 0
                ELSE 1
            END);
        """;

    public async Task<IReadOnlyList<InstitutionalReferenceLexicalSearchResult>> SearchAsync(
        InstitutionalReferenceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = InstitutionalReferenceQueryBuilder.Build(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!context.Database.IsSqlServer())
        {
            throw new InstitutionalReferenceAvailabilityException(
                "Institutional lexical retrieval requires the SQL Server EF Core provider.");
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
            AddParameter(command, "@asOfDate", query.AsOfDate, DbType.Date);
            AddParameter(command, "@assetCategory", query.AssetCategory, DbType.String);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<InstitutionalReferenceLexicalSearchResult>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new InstitutionalReferenceLexicalSearchResult(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                    reader.GetInt32(7),
                    reader.GetString(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    reader.IsDBNull(13)
                        ? InstitutionalReferenceApplicabilityMatch.CategoryWide
                        : InstitutionalReferenceApplicabilityMatch.CategorySpecific,
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.GetInt32(15)));
            }

            return results;
        }
        catch (InstitutionalReferenceRetrievalException)
        {
            throw;
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InstitutionalReferenceExecutionException(
                "SQL Server could not execute the institutional lexical retrieval query.",
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
        try
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not bool isReady || !isReady)
            {
                throw new InstitutionalReferenceAvailabilityException(
                    "The UniPM reference full-text catalog or section index is missing or unavailable.");
            }
        }
        catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InstitutionalReferenceAvailabilityException(
                "SQL Server Full-Text Search is unavailable for institutional lexical retrieval.",
                exception);
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value, DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
