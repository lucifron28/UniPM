namespace UniPM.Api.Features.Retrieval;

internal sealed class SemanticMaintenanceRetrievalDiagnostics
{
    private readonly object gate = new();
    private SemanticMaintenanceCandidateDiagnostics? current;

    public void Clear()
    {
        lock (gate)
        {
            current = null;
        }
    }

    public void Record(int candidateCount, bool candidateCapReached)
    {
        lock (gate)
        {
            current = new SemanticMaintenanceCandidateDiagnostics(
                candidateCount,
                candidateCapReached);
        }
    }

    public SemanticMaintenanceCandidateDiagnostics Consume()
    {
        lock (gate)
        {
            var value = current ?? new SemanticMaintenanceCandidateDiagnostics(0, false);
            current = null;
            return value;
        }
    }
}

internal sealed record SemanticMaintenanceCandidateDiagnostics(
    int CandidateCount,
    bool CandidateCapReached);
