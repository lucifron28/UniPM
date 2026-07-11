using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Retrieval;

internal interface IMaintenanceSearchDocumentEmbeddingIndexer
{
    Task<MaintenanceEmbeddingIndexResult> RebuildAsync(
        CancellationToken cancellationToken = default);
}

internal sealed class MaintenanceSearchDocumentEmbeddingIndexer(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEmbeddingService embeddingService,
    IOptions<EmbeddingOptions> optionsAccessor)
    : IMaintenanceSearchDocumentEmbeddingIndexer
{
    private readonly EmbeddingOptions options = optionsAccessor.Value;

    public async Task<MaintenanceEmbeddingIndexResult> RebuildAsync(
        CancellationToken cancellationToken = default)
    {
        var descriptor = embeddingService.Descriptor;
        if (!descriptor.Enabled)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Semantic embeddings are disabled by configuration.");
        }

        if (options.MaxBatchSize is < 1 or > 128)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:MaxBatchSize is outside the supported bounds.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var total = await context.MaintenanceSearchDocuments.CountAsync(cancellationToken);
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;
        var offset = 0;

        while (true)
        {
            var documents = await context.MaintenanceSearchDocuments
                .AsNoTracking()
                .OrderBy(document => document.InspectionId)
                .Skip(offset)
                .Take(options.MaxBatchSize)
                .ToListAsync(cancellationToken);

            if (documents.Count == 0)
            {
                break;
            }

            offset += documents.Count;
            var inspectionIds = documents.Select(document => document.InspectionId).ToArray();
            var existingEmbeddings = await context.MaintenanceSearchDocumentEmbeddings
                .AsNoTracking()
                .Where(embedding => inspectionIds.Contains(embedding.InspectionId))
                .ToDictionaryAsync(embedding => embedding.InspectionId, cancellationToken);

            var pending = new List<PendingEmbedding>();
            foreach (var document in documents)
            {
                var sourceHash = MaintenanceEmbeddingInput.ComputeSourceHash(document.SearchText);
                if (existingEmbeddings.TryGetValue(document.InspectionId, out var existing)
                    && IsCurrent(existing, sourceHash, descriptor))
                {
                    skipped++;
                    continue;
                }

                pending.Add(new PendingEmbedding(document, sourceHash, existing));
            }

            foreach (var batch in pending.Chunk(options.MaxBatchSize))
            {
                IReadOnlyList<EmbeddingVector> vectors;
                try
                {
                    vectors = await embeddingService.GenerateBatchAsync(
                        batch.Select(item => MaintenanceEmbeddingInput.NormalizeDocumentText(item.Document.SearchText)).ToArray(),
                        cancellationToken);

                    if (vectors.Count != batch.Length)
                    {
                        throw new EmbeddingVectorValidationException(
                            "The embedding provider returned a vector count different from the requested batch.");
                    }

                    if (descriptor.Dimensions is not null
                        && vectors.Any(vector => vector.Dimensions != descriptor.Dimensions))
                    {
                        throw new EmbeddingVectorValidationException(
                            "The embedding provider returned dimensions different from its descriptor.");
                    }

                    for (var index = 0; index < batch.Length; index++)
                    {
                        var vector = vectors[index];

                        var target = batch[index].Existing;
                        if (target is null)
                        {
                            context.MaintenanceSearchDocumentEmbeddings.Add(
                                CreateEmbedding(batch[index], vector, descriptor));
                            created++;
                        }
                        else
                        {
                            Apply(target, batch[index], vector, descriptor);
                            context.MaintenanceSearchDocumentEmbeddings.Update(target);
                            updated++;
                        }
                    }

                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (EmbeddingServiceException exception)
                    when (exception.Kind is EmbeddingFailureKind.Execution or EmbeddingFailureKind.Validation)
                {
                    failed += batch.Length;
                }
            }
        }

        return new MaintenanceEmbeddingIndexResult(total, created, updated, skipped, failed);
    }

    private static bool IsCurrent(
        MaintenanceSearchDocumentEmbedding embedding,
        string sourceHash,
        EmbeddingServiceDescriptor descriptor)
    {
        return string.Equals(embedding.SourceHash, sourceHash, StringComparison.Ordinal)
            && string.Equals(embedding.EmbeddingProfile, descriptor.EmbeddingProfile, StringComparison.Ordinal)
            && (descriptor.Dimensions is null || embedding.Dimensions == descriptor.Dimensions);
    }

    private static MaintenanceSearchDocumentEmbedding CreateEmbedding(
        PendingEmbedding pending,
        EmbeddingVector vector,
        EmbeddingServiceDescriptor descriptor)
    {
        var embedding = new MaintenanceSearchDocumentEmbedding
        {
            InspectionId = pending.Document.InspectionId
        };
        Apply(embedding, pending, vector, descriptor);
        return embedding;
    }

    private static void Apply(
        MaintenanceSearchDocumentEmbedding target,
        PendingEmbedding pending,
        EmbeddingVector vector,
        EmbeddingServiceDescriptor descriptor)
    {
        target.ProviderKey = descriptor.ProviderKey;
        target.ModelKey = descriptor.ModelKey;
        target.EmbeddingProfile = descriptor.EmbeddingProfile;
        target.Dimensions = vector.Dimensions;
        target.VectorJson = EmbeddingVectorCodec.Serialize(vector.Values);
        target.SourceHash = pending.SourceHash;
        target.GeneratedAt = DateTimeOffset.UtcNow;
    }

    private sealed record PendingEmbedding(
        MaintenanceSearchDocument Document,
        string SourceHash,
        MaintenanceSearchDocumentEmbedding? Existing);
}

internal sealed record MaintenanceEmbeddingIndexResult(
    int Total,
    int Created,
    int Updated,
    int Skipped,
    int Failed);
