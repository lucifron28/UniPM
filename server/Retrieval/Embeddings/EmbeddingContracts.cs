namespace UniPM.Api.Features.Retrieval;

internal interface IEmbeddingService
{
    EmbeddingServiceDescriptor Descriptor { get; }

    Task<IReadOnlyList<EmbeddingVector>> GenerateBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}

internal sealed record EmbeddingServiceDescriptor(
    bool Enabled,
    string ProviderKey,
    string ModelKey,
    int? Dimensions,
    string EmbeddingProfile);

internal sealed class EmbeddingVector
{
    public EmbeddingVector(IReadOnlyList<double> values)
    {
        Values = EmbeddingVectorCodec.Normalize(values);
    }

    public IReadOnlyList<double> Values { get; }
    public int Dimensions => Values.Count;
}

internal enum EmbeddingFailureKind
{
    Availability,
    Execution,
    Validation
}

internal class EmbeddingServiceException(
    EmbeddingFailureKind kind,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public EmbeddingFailureKind Kind { get; } = kind;
}

internal sealed class EmbeddingServiceAvailabilityException(string message, Exception? innerException = null)
    : EmbeddingServiceException(EmbeddingFailureKind.Availability, message, innerException);

internal sealed class EmbeddingServiceExecutionException(string message, Exception? innerException = null)
    : EmbeddingServiceException(EmbeddingFailureKind.Execution, message, innerException);

internal sealed class EmbeddingVectorValidationException(string message, Exception? innerException = null)
    : EmbeddingServiceException(EmbeddingFailureKind.Validation, message, innerException);
