namespace UniPM.Api.Features.Retrieval;

internal sealed class SemanticMaintenanceRetrievalDiagnostics
{
    private readonly AsyncLocal<SemanticMaintenanceCandidateDiagnostics?> current = new();

    public void Clear() => current.Value = null;

    public void Record(int candidateCount, bool candidateCapReached)
        => current.Value = new SemanticMaintenanceCandidateDiagnostics(
            candidateCount,
            candidateCapReached);

    public SemanticMaintenanceCandidateDiagnostics Consume()
    {
        var value = current.Value ?? new SemanticMaintenanceCandidateDiagnostics(0, false);
        current.Value = null;
        return value;
    }
}

internal sealed record SemanticMaintenanceCandidateDiagnostics(
    int CandidateCount,
    bool CandidateCapReached);
