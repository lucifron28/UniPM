using UniPM.PmAnalytics.V3Evaluation;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3EvaluationHarnessTests
{
    [Fact]
    public void V3_corpus_validation_and_scoring_fail_closed_while_labels_are_unreviewed()
    {
        var root = V3EvaluationHarness.FindRepositoryRoot();
        var validation = V3EvaluationHarness.Validate(root);
        var diagnosis = V3EvaluationHarness.Diagnose(root);
        var score = V3EvaluationHarness.Score(root);

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        Assert.Equal(18, validation.CaseCount);
        Assert.Equal(18, validation.PendingReviewCount);
        Assert.Equal(18, diagnosis.CaseCount);
        Assert.Equal(0, diagnosis.ExitCode);
        Assert.Contains("not accuracy scores", diagnosis.ScoringStatus);
        Assert.False(diagnosis.Cases.Single(item => item.CaseId == "V3D-018").UnsafeAcceptance);
        Assert.Equal("Unsupported", diagnosis.Cases.Single(item => item.CaseId == "V3D-018").ActualStatus);
        Assert.Equal("Unsupported", diagnosis.Cases.Single(item => item.CaseId == "V3D-017").ActualStatus);
        Assert.False(score.ScoreProduced);
        Assert.Equal(18, score.PendingReviewCount);
        Assert.Equal(3, score.ExitCode);
        Assert.Contains("human review", score.Message);
    }
}
