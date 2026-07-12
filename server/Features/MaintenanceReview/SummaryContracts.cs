namespace UniPM.Api.Features.MaintenanceReview;

internal interface ISummaryService
{
    SummaryServiceDescriptor Descriptor { get; }

    Task<SummaryGenerationResult> GenerateAsync(
        SummaryGenerationRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record SummaryServiceDescriptor(
    bool Enabled,
    string ProviderKey,
    string ModelKey);

internal sealed record SummaryGenerationRequest(
    string Prompt,
    IReadOnlySet<string> SourceLabels);

internal sealed record SummaryGenerationResult(string Content);

internal enum SummaryServiceFailureKind
{
    Availability,
    Execution,
    Data
}

internal class SummaryServiceException(
    SummaryServiceFailureKind kind,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public SummaryServiceFailureKind Kind { get; } = kind;
}

internal sealed class SummaryServiceAvailabilityException(string message, Exception? innerException = null)
    : SummaryServiceException(SummaryServiceFailureKind.Availability, message, innerException);

internal sealed class SummaryServiceExecutionException(string message, Exception? innerException = null)
    : SummaryServiceException(SummaryServiceFailureKind.Execution, message, innerException);

internal sealed class SummaryServiceDataException(string message, Exception? innerException = null)
    : SummaryServiceException(SummaryServiceFailureKind.Data, message, innerException);
