namespace UniPM.Api.Features.Retrieval;

internal static class ReciprocalRankFusion
{
    internal const string Method = "rrf";
    internal const int ReciprocalRankConstant = 60;
    internal const int MaxCandidateDepth = 100;

    public static IReadOnlyList<FusedMaintenanceSearchResult> Fuse(
        IReadOnlyList<LexicalMaintenanceSearchResult> lexicalResults,
        IReadOnlyList<SemanticMaintenanceSearchResult> semanticResults,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(lexicalResults);
        ArgumentNullException.ThrowIfNull(semanticResults);
        if (limit is < 1 or > MaxCandidateDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var candidates = new Dictionary<Guid, CandidateState>();
        AddLexicalResults(candidates, lexicalResults);
        AddSemanticResults(candidates, semanticResults);

        return candidates.Values
            .Select(candidate => candidate.ToResult())
            .OrderByDescending(result => result.FusionScore)
            .ThenByDescending(result => result.MatchedChannelCount)
            .ThenBy(result => Math.Min(
                result.LexicalRank ?? int.MaxValue,
                result.SemanticRank ?? int.MaxValue))
            .ThenByDescending(result => result.DateInspected)
            .ThenBy(result => result.InspectionId.ToString("D"), StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static void AddLexicalResults(
        IDictionary<Guid, CandidateState> candidates,
        IReadOnlyList<LexicalMaintenanceSearchResult> results)
    {
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var rank = index + 1;
            var candidate = GetOrCreate(candidates, result.InspectionId, result);
            if (candidate.LexicalRank is not null)
            {
                throw new FusedMaintenanceDataIntegrityException(
                    "Lexical retrieval returned a duplicate inspection result.");
            }

            candidate.LexicalRank = rank;
            candidate.RawLexicalRank = result.RawLexicalRank;
            candidate.FusionScore += ReciprocalContribution(rank);
        }
    }

    private static void AddSemanticResults(
        IDictionary<Guid, CandidateState> candidates,
        IReadOnlyList<SemanticMaintenanceSearchResult> results)
    {
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var rank = index + 1;
            var candidate = GetOrCreate(candidates, result.InspectionId, result);
            if (candidate.SemanticRank is not null)
            {
                throw new FusedMaintenanceDataIntegrityException(
                    "Semantic retrieval returned a duplicate inspection result.");
            }

            candidate.SemanticRank = rank;
            candidate.RawSemanticScore = result.RawSemanticScore;
            candidate.FusionScore += ReciprocalContribution(rank);
        }
    }

    private static double ReciprocalContribution(int rank)
        => 1d / (ReciprocalRankConstant + rank);

    private static CandidateState GetOrCreate<T>(
        IDictionary<Guid, CandidateState> candidates,
        Guid inspectionId,
        T result)
        where T : IFusedMetadataResult
    {
        if (candidates.TryGetValue(inspectionId, out var existing))
        {
            existing.VerifyMetadata(result);
            return existing;
        }

        var created = new CandidateState(result);
        candidates.Add(inspectionId, created);
        return created;
    }

    private sealed class CandidateState
    {
        public CandidateState(IFusedMetadataResult result)
        {
            InspectionId = result.InspectionId;
            AssetId = result.AssetId;
            ScheduleId = result.ScheduleId;
            AssetCode = result.AssetCode;
            AssetCategory = result.AssetCategory;
            Building = result.Building;
            Department = result.Department;
            Location = result.Location;
            DateInspected = result.DateInspected;
            IsOperational = result.IsOperational;
        }

        public Guid InspectionId { get; }
        public Guid AssetId { get; }
        public Guid ScheduleId { get; }
        public string AssetCode { get; }
        public string AssetCategory { get; }
        public string? Building { get; }
        public string? Department { get; }
        public string? Location { get; }
        public DateTimeOffset DateInspected { get; }
        public bool IsOperational { get; }
        public double FusionScore { get; set; }
        public int? LexicalRank { get; set; }
        public int? SemanticRank { get; set; }
        public int? RawLexicalRank { get; set; }
        public double? RawSemanticScore { get; set; }
        public int BestComponentRank => Math.Min(LexicalRank ?? int.MaxValue, SemanticRank ?? int.MaxValue);

        public void VerifyMetadata(IFusedMetadataResult result)
        {
            if (AssetId != result.AssetId
                || ScheduleId != result.ScheduleId
                || !string.Equals(AssetCode, result.AssetCode, StringComparison.Ordinal)
                || !string.Equals(AssetCategory, result.AssetCategory, StringComparison.Ordinal)
                || !string.Equals(Building, result.Building, StringComparison.Ordinal)
                || !string.Equals(Department, result.Department, StringComparison.Ordinal)
                || !string.Equals(Location, result.Location, StringComparison.Ordinal)
                || DateInspected != result.DateInspected
                || IsOperational != result.IsOperational)
            {
                throw new FusedMaintenanceDataIntegrityException(
                    "Retrieval channels returned conflicting source metadata.");
            }
        }

        public FusedMaintenanceSearchResult ToResult()
            => new(
                InspectionId,
                AssetId,
                ScheduleId,
                AssetCode,
                AssetCategory,
                Building,
                Department,
                Location,
                DateInspected,
                IsOperational,
                FusionScore,
                LexicalRank,
                SemanticRank,
                RawLexicalRank,
                RawSemanticScore,
                (LexicalRank is not null ? 1 : 0) + (SemanticRank is not null ? 1 : 0));
    }
}
