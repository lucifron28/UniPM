using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceDocuments;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Retrieval;

internal sealed class SqlServerSemanticInstitutionalReferenceRetriever(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEmbeddingService embeddingService,
    InstitutionalReferenceRetrievalDiagnostics? diagnostics = null)
    : ISemanticInstitutionalReferenceRetriever
{
    internal const int MaxCandidateCount = 500;

    public async Task<IReadOnlyList<InstitutionalReferenceSemanticSearchResult>> SearchAsync(
        InstitutionalReferenceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = InstitutionalReferenceQueryBuilder.Build(request);
        var descriptor = embeddingService.Descriptor;
        if (!descriptor.Enabled || string.IsNullOrWhiteSpace(descriptor.ProviderKey)
            || string.IsNullOrWhiteSpace(descriptor.ModelKey) || descriptor.Dimensions is null)
        {
            throw new InstitutionalReferenceAvailabilityException(
                "Semantic embeddings must be configured before institutional reference retrieval.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!context.Database.IsSqlServer())
        {
            throw new InstitutionalReferenceAvailabilityException(
                "Institutional semantic retrieval requires the SQL Server EF Core provider.");
        }

        try
        {
            var profile = InstitutionalReferenceEmbeddingInput.BuildProfile(descriptor);
            var load = await LoadCandidatesAsync(context, query, descriptor, profile, cancellationToken);
            diagnostics?.Record(load.Candidates.Count, load.CandidateCapReached, load.InvalidVectorCount);
            if (load.Candidates.Count == 0)
            {
                return [];
            }

            IReadOnlyList<EmbeddingVector> vectors;
            try
            {
                vectors = await embeddingService.GenerateBatchAsync(
                    [InstitutionalReferenceEmbeddingInput.BuildQueryInput(query.NormalizedQuery)],
                    cancellationToken);
            }
            catch (EmbeddingServiceAvailabilityException exception)
            {
                throw new InstitutionalReferenceAvailabilityException("The embedding provider is unavailable for institutional reference retrieval.", exception);
            }
            catch (EmbeddingServiceExecutionException exception)
            {
                throw new InstitutionalReferenceExecutionException("The embedding provider failed while generating an institutional reference query vector.", exception);
            }
            catch (EmbeddingVectorValidationException exception)
            {
                throw new InstitutionalReferenceDataException("The embedding provider returned invalid institutional reference query vector data.", exception);
            }

            if (vectors.Count != 1 || vectors[0].Dimensions != descriptor.Dimensions)
            {
                throw new InstitutionalReferenceDataException("The embedding provider returned an invalid institutional reference query vector.");
            }

            return load.Candidates
                .Select(candidate => ToResult(candidate.Section, candidate.Document, query.AssetCategory,
                    EmbeddingVectorCodec.CosineSimilarity(vectors[0].Values, candidate.Vector)))
                .OrderByDescending(result => result.RawSemanticScore)
                .ThenByDescending(result => result.EffectiveDate.HasValue)
                .ThenByDescending(result => result.EffectiveDate)
                .ThenBy(result => result.SourceKey, StringComparer.Ordinal)
                .ThenBy(result => result.Revision, StringComparer.Ordinal)
                .ThenBy(result => result.SectionSequence)
                .ThenBy(result => result.SectionId)
                .Take(query.Limit)
                .ToArray();
        }
        catch (InstitutionalReferenceRetrievalException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InstitutionalReferenceExecutionException(
                "SQL Server could not execute institutional semantic retrieval.", exception);
        }
    }

    private static async Task<CandidateLoad> LoadCandidatesAsync(
        ApplicationDbContext context,
        InstitutionalReferenceQuery query,
        EmbeddingServiceDescriptor descriptor,
        string profile,
        CancellationToken cancellationToken)
    {
        var maximumCollected = MaxCandidateCount + 1;
        var candidatesQuery = context.ReferenceDocumentSections
            .AsNoTracking()
            .Include(section => section.ReferenceDocument)
            .Include(section => section.Embedding)
            .Where(section => section.ReferenceDocument != null
                && section.ReferenceDocument.SourceType == ReferenceDocumentSourceTypeCatalog.Institutional
                && section.ReferenceDocument.LifecycleStatus == ReferenceDocumentLifecycleCatalog.Active
                && (section.ReferenceDocument.EffectiveDate == null || section.ReferenceDocument.EffectiveDate <= query.AsOfDate)
                && section.ReferenceDocument.Applicabilities.Any(applicability =>
                    applicability.AssetCategory == query.AssetCategory || applicability.AssetCategory == null)
                && section.Embedding != null
                && section.Embedding.EmbeddingProfile == profile
                && section.Embedding.Dimensions == descriptor.Dimensions);

        var eligible = new List<Candidate>(maximumCollected);
        var invalidVectorCount = 0;
        var offset = 0;
        while (eligible.Count < maximumCollected)
        {
            var page = await candidatesQuery
                .OrderByDescending(section => section.ReferenceDocument!.EffectiveDate.HasValue)
                .ThenByDescending(section => section.ReferenceDocument!.EffectiveDate)
                .ThenBy(section => section.ReferenceDocument!.SourceKey)
                .ThenBy(section => section.ReferenceDocument!.Revision)
                .ThenBy(section => section.Sequence)
                .ThenBy(section => section.Id)
                .Skip(offset)
                .Take(MaxCandidateCount)
                .ToListAsync(cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            offset += page.Count;
            foreach (var section in page)
            {
                if (TryReadCurrentVector(section, section.ReferenceDocument!, descriptor, profile, out var vector))
                {
                    eligible.Add(new Candidate(section, section.ReferenceDocument!, vector));
                    if (eligible.Count == maximumCollected)
                    {
                        break;
                    }
                }
                else
                {
                    invalidVectorCount++;
                }
            }
        }

        var capReached = eligible.Count > MaxCandidateCount;
        return new CandidateLoad(
            capReached ? eligible.Take(MaxCandidateCount).ToArray() : eligible,
            capReached,
            invalidVectorCount);
    }

    private static bool TryReadCurrentVector(
        ReferenceDocumentSection section,
        ReferenceDocument document,
        EmbeddingServiceDescriptor descriptor,
        string profile,
        out double[] vector)
    {
        vector = [];
        var embedding = section.Embedding;
        if (embedding is null
            || !string.Equals(embedding.ProviderKey, descriptor.ProviderKey, StringComparison.Ordinal)
            || !string.Equals(embedding.ModelKey, descriptor.ModelKey, StringComparison.Ordinal)
            || !string.Equals(embedding.EmbeddingProfile, profile, StringComparison.Ordinal)
            || embedding.Dimensions != descriptor.Dimensions
            || !string.Equals(embedding.SectionHash,
                InstitutionalReferenceEmbeddingInput.ComputeSectionSourceHash(section, document),
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            vector = EmbeddingVectorCodec.Parse(embedding.VectorJson, embedding.Dimensions);
            return vector.Length == descriptor.Dimensions;
        }
        catch (EmbeddingVectorValidationException)
        {
            return false;
        }
    }

    private static InstitutionalReferenceSemanticSearchResult ToResult(
        ReferenceDocumentSection section,
        ReferenceDocument document,
        string category,
        double score)
        => new(
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
            category,
            score);

    private sealed record Candidate(ReferenceDocumentSection Section, ReferenceDocument Document, double[] Vector);
    private sealed record CandidateLoad(
        IReadOnlyList<Candidate> Candidates,
        bool CandidateCapReached,
        int InvalidVectorCount);
}
