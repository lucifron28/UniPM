using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceDocuments;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Retrieval;

internal interface IInstitutionalReferenceEmbeddingIndexer
{
    Task<InstitutionalReferenceEmbeddingIndexResult> RebuildAsync(
        CancellationToken cancellationToken = default);
}

internal sealed class InstitutionalReferenceEmbeddingIndexer(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEmbeddingService embeddingService,
    IOptions<EmbeddingOptions> optionsAccessor)
    : IInstitutionalReferenceEmbeddingIndexer
{
    private readonly EmbeddingOptions options = optionsAccessor.Value;

    public async Task<InstitutionalReferenceEmbeddingIndexResult> RebuildAsync(
        CancellationToken cancellationToken = default)
    {
        var descriptor = embeddingService.Descriptor;
        if (!descriptor.Enabled
            || string.IsNullOrWhiteSpace(descriptor.ProviderKey)
            || string.IsNullOrWhiteSpace(descriptor.ModelKey)
            || descriptor.Dimensions is null)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Semantic embeddings must be configured before institutional reference indexing.");
        }

        if (options.MaxBatchSize is < 1 or > 128)
        {
            throw new EmbeddingServiceAvailabilityException("Embeddings:MaxBatchSize is outside the supported bounds.");
        }

        var profile = InstitutionalReferenceEmbeddingInput.BuildProfile(descriptor);
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.ReferenceDocumentSections
            .Include(section => section.ReferenceDocument)
            .Include(section => section.Embedding)
            .Where(section => section.ReferenceDocument != null
                && section.ReferenceDocument.SourceType == ReferenceDocumentSourceTypeCatalog.Institutional
                && section.ReferenceDocument.LifecycleStatus == ReferenceDocumentLifecycleCatalog.Active
                && (section.ReferenceDocument.EffectiveDate == null || section.ReferenceDocument.EffectiveDate <= asOfDate)
                && section.ReferenceDocument.Applicabilities.Any());

        var total = await query.CountAsync(cancellationToken);
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;
        var offset = 0;
        while (true)
        {
            var sections = await query.AsNoTracking()
                .OrderBy(section => section.Id)
                .Skip(offset)
                .Take(options.MaxBatchSize)
                .ToListAsync(cancellationToken);
            if (sections.Count == 0)
            {
                break;
            }

            offset += sections.Count;
            var pending = sections.Select(section =>
            {
                var document = section.ReferenceDocument!;
                var sourceHash = InstitutionalReferenceEmbeddingInput.ComputeSectionSourceHash(section, document);
                return new Pending(section, document, sourceHash, section.Embedding);
            }).Where(item => !IsCurrent(item.Existing, item.SourceHash, descriptor, profile)).ToArray();
            skipped += sections.Count - pending.Length;

            foreach (var batch in pending.Chunk(options.MaxBatchSize))
            {
                try
                {
                    var vectors = await embeddingService.GenerateBatchAsync(
                        batch.Select(item => InstitutionalReferenceEmbeddingInput.BuildDocumentInput(item.Section, item.Document)).ToArray(),
                        cancellationToken);
                    if (vectors.Count != batch.Length || vectors.Any(vector => vector.Dimensions != descriptor.Dimensions))
                    {
                        throw new EmbeddingVectorValidationException("The embedding provider returned incompatible institutional reference vectors.");
                    }

                    foreach (var (pendingItem, vector) in batch.Zip(vectors))
                    {
                        var target = pendingItem.Existing ?? new ReferenceDocumentSectionEmbedding
                        {
                            ReferenceDocumentSectionId = pendingItem.Section.Id
                        };
                        target.ProviderKey = descriptor.ProviderKey;
                        target.ModelKey = descriptor.ModelKey;
                        target.EmbeddingProfile = profile;
                        target.Dimensions = vector.Dimensions;
                        target.VectorJson = EmbeddingVectorCodec.Serialize(vector.Values);
                        target.SectionHash = pendingItem.SourceHash;
                        target.GeneratedAt = DateTimeOffset.UtcNow;
                        if (pendingItem.Existing is null)
                        {
                            context.ReferenceDocumentSectionEmbeddings.Add(target);
                            created++;
                        }
                        else
                        {
                            context.ReferenceDocumentSectionEmbeddings.Update(target);
                            updated++;
                        }
                    }

                    await context.SaveChangesAsync(cancellationToken);
                    context.ChangeTracker.Clear();
                }
                catch (EmbeddingServiceException exception)
                    when (exception.Kind is EmbeddingFailureKind.Execution or EmbeddingFailureKind.Validation)
                {
                    failed += batch.Length;
                }
            }
        }

        return new InstitutionalReferenceEmbeddingIndexResult(total, created, updated, skipped, failed);
    }

    private static bool IsCurrent(
        ReferenceDocumentSectionEmbedding? embedding,
        string sourceHash,
        EmbeddingServiceDescriptor descriptor,
        string profile)
        => embedding is not null
            && string.Equals(embedding.SectionHash, sourceHash, StringComparison.Ordinal)
            && string.Equals(embedding.ProviderKey, descriptor.ProviderKey, StringComparison.Ordinal)
            && string.Equals(embedding.ModelKey, descriptor.ModelKey, StringComparison.Ordinal)
            && string.Equals(embedding.EmbeddingProfile, profile, StringComparison.Ordinal)
            && embedding.Dimensions == descriptor.Dimensions;

    private sealed record Pending(
        ReferenceDocumentSection Section,
        ReferenceDocument Document,
        string SourceHash,
        ReferenceDocumentSectionEmbedding? Existing);
}

internal sealed record InstitutionalReferenceEmbeddingIndexResult(
    int Total,
    int Created,
    int Updated,
    int Skipped,
    int Failed);
