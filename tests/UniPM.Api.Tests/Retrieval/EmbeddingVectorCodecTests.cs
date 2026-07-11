using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class EmbeddingVectorCodecTests
{
    [Fact]
    public void Serialization_is_deterministic_after_normalization()
    {
        var first = EmbeddingVectorCodec.Serialize([3d, 4d]);
        var second = EmbeddingVectorCodec.Serialize([0.6d, 0.8d]);

        Assert.Equal(first, second);
        Assert.Equal("[0.6,0.8]", first);
    }

    [Fact]
    public void Parse_normalizes_and_validates_dimensions()
    {
        var parsed = EmbeddingVectorCodec.Parse("[3,4]", 2);

        Assert.Equal(0.6d, parsed[0], precision: 12);
        Assert.Equal(0.8d, parsed[1], precision: 12);
        Assert.Throws<EmbeddingVectorValidationException>(
            () => EmbeddingVectorCodec.Parse("[1,2,3]", 2));
    }

    [Fact]
    public void Zero_and_non_finite_vectors_are_rejected()
    {
        Assert.Throws<EmbeddingVectorValidationException>(
            () => EmbeddingVectorCodec.Normalize([0d, 0d]));
        Assert.Throws<EmbeddingVectorValidationException>(
            () => EmbeddingVectorCodec.Normalize([double.NaN, 1d]));
        Assert.Throws<EmbeddingVectorValidationException>(
            () => EmbeddingVectorCodec.Normalize([double.PositiveInfinity, 1d]));
    }

    [Fact]
    public void Cosine_similarity_matches_known_vectors_and_clamps_rounding()
    {
        Assert.Equal(
            1d,
            EmbeddingVectorCodec.CosineSimilarity([1d, 0d], [1d, 0d]),
            precision: 12);
        Assert.Equal(
            0d,
            EmbeddingVectorCodec.CosineSimilarity([1d, 0d], [0d, 1d]),
            precision: 12);
        Assert.Equal(
            -1d,
            EmbeddingVectorCodec.CosineSimilarity([1d, 0d], [-1d, 0d]),
            precision: 12);
        Assert.Throws<EmbeddingVectorValidationException>(
            () => EmbeddingVectorCodec.CosineSimilarity([1d], [1d, 0d]));
    }
}
