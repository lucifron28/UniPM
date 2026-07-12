using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void One_channel_result_uses_one_based_rrf_rank()
    {
        var result = ReciprocalRankFusion.Fuse([Lexical(1, 900)], [], 10);

        var fused = Assert.Single(result);
        Assert.Equal(1d / 61d, fused.FusionScore, precision: 12);
        Assert.Equal(1, fused.LexicalRank);
        Assert.Null(fused.SemanticRank);
        Assert.Equal(1, fused.MatchedChannelCount);
    }

    [Fact]
    public void Overlapping_result_receives_both_contributions_and_deduplicates()
    {
        var result = ReciprocalRankFusion.Fuse(
            [Lexical(1, 5), Lexical(2, 4)],
            [Semantic(2, 0.1), Semantic(1, 0.9)],
            10);

        Assert.Equal(2, result.Count);
        var overlap = Assert.Single(result, item => item.InspectionId == Id(2));
        Assert.Equal(1d / 62d + 1d / 61d, overlap.FusionScore, precision: 12);
        Assert.Equal(2, overlap.LexicalRank);
        Assert.Equal(1, overlap.SemanticRank);
        Assert.Equal(2, overlap.MatchedChannelCount);
    }

    [Fact]
    public void Raw_scores_do_not_change_fused_score_or_ordering()
    {
        var lowRaw = ReciprocalRankFusion.Fuse(
            [Lexical(1, 1), Lexical(2, 100000)],
            [Semantic(2, 0.001), Semantic(1, 0.999)],
            10);
        var highRaw = ReciprocalRankFusion.Fuse(
            [Lexical(1, 999999), Lexical(2, -5)],
            [Semantic(2, 999999), Semantic(1, -1)],
            10);

        Assert.Equal(lowRaw.Select(item => item.InspectionId), highRaw.Select(item => item.InspectionId));
        Assert.Equal(lowRaw.Select(item => item.FusionScore), highRaw.Select(item => item.FusionScore));
    }

    [Fact]
    public void Non_overlapping_results_are_ordered_by_score_then_deterministic_ties()
    {
        var result = ReciprocalRankFusion.Fuse(
            [Lexical(2, 1), Lexical(1, 2)],
            [Semantic(3, 0.5)],
            2);

        Assert.Equal([Id(3), Id(2)], result.Select(item => item.InspectionId));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Equal_score_ties_use_match_count_best_rank_date_then_inspection_id()
    {
        var sameDate = AtManila(2025, 1, 1);
        var result = ReciprocalRankFusion.Fuse(
            [Lexical(3, 1, sameDate), Lexical(4, 1, sameDate)],
            [Semantic(4, 1, sameDate), Semantic(2, 1, sameDate)],
            10);

        Assert.Equal([Id(4), Id(3), Id(2)], result.Select(item => item.InspectionId));
    }

    [Fact]
    public void Conflicting_metadata_for_an_overlapping_source_is_rejected()
    {
        var lexical = Lexical(1, 1);
        var semantic = Semantic(1, 0.9) with { Location = "Different location" };

        Assert.Throws<FusedMaintenanceDataIntegrityException>(
            () => ReciprocalRankFusion.Fuse([lexical], [semantic], 10));
    }

    [Fact]
    public void Duplicate_within_one_channel_is_rejected()
    {
        Assert.Throws<FusedMaintenanceDataIntegrityException>(
            () => ReciprocalRankFusion.Fuse([Lexical(1, 1), Lexical(1, 2)], [], 10));
    }

    private static LexicalMaintenanceSearchResult Lexical(
        int suffix,
        int rawRank,
        DateTimeOffset? date = null)
        => new(
            Id(suffix),
            Guid.Parse($"10000000-0000-0000-0000-0000000000{suffix:00}"),
            Guid.Parse($"20000000-0000-0000-0000-0000000000{suffix:00}"),
            $"FE-{suffix:000}",
            "fire-extinguisher",
            "Main Building",
            "GSD",
            $"Room {suffix}",
            date ?? AtManila(2025, 1, suffix),
            false,
            rawRank);

    private static SemanticMaintenanceSearchResult Semantic(
        int suffix,
        double rawScore,
        DateTimeOffset? date = null)
        => new(
            Id(suffix),
            Guid.Parse($"10000000-0000-0000-0000-0000000000{suffix:00}"),
            Guid.Parse($"20000000-0000-0000-0000-0000000000{suffix:00}"),
            $"FE-{suffix:000}",
            "fire-extinguisher",
            "Main Building",
            "GSD",
            $"Room {suffix}",
            date ?? AtManila(2025, 1, suffix),
            false,
            rawScore);

    private static Guid Id(int suffix)
        => Guid.Parse($"00000000-0000-0000-0000-0000000000{suffix:00}");

    private static DateTimeOffset AtManila(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, TimeSpan.FromHours(8));
}
