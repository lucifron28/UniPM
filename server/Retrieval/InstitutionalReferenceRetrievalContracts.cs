using UniPM.Api.Features.ReferenceDocuments;

namespace UniPM.Api.Features.Retrieval;

internal interface ILexicalInstitutionalReferenceRetriever
{
    Task<IReadOnlyList<InstitutionalReferenceLexicalSearchResult>> SearchAsync(
        InstitutionalReferenceSearchRequest request,
        CancellationToken cancellationToken = default);
}

internal interface ISemanticInstitutionalReferenceRetriever
{
    Task<IReadOnlyList<InstitutionalReferenceSemanticSearchResult>> SearchAsync(
        InstitutionalReferenceSearchRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record InstitutionalReferenceSearchRequest(
    string Query,
    string AssetCategory,
    DateOnly AsOfDate,
    int? Limit = null);

internal sealed record InstitutionalReferenceQuery(
    string NormalizedQuery,
    string SearchCondition,
    string AssetCategory,
    DateOnly AsOfDate,
    int Limit);

internal abstract record InstitutionalReferenceSearchResult(
    Guid DocumentId,
    Guid SectionId,
    string SourceKey,
    string Revision,
    string Title,
    string PublisherAuthority,
    DateOnly? EffectiveDate,
    int SectionSequence,
    string SectionHeading,
    string SectionText,
    string SourceLocator,
    int? PageStart,
    int? PageEnd,
    InstitutionalReferenceApplicabilityMatch ApplicabilityMatch,
    string? MatchedScopeLabel)
{
    public string EvidenceSourceGroup => EvidenceSourceGroupCatalog.InstitutionalReference;
}

internal sealed record InstitutionalReferenceLexicalSearchResult(
    Guid DocumentId,
    Guid SectionId,
    string SourceKey,
    string Revision,
    string Title,
    string PublisherAuthority,
    DateOnly? EffectiveDate,
    int SectionSequence,
    string SectionHeading,
    string SectionText,
    string SourceLocator,
    int? PageStart,
    int? PageEnd,
    InstitutionalReferenceApplicabilityMatch ApplicabilityMatch,
    string? MatchedScopeLabel,
    int RawLexicalRank)
    : InstitutionalReferenceSearchResult(
        DocumentId,
        SectionId,
        SourceKey,
        Revision,
        Title,
        PublisherAuthority,
        EffectiveDate,
        SectionSequence,
        SectionHeading,
        SectionText,
        SourceLocator,
        PageStart,
        PageEnd,
        ApplicabilityMatch,
        MatchedScopeLabel);

internal sealed record InstitutionalReferenceSemanticSearchResult(
    Guid DocumentId,
    Guid SectionId,
    string SourceKey,
    string Revision,
    string Title,
    string PublisherAuthority,
    DateOnly? EffectiveDate,
    int SectionSequence,
    string SectionHeading,
    string SectionText,
    string SourceLocator,
    int? PageStart,
    int? PageEnd,
    InstitutionalReferenceApplicabilityMatch ApplicabilityMatch,
    string? MatchedScopeLabel,
    double RawSemanticScore)
    : InstitutionalReferenceSearchResult(
        DocumentId,
        SectionId,
        SourceKey,
        Revision,
        Title,
        PublisherAuthority,
        EffectiveDate,
        SectionSequence,
        SectionHeading,
        SectionText,
        SourceLocator,
        PageStart,
        PageEnd,
        ApplicabilityMatch,
        MatchedScopeLabel);

internal enum InstitutionalReferenceApplicabilityMatch
{
    CategorySpecific,
    CategoryWide
}

internal enum InstitutionalReferenceRetrievalFailureKind
{
    Validation,
    Availability,
    Execution,
    Data
}

internal class InstitutionalReferenceRetrievalException(
    InstitutionalReferenceRetrievalFailureKind kind,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public InstitutionalReferenceRetrievalFailureKind Kind { get; } = kind;
}

internal sealed class InstitutionalReferenceQueryValidationException(string message)
    : InstitutionalReferenceRetrievalException(InstitutionalReferenceRetrievalFailureKind.Validation, message);

internal sealed class InstitutionalReferenceAvailabilityException(string message, Exception? innerException = null)
    : InstitutionalReferenceRetrievalException(InstitutionalReferenceRetrievalFailureKind.Availability, message, innerException);

internal sealed class InstitutionalReferenceExecutionException(string message, Exception? innerException = null)
    : InstitutionalReferenceRetrievalException(InstitutionalReferenceRetrievalFailureKind.Execution, message, innerException);

internal sealed class InstitutionalReferenceDataException(string message, Exception? innerException = null)
    : InstitutionalReferenceRetrievalException(InstitutionalReferenceRetrievalFailureKind.Data, message, innerException);
