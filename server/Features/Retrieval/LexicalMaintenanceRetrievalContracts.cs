namespace UniPM.Api.Features.Retrieval;

internal interface ILexicalMaintenanceRetriever
{
    Task<IReadOnlyList<LexicalMaintenanceSearchResult>> SearchAsync(
        LexicalMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record LexicalMaintenanceSearchRequest(
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

internal sealed record LexicalMaintenanceSearchResult(
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
    int RawLexicalRank,
    string RetrievalChannel = "lexical");

internal enum LexicalMaintenanceRetrievalFailureKind
{
    Validation,
    Availability,
    Execution
}

internal class LexicalMaintenanceRetrievalException(
    LexicalMaintenanceRetrievalFailureKind kind,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public LexicalMaintenanceRetrievalFailureKind Kind { get; } = kind;
}

internal sealed class LexicalMaintenanceQueryValidationException(string message)
    : LexicalMaintenanceRetrievalException(
        LexicalMaintenanceRetrievalFailureKind.Validation,
        message);

internal sealed class LexicalMaintenanceAvailabilityException(string message, Exception? innerException = null)
    : LexicalMaintenanceRetrievalException(
        LexicalMaintenanceRetrievalFailureKind.Availability,
        message,
        innerException);

internal sealed class LexicalMaintenanceExecutionException(string message, Exception? innerException = null)
    : LexicalMaintenanceRetrievalException(
        LexicalMaintenanceRetrievalFailureKind.Execution,
        message,
        innerException);
