using System.Text.Json.Nodes;
using UniPM.PmAnalytics.V3Evaluation;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3EvaluationHarnessTests
{
    private static readonly string[] EvaluationFiles =
    [
        "corpus-manifest.json",
        "dev-candidates.jsonl",
        "review-decisions.json",
        "synthetic-calculation-fixtures.json"
    ];

    [Fact]
    public void V3_corpus_validation_and_scoring_fail_closed_while_fixture_labels_are_unreviewed()
    {
        var repositoryRoot = V3EvaluationHarness.FindRepositoryRoot();
        var fixtureDirectory = Directory.CreateTempSubdirectory("unipm-v3-review-gate-");

        try
        {
            CopyEvaluationFiles(repositoryRoot, fixtureDirectory.FullName);
            MarkFirstReviewPending(fixtureDirectory.FullName);

            var validation = V3EvaluationHarness.Validate(fixtureDirectory.FullName);
            var diagnosis = V3EvaluationHarness.Diagnose(fixtureDirectory.FullName);
            var score = V3EvaluationHarness.Score(fixtureDirectory.FullName);

            Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
            Assert.Equal(18, validation.CaseCount);
            Assert.Equal(1, validation.PendingReviewCount);
            Assert.Equal(18, diagnosis.CaseCount);
            Assert.Equal(0, diagnosis.ExitCode);
            Assert.Contains("not accuracy scores", diagnosis.ScoringStatus);
            Assert.False(diagnosis.Cases.Single(item => item.CaseId == "V3D-018").UnsafeAcceptance);
            Assert.Equal("Unsupported", diagnosis.Cases.Single(item => item.CaseId == "V3D-018").ActualStatus);
            Assert.Equal("Unsupported", diagnosis.Cases.Single(item => item.CaseId == "V3D-017").ActualStatus);
            Assert.False(score.ScoreProduced);
            Assert.Equal(1, score.PendingReviewCount);
            Assert.Equal(3, score.ExitCode);
            Assert.Contains("human review", score.Message);
        }
        finally
        {
            fixtureDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void V3_frozen_researcher_reviewed_corpus_is_accepted()
    {
        var root = V3EvaluationHarness.FindRepositoryRoot();
        var validation = V3EvaluationHarness.Validate(root);
        var score = V3EvaluationHarness.Score(root);

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        Assert.Equal(18, validation.CaseCount);
        Assert.Equal(0, validation.PendingReviewCount);
        Assert.True(score.ScoreProduced, score.Message);
        Assert.Equal(18, score.TotalCases);
        Assert.Equal(0, score.PendingReviewCount);
        Assert.Equal(0, score.ExitCode);
    }

    private static void CopyEvaluationFiles(string repositoryRoot, string fixtureRoot)
    {
        var sourceDirectory = Path.Combine(repositoryRoot, "reference", "evaluation", "pm-analytics-v3");
        var targetDirectory = Path.Combine(fixtureRoot, "reference", "evaluation", "pm-analytics-v3");
        Directory.CreateDirectory(targetDirectory);

        foreach (var fileName in EvaluationFiles)
        {
            File.Copy(
                Path.Combine(sourceDirectory, fileName),
                Path.Combine(targetDirectory, fileName));
        }
    }

    private static void MarkFirstReviewPending(string fixtureRoot)
    {
        var corpusDirectory = Path.Combine(fixtureRoot, "reference", "evaluation", "pm-analytics-v3");
        var reviewPath = Path.Combine(corpusDirectory, "review-decisions.json");
        var review = JsonNode.Parse(File.ReadAllText(reviewPath))!.AsObject();
        review["status"] = "review-pending";
        var firstDecision = review["decisions"]!.AsArray()[0]!.AsObject();
        firstDecision["status"] = "pending";
        firstDecision["reviewers"] = new JsonArray();
        firstDecision["reviewDate"] = null;
        File.WriteAllText(reviewPath, review.ToJsonString());
    }
}
