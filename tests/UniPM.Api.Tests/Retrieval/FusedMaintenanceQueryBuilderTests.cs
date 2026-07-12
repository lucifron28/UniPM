using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class FusedMaintenanceQueryBuilderTests
{
    [Fact]
    public void Defaults_to_ten_results_and_twenty_candidates()
    {
        var query = FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest("pressure"));

        Assert.Equal(10, query.Limit);
        Assert.Equal(20, query.CandidateLimit);
    }

    [Fact]
    public void Candidate_depth_is_at_least_output_limit_and_capped_at_one_hundred()
    {
        Assert.Equal(
            25,
            FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest("pressure", 25)).CandidateLimit);
        Assert.Equal(
            100,
            FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest("pressure", 1000)).CandidateLimit);
    }

    [Fact]
    public void Common_filters_are_normalized_and_invalid_values_are_rejected()
    {
        var query = FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest(
            "  pressure\r\n finding ",
            AssetCategory: " FIRE-ALARM ",
            Building: " Main\tBuilding "));

        Assert.Equal("pressure finding", query.NormalizedQuery);
        Assert.Equal("fire-alarm", query.AssetCategory);
        Assert.Equal("Main Building", query.Building);
        Assert.Throws<FusedMaintenanceQueryValidationException>(
            () => FusedMaintenanceQueryBuilder.Build(
                new FusedMaintenanceSearchRequest("pressure", AssetCategory: "unsupported")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_query_is_rejected(string query)
    {
        Assert.Throws<FusedMaintenanceQueryValidationException>(
            () => FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest(query)));
    }

    [Fact]
    public void Reversed_dates_and_oversized_filters_are_rejected()
    {
        Assert.Throws<FusedMaintenanceQueryValidationException>(() =>
            FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest(
                "pressure",
                DateFrom: AtManila(2025, 2, 1),
                DateTo: AtManila(2025, 1, 1))));
        Assert.Throws<FusedMaintenanceQueryValidationException>(() =>
            FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest(
                "pressure",
                Location: new string('x', 257))));
        Assert.Throws<FusedMaintenanceQueryValidationException>(() =>
            FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest(
                new string('x', 257))));
        Assert.Throws<FusedMaintenanceQueryValidationException>(() =>
            FusedMaintenanceQueryBuilder.Build(new FusedMaintenanceSearchRequest("pressure", -1)));
    }

    private static DateTimeOffset AtManila(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, TimeSpan.FromHours(8));
}
