using Microsoft.Extensions.Options;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class OpenAiCompatibleEmbeddingSmokeTests
{
    [EmbeddingSmokeFact]
    public async Task Configured_provider_returns_stable_vectors()
    {
        var service = new OpenAiCompatibleEmbeddingService(
            new HttpClient(),
            Options.Create(new EmbeddingOptions
            {
                Enabled = true,
                BaseAddress = Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_ENDPOINT"),
                Model = Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_MODEL"),
                ApiKey = Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_API_KEY"),
                AllowRemoteProvider = true,
                MaxBatchSize = 2,
                MaxInputCharacters = 4000
            }));

        var vectors = await service.GenerateBatchAsync(["mahina ang pressure"]);

        var vector = Assert.Single(vectors);
        Assert.InRange(vector.Dimensions, EmbeddingVectorCodec.MinDimensions, EmbeddingVectorCodec.MaxDimensions);
        Assert.All(vector.Values, value => Assert.True(double.IsFinite(value)));
    }
}

internal sealed class EmbeddingSmokeFactAttribute : FactAttribute
{
    public EmbeddingSmokeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_ENDPOINT"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_MODEL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_API_KEY")))
        {
            Skip = "Set UNIPM_EMBEDDING_TEST_ENDPOINT, UNIPM_EMBEDDING_TEST_MODEL, and UNIPM_EMBEDDING_TEST_API_KEY to run the optional embedding smoke test.";
        }
    }
}
