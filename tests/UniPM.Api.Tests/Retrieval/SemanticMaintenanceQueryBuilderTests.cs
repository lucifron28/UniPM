using UniPM.Api.Features.Retrieval;
using UniPM.Api.Features.MaintenanceReview;

namespace UniPM.Api.Tests.Retrieval;

public sealed class SemanticMaintenanceQueryBuilderTests
{
    [Fact]
    public void Query_normalizes_whitespace_and_includes_safe_issue_context()
    {
        var query = SemanticMaintenanceQueryBuilder.Build(
            new SemanticMaintenanceSearchRequest(
                "  mahina \r\n ang pressure ",
                AssetCategory: " FIRE-EXTINGUISHER "),
            CreateIssueNormalizer());

        Assert.Equal("mahina ang pressure", query.NormalizedQuery);
        Assert.Contains("issue-context: low_pressure", query.EmbeddingInput, StringComparison.Ordinal);
        Assert.Equal("fire-extinguisher", query.AssetCategory);
    }

    [Fact]
    public void Query_limits_and_filters_are_validated()
    {
        Assert.Throws<SemanticMaintenanceQueryValidationException>(
            () => SemanticMaintenanceQueryBuilder.Build(
                new SemanticMaintenanceSearchRequest(" "),
                CreateIssueNormalizer()));
        Assert.Throws<SemanticMaintenanceQueryValidationException>(
            () => SemanticMaintenanceQueryBuilder.Build(
                new SemanticMaintenanceSearchRequest(new string('x', 257)),
                CreateIssueNormalizer()));
        Assert.Throws<SemanticMaintenanceQueryValidationException>(
            () => SemanticMaintenanceQueryBuilder.Build(
                new SemanticMaintenanceSearchRequest("pressure", -1),
                CreateIssueNormalizer()));
        Assert.Throws<SemanticMaintenanceQueryValidationException>(
            () => SemanticMaintenanceQueryBuilder.Build(
                new SemanticMaintenanceSearchRequest(
                    "pressure",
                    Building: new string('x', 257)),
                CreateIssueNormalizer()));
        Assert.Throws<SemanticMaintenanceQueryValidationException>(
            () => SemanticMaintenanceQueryBuilder.Build(
                new SemanticMaintenanceSearchRequest(
                    "pressure",
                    DateFrom: AtManila(2025, 6, 1),
                    DateTo: AtManila(2025, 5, 1)),
                CreateIssueNormalizer()));
        Assert.Throws<SemanticMaintenanceQueryValidationException>(
            () => SemanticMaintenanceQueryBuilder.Build(
                new SemanticMaintenanceSearchRequest("pressure", AssetCategory: "unsupported"),
                CreateIssueNormalizer()));
    }

    [Fact]
    public void Query_limit_uses_default_and_maximum()
    {
        var defaultQuery = SemanticMaintenanceQueryBuilder.Build(
            new SemanticMaintenanceSearchRequest("pressure"),
            CreateIssueNormalizer());
        var cappedQuery = SemanticMaintenanceQueryBuilder.Build(
            new SemanticMaintenanceSearchRequest("pressure", 1000),
            CreateIssueNormalizer());

        Assert.Equal(SemanticMaintenanceQueryBuilder.DefaultLimit, defaultQuery.Limit);
        Assert.Equal(SemanticMaintenanceQueryBuilder.MaxLimit, cappedQuery.Limit);
    }

    [Fact]
    public void Sanitized_review_query_never_places_sensitive_finding_values_in_embedding_input()
    {
        const string rawFinding =
            "Low pressure reported by Employee ID 2024-001, contact 0917-123-4567, email ron@example.com";
        var session = new PrivacySanitizerService().CreateSession();
        var reviewQuery = MaintenanceReviewRetrievalQueryBuilder.Build(
            session.Sanitize(rawFinding),
            ["low_pressure"]);

        var semanticQuery = SemanticMaintenanceQueryBuilder.Build(
            new SemanticMaintenanceSearchRequest(
                reviewQuery.Text,
                AssetCategory: "fire-extinguisher",
                IssueKeys: reviewQuery.IssueKeys),
            CreateIssueNormalizer());

        Assert.DoesNotContain("2024-001", semanticQuery.EmbeddingInput, StringComparison.Ordinal);
        Assert.DoesNotContain("0917-123-4567", semanticQuery.EmbeddingInput, StringComparison.Ordinal);
        Assert.DoesNotContain("ron@example.com", semanticQuery.EmbeddingInput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[EMPLOYEE_ID_1]", semanticQuery.EmbeddingInput, StringComparison.Ordinal);
        Assert.Contains("[PHONE_1]", semanticQuery.EmbeddingInput, StringComparison.Ordinal);
        Assert.Contains("issue-context: low_pressure", semanticQuery.EmbeddingInput, StringComparison.Ordinal);
    }

    private static MaintenanceIssueNormalizer CreateIssueNormalizer()
    {
        var root = FindRepositoryRoot();
        var loader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
        {
            LexiconPath = Path.Combine(
                root,
                "server",
                "Features",
                "Retrieval",
                "Resources",
                MaintenanceIssueLexiconOptions.LexiconFileName)
        });

        return new MaintenanceIssueNormalizer(loader);
    }

    private static DateTimeOffset AtManila(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, TimeSpan.FromHours(8));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniPM.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the UniPM repository root.");
    }
}
