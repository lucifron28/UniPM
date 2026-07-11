using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class LexicalMaintenanceQueryBuilderTests
{
    [Fact]
    public void Blank_query_is_rejected()
    {
        var exception = Assert.Throws<LexicalMaintenanceQueryValidationException>(
            () => LexicalMaintenanceQueryBuilder.Build(new LexicalMaintenanceSearchRequest(" \r\n\t ")));

        Assert.Equal(LexicalMaintenanceRetrievalFailureKind.Validation, exception.Kind);
    }

    [Fact]
    public void Whitespace_and_line_endings_are_normalized()
    {
        var query = LexicalMaintenanceQueryBuilder.Build(
            new LexicalMaintenanceSearchRequest("  low\r\n\t pressure  "));

        Assert.Equal("low pressure", query.NormalizedQuery);
        Assert.Equal("\"low*\" AND \"pressure*\"", query.SearchCondition);
    }

    [Fact]
    public void Query_length_is_bounded()
    {
        Assert.Throws<LexicalMaintenanceQueryValidationException>(
            () => LexicalMaintenanceQueryBuilder.Build(
                new LexicalMaintenanceSearchRequest(new string('a', LexicalMaintenanceQueryBuilder.MaxQueryLength + 1))));
    }

    [Fact]
    public void Searchable_token_count_is_bounded()
    {
        Assert.Throws<LexicalMaintenanceQueryValidationException>(
            () => LexicalMaintenanceQueryBuilder.Build(
                new LexicalMaintenanceSearchRequest("one two three four five six seven eight nine")));
    }

    [Fact]
    public void Punctuation_and_full_text_operators_are_removed_before_quoting()
    {
        var query = LexicalMaintenanceQueryBuilder.Build(
            new LexicalMaintenanceSearchRequest("\"low-pressure\" AND (finding) OR NOT"));

        Assert.Equal(
            "\"low*\" AND \"pressure*\" AND \"finding*\"",
            query.SearchCondition);
    }

    [Fact]
    public void Generated_condition_is_restricted_prefix_grammar()
    {
        var query = LexicalMaintenanceQueryBuilder.Build(
            new LexicalMaintenanceSearchRequest("low\" OR pressure; --"));

        Assert.Equal("\"low*\" AND \"pressure*\"", query.SearchCondition);
        Assert.DoesNotContain("OR", query.SearchCondition, StringComparison.Ordinal);
        Assert.DoesNotContain(";", query.SearchCondition, StringComparison.Ordinal);
        Assert.DoesNotContain("--", query.SearchCondition, StringComparison.Ordinal);
    }

    [Fact]
    public void Limit_uses_a_default_and_caps_at_the_maximum()
    {
        var defaultQuery = LexicalMaintenanceQueryBuilder.Build(new LexicalMaintenanceSearchRequest("pressure"));
        var zeroQuery = LexicalMaintenanceQueryBuilder.Build(new LexicalMaintenanceSearchRequest("pressure", 0));
        var cappedQuery = LexicalMaintenanceQueryBuilder.Build(new LexicalMaintenanceSearchRequest("pressure", 1000));

        Assert.Equal(LexicalMaintenanceQueryBuilder.DefaultLimit, defaultQuery.Limit);
        Assert.Equal(LexicalMaintenanceQueryBuilder.DefaultLimit, zeroQuery.Limit);
        Assert.Equal(LexicalMaintenanceQueryBuilder.MaxLimit, cappedQuery.Limit);
    }

    [Fact]
    public void Negative_limit_is_rejected()
    {
        Assert.Throws<LexicalMaintenanceQueryValidationException>(
            () => LexicalMaintenanceQueryBuilder.Build(new LexicalMaintenanceSearchRequest("pressure", -1)));
    }

    [Fact]
    public void Controlled_asset_category_is_normalized_through_the_catalog()
    {
        var query = LexicalMaintenanceQueryBuilder.Build(
            new LexicalMaintenanceSearchRequest("pressure", AssetCategory: " FIRE-ALARM "));

        Assert.Equal("fire-alarm", query.AssetCategory);
    }

    [Fact]
    public void Unsupported_asset_category_is_rejected()
    {
        Assert.Throws<LexicalMaintenanceQueryValidationException>(
            () => LexicalMaintenanceQueryBuilder.Build(
                new LexicalMaintenanceSearchRequest("pressure", AssetCategory: "unknown-category")));
    }

    [Fact]
    public void Reversed_date_range_is_rejected()
    {
        Assert.Throws<LexicalMaintenanceQueryValidationException>(
            () => LexicalMaintenanceQueryBuilder.Build(
                new LexicalMaintenanceSearchRequest(
                    "pressure",
                    DateFrom: new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.FromHours(8)),
                    DateTo: new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.FromHours(8)))));
    }
}
