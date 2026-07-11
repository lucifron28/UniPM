using System.Globalization;
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
                ProviderKey = "smoke-provider",
                BaseAddress = Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_ENDPOINT"),
                Model = Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_MODEL"),
                ApiKey = Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_API_KEY"),
                AllowRemoteProvider = true,
                Dimensions = GetTestDimensions(),
                MaxBatchSize = 2,
                MaxInputCharacters = 4000
            }));

        var vectors = await service.GenerateBatchAsync(["mahina ang pressure"]);

        var vector = Assert.Single(vectors);
        Assert.InRange(vector.Dimensions, EmbeddingVectorCodec.MinDimensions, EmbeddingVectorCodec.MaxDimensions);
        Assert.All(vector.Values, value => Assert.True(double.IsFinite(value)));
    }

    private static int GetTestDimensions()
    {
        if (!int.TryParse(
                Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_DIMENSIONS"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var dimensions)
            || dimensions is < EmbeddingVectorCodec.MinDimensions or > EmbeddingVectorCodec.MaxDimensions)
        {
            throw new InvalidOperationException(
                "UNIPM_EMBEDDING_TEST_DIMENSIONS must be a valid embedding dimension count.");
        }

        return dimensions;
    }
}

internal sealed class EmbeddingSmokeFactAttribute : FactAttribute
{
    public EmbeddingSmokeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_ENDPOINT"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_MODEL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_API_KEY"))
            || !int.TryParse(
                Environment.GetEnvironmentVariable("UNIPM_EMBEDDING_TEST_DIMENSIONS"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var dimensions)
            || dimensions is < EmbeddingVectorCodec.MinDimensions or > EmbeddingVectorCodec.MaxDimensions)
        {
            Skip = "Set the embedding smoke endpoint, model, API key, and a valid UNIPM_EMBEDDING_TEST_DIMENSIONS value to run the optional embedding smoke test.";
        }
    }

}
