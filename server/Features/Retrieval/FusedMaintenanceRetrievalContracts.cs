namespace UniPM.Api.Features.Retrieval;

internal interface IFusedMaintenanceRetriever
{
    Task<FusedMaintenanceSearchResponse> SearchAsync(
        FusedMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record FusedMaintenanceSearchRequest(
    string Query,
    int? Limit = null,
    Guid? AssetId = null,
    string? AssetCategory = null,
    string? Building = null,
    string? Department = null,
    string? Location = null,
    bool? IsOperational = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null);

internal interface IFusedMetadataResult
{
    Guid InspectionId { get; }
    Guid AssetId { get; }
    Guid ScheduleId { get; }
    string AssetCode { get; }
    string AssetCategory { get; }
    string? Building { get; }
    string? Department { get; }
    string? Location { get; }
    DateTimeOffset DateInspected { get; }
    bool IsOperational { get; }
}

internal enum FusedRetrievalChannelStatus
{
    Success,
    Empty,
    Unavailable,
    Failed
}

internal sealed record FusedRetrievalChannelExecution(
    string Channel,
    FusedRetrievalChannelStatus Status,
    int ResultCount);

internal sealed record FusedMaintenanceSearchResponse(
    IReadOnlyList<FusedMaintenanceSearchResult> Results,
    FusedRetrievalChannelExecution Lexical,
    FusedRetrievalChannelExecution Semantic,
    bool IsDegraded,
    string FusionMethod,
    int ReciprocalRankConstant,
    int CandidateLimit);

internal sealed record FusedMaintenanceSearchResult(
    Guid InspectionId,
    Guid AssetId,
    Guid ScheduleId,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Department,
    string? Location,
    DateTimeOffset DateInspected,
    bool IsOperational,
    double FusionScore,
    int? LexicalRank,
    int? SemanticRank,
    int? RawLexicalRank,
    double? RawSemanticScore,
    int MatchedChannelCount)
{
    public const string RetrievalChannelValue = "fused";

    public string RetrievalChannel => RetrievalChannelValue;
}

internal enum FusedMaintenanceRetrievalFailureKind
{
    Validation,
    Availability,
    Execution,
    Data
}

internal class FusedMaintenanceRetrievalException(
    FusedMaintenanceRetrievalFailureKind kind,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public FusedMaintenanceRetrievalFailureKind Kind { get; } = kind;
}

internal sealed class FusedMaintenanceQueryValidationException(string message)
    : FusedMaintenanceRetrievalException(
        FusedMaintenanceRetrievalFailureKind.Validation,
        message);

internal sealed class FusedMaintenanceAvailabilityException(string message, Exception? innerException = null)
    : FusedMaintenanceRetrievalException(
        FusedMaintenanceRetrievalFailureKind.Availability,
        message,
        innerException);

internal sealed class FusedMaintenanceExecutionException(string message, Exception? innerException = null)
    : FusedMaintenanceRetrievalException(
        FusedMaintenanceRetrievalFailureKind.Execution,
        message,
        innerException);

internal sealed class FusedMaintenanceDataIntegrityException(string message)
    : FusedMaintenanceRetrievalException(
        FusedMaintenanceRetrievalFailureKind.Data,
        message);
