namespace UniPM.Api.Features.Retrieval;

internal interface ISemanticMaintenanceRetriever
{
    Task<IReadOnlyList<SemanticMaintenanceSearchResult>> SearchAsync(
        SemanticMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record SemanticMaintenanceSearchRequest(
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

internal sealed record SemanticMaintenanceSearchResult(
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
    double RawSemanticScore)
    : IFusedMetadataResult
{
    public const string RetrievalChannelValue = "semantic";

    public string RetrievalChannel => RetrievalChannelValue;
}

internal enum SemanticMaintenanceFailureKind
{
    Validation,
    Availability,
    Execution,
    Data
}

internal class SemanticMaintenanceException(
    SemanticMaintenanceFailureKind kind,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public SemanticMaintenanceFailureKind Kind { get; } = kind;
}

internal sealed class SemanticMaintenanceQueryValidationException(string message)
    : SemanticMaintenanceException(SemanticMaintenanceFailureKind.Validation, message);

internal sealed class SemanticMaintenanceAvailabilityException(string message, Exception? innerException = null)
    : SemanticMaintenanceException(SemanticMaintenanceFailureKind.Availability, message, innerException);

internal sealed class SemanticMaintenanceExecutionException(string message, Exception? innerException = null)
    : SemanticMaintenanceException(SemanticMaintenanceFailureKind.Execution, message, innerException);

internal sealed class SemanticMaintenanceDataException(string message, Exception? innerException = null)
    : SemanticMaintenanceException(SemanticMaintenanceFailureKind.Data, message, innerException);
