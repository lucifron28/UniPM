using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Retrieval;

internal sealed class SqlServerSemanticMaintenanceRetriever(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEmbeddingService embeddingService,
    MaintenanceIssueNormalizer issueNormalizer)
    : ISemanticMaintenanceRetriever
{
    public async Task<IReadOnlyList<SemanticMaintenanceSearchResult>> SearchAsync(
        SemanticMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = SemanticMaintenanceQueryBuilder.Build(request, issueNormalizer);
        var descriptor = embeddingService.Descriptor;
        if (!descriptor.Enabled)
        {
            throw new SemanticMaintenanceAvailabilityException(
                "Semantic embeddings are disabled by configuration.");
        }

        IReadOnlyList<EmbeddingVector> queryVectors;
        try
        {
            queryVectors = await embeddingService.GenerateBatchAsync(
                [query.EmbeddingInput],
                cancellationToken);
        }
        catch (EmbeddingServiceAvailabilityException exception)
        {
            throw new SemanticMaintenanceAvailabilityException(
                "The configured embedding provider is unavailable for semantic retrieval.",
                exception);
        }
        catch (EmbeddingServiceExecutionException exception)
        {
            throw new SemanticMaintenanceExecutionException(
                "The embedding provider failed while generating the semantic query vector.",
                exception);
        }
        catch (EmbeddingVectorValidationException exception)
        {
            throw new SemanticMaintenanceDataException(
                "The embedding provider returned invalid semantic query vector data.",
                exception);
        }

        if (queryVectors.Count != 1)
        {
            throw new SemanticMaintenanceDataException(
                "The embedding provider returned an invalid query vector count.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!context.Database.IsSqlServer())
        {
            throw new SemanticMaintenanceAvailabilityException(
                "Semantic maintenance retrieval requires the SQL Server EF Core provider.");
        }

        try
        {
            var candidatesQuery = context.MaintenanceSearchDocuments
                .AsNoTracking()
                .Include(document => document.Embedding)
                .AsQueryable();

            if (query.AssetId is not null)
            {
                candidatesQuery = candidatesQuery.Where(document => document.AssetId == query.AssetId);
            }

            if (query.AssetCategory is not null)
            {
                candidatesQuery = candidatesQuery.Where(
                    document => document.AssetCategory == query.AssetCategory);
            }

            if (query.Building is not null)
            {
                candidatesQuery = candidatesQuery.Where(document => document.Building == query.Building);
            }

            if (query.Department is not null)
            {
                candidatesQuery = candidatesQuery.Where(document => document.Department == query.Department);
            }

            if (query.Location is not null)
            {
                candidatesQuery = candidatesQuery.Where(document => document.Location == query.Location);
            }

            if (query.IsOperational is not null)
            {
                candidatesQuery = candidatesQuery.Where(
                    document => document.IsOperational == query.IsOperational);
            }

            if (query.DateFrom is not null)
            {
                candidatesQuery = candidatesQuery.Where(
                    document => document.DateInspected >= query.DateFrom);
            }

            if (query.DateTo is not null)
            {
                candidatesQuery = candidatesQuery.Where(
                    document => document.DateInspected <= query.DateTo);
            }

            var candidates = await candidatesQuery
                .OrderByDescending(document => document.DateInspected)
                .ThenBy(document => document.InspectionId)
                .Take(SemanticMaintenanceQueryBuilder.MaxCandidateCount)
                .ToListAsync(cancellationToken);

            var queryVector = queryVectors[0].Values;
            var results = new List<SemanticMaintenanceSearchResult>(Math.Min(query.Limit, candidates.Count));
            foreach (var candidate in candidates)
            {
                if (!TryReadCurrentVector(candidate, descriptor, queryVector.Count, out var vector))
                {
                    continue;
                }

                var score = EmbeddingVectorCodec.CosineSimilarity(queryVector, vector);
                results.Add(ToResult(candidate, score));
            }

            return results
                .OrderByDescending(result => result.RawSemanticScore)
                .ThenByDescending(result => result.DateInspected)
                .ThenBy(result => result.InspectionId)
                .Take(query.Limit)
                .ToArray();
        }
        catch (SemanticMaintenanceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SemanticMaintenanceExecutionException(
                "SQL Server could not execute semantic maintenance retrieval.",
                exception);
        }
    }

    private static bool TryReadCurrentVector(
        MaintenanceSearchDocument document,
        EmbeddingServiceDescriptor descriptor,
        int expectedDimensions,
        out double[] vector)
    {
        vector = [];
        var embedding = document.Embedding;
        if (embedding is null
            || !string.Equals(
                embedding.EmbeddingProfile,
                descriptor.EmbeddingProfile,
                StringComparison.Ordinal)
            || !string.Equals(
                embedding.SourceHash,
                MaintenanceEmbeddingInput.ComputeSourceHash(document.SearchText),
                StringComparison.Ordinal)
            || (descriptor.Dimensions is not null && embedding.Dimensions != descriptor.Dimensions))
        {
            return false;
        }

        try
        {
            vector = EmbeddingVectorCodec.Parse(embedding.VectorJson, embedding.Dimensions);
            return vector.Length == expectedDimensions;
        }
        catch (EmbeddingVectorValidationException)
        {
            return false;
        }
    }

    private static SemanticMaintenanceSearchResult ToResult(
        MaintenanceSearchDocument document,
        double score)
    {
        return new SemanticMaintenanceSearchResult(
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
            score);
    }
}
