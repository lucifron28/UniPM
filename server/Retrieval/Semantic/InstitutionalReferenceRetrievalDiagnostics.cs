namespace UniPM.Api.Features.Retrieval;

internal sealed class InstitutionalReferenceRetrievalDiagnostics
{
    private readonly object gate = new();
    private InstitutionalReferenceCandidateDiagnostics latest = new(0, false, 0);

    public void Clear()
    {
        lock (gate)
        {
            latest = new InstitutionalReferenceCandidateDiagnostics(0, false, 0);
        }
    }

    public void Record(int candidateCount, bool candidateCapReached, int invalidVectorCount)
    {
        lock (gate)
        {
            latest = new InstitutionalReferenceCandidateDiagnostics(candidateCount, candidateCapReached, invalidVectorCount);
        }
    }

    internal InstitutionalReferenceCandidateDiagnostics Consume()
    {
        lock (gate)
        {
            var current = latest;
            latest = new InstitutionalReferenceCandidateDiagnostics(0, false, 0);
            return current;
        }
    }
}

internal sealed record InstitutionalReferenceCandidateDiagnostics(
    int CandidateCount,
    bool CandidateCapReached,
    int InvalidVectorCount);
