using UniPM.Api.Features.Retrieval;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class InstitutionalReferenceRetrievalTests
{
    [Fact]
    public void Query_builder_normalizes_category_and_constructs_bounded_full_text_condition()
    {
        var query = InstitutionalReferenceQueryBuilder.Build(new InstitutionalReferenceSearchRequest(
            "  panel, response  ",
            " FIRE-ALARM ",
            new DateOnly(2026, 1, 1)));

        Assert.Equal("panel, response", query.NormalizedQuery);
        Assert.Equal("fire-alarm", query.AssetCategory);
        Assert.Equal("\"panel*\" AND \"response*\"", query.SearchCondition);
        Assert.Equal(InstitutionalReferenceQueryBuilder.DefaultLimit, query.Limit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("       ")]
    public void Query_builder_rejects_blank_queries(string query)
    {
        Assert.Throws<InstitutionalReferenceQueryValidationException>(() =>
            InstitutionalReferenceQueryBuilder.Build(new InstitutionalReferenceSearchRequest(
                query,
                "fire-alarm",
                new DateOnly(2026, 1, 1))));
    }

    [Fact]
    public void Section_embedding_input_is_deterministic_and_uses_a_distinct_profile()
    {
        var document = new ReferenceDocument { Title = " Fictional Guide " };
        var section = new ReferenceDocumentSection
        {
            Heading = " Observation ",
            SectionText = "Record\r\nconditions"
        };
        var descriptor = new EmbeddingServiceDescriptor(
            true,
            "test-provider",
            "test-model",
            384,
            "maintenance-profile");

        Assert.Equal("Fictional Guide\nObservation\nRecord\nconditions",
            InstitutionalReferenceEmbeddingInput.BuildDocumentInput(section, document));
        Assert.Equal(
            "openai-compatible:test-provider:test-model:reference-document-section-v1:384",
            InstitutionalReferenceEmbeddingInput.BuildProfile(descriptor));
        Assert.NotEqual(
            InstitutionalReferenceEmbeddingInput.BuildProfile(descriptor),
            descriptor.EmbeddingProfile);
        Assert.Equal(
            InstitutionalReferenceEmbeddingInput.ComputeSectionSourceHash(section, document),
            InstitutionalReferenceEmbeddingInput.ComputeSectionSourceHash(section, document));
    }

    [Fact]
    public void Results_explicitly_identify_the_institutional_evidence_group()
    {
        InstitutionalReferenceSearchResult result = new InstitutionalReferenceLexicalSearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "FIC-001", "R1", "Fictional", "Fictional Authority",
            null, 0, "Heading", "Text", "Locator", 1, 1,
            InstitutionalReferenceApplicabilityMatch.CategorySpecific, "Fictional scope", 100);

        Assert.Equal("InstitutionalReference", result.EvidenceSourceGroup);
    }

    [Fact]
    public async Task Semantic_diagnostics_are_cleared_before_validation_and_consumed_once()
    {
        var diagnostics = new InstitutionalReferenceRetrievalDiagnostics();
        diagnostics.Record(500, true, 2);
        var retriever = new SqlServerSemanticInstitutionalReferenceRetriever(
            null!,
            new DeterministicEmbeddingService(_ => [1d, 0d]),
            diagnostics);

        await Assert.ThrowsAsync<InstitutionalReferenceQueryValidationException>(() =>
            retriever.SearchAsync(new InstitutionalReferenceSearchRequest(
                " ",
                "fire-alarm",
                new DateOnly(2026, 1, 1))));

        Assert.Equal(new InstitutionalReferenceCandidateDiagnostics(0, false, 0), diagnostics.Consume());
        Assert.Equal(new InstitutionalReferenceCandidateDiagnostics(0, false, 0), diagnostics.Consume());
    }
}
