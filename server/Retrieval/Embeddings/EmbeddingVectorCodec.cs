using System.Text.Json;

namespace UniPM.Api.Features.Retrieval;

internal static class EmbeddingVectorCodec
{
    internal const int MinDimensions = 1;
    internal const int MaxDimensions = 4096;
    internal const int MaxJsonLength = 131072;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    public static double[] Normalize(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count is < MinDimensions or > MaxDimensions)
        {
            throw new EmbeddingVectorValidationException(
                $"Embedding dimensions must be between {MinDimensions} and {MaxDimensions}.");
        }

        var normalized = new double[values.Count];
        var sumSquares = 0d;
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (!double.IsFinite(value))
            {
                throw new EmbeddingVectorValidationException(
                    "Embedding vectors must contain only finite values.");
            }

            sumSquares += value * value;
            if (!double.IsFinite(sumSquares))
            {
                throw new EmbeddingVectorValidationException(
                    "Embedding vector magnitude is outside the supported range.");
            }
        }

        if (sumSquares <= double.Epsilon)
        {
            throw new EmbeddingVectorValidationException("Zero-length embedding vectors are not supported.");
        }

        var magnitude = Math.Sqrt(sumSquares);
        for (var index = 0; index < normalized.Length; index++)
        {
            normalized[index] = values[index] / magnitude;
        }

        return normalized;
    }

    public static string Serialize(IReadOnlyList<double> values)
    {
        var normalized = Normalize(values);
        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        if (json.Length > MaxJsonLength)
        {
            throw new EmbeddingVectorValidationException(
                $"The serialized embedding exceeds the {MaxJsonLength}-character limit.");
        }

        return json;
    }

    public static double[] Parse(string json, int expectedDimensions)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength)
        {
            throw new EmbeddingVectorValidationException("The persisted embedding JSON is empty or oversized.");
        }

        if (expectedDimensions is < MinDimensions or > MaxDimensions)
        {
            throw new EmbeddingVectorValidationException("The persisted embedding dimensions are unsupported.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.GetArrayLength() != expectedDimensions)
            {
                throw new EmbeddingVectorValidationException("The persisted embedding dimensions do not match.");
            }

            var values = new double[expectedDimensions];
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Number
                    || !element.TryGetDouble(out var value)
                    || !double.IsFinite(value))
                {
                    throw new EmbeddingVectorValidationException(
                        "The persisted embedding contains a non-finite or non-numeric value.");
                }

                values[index++] = value;
            }

            return Normalize(values);
        }
        catch (JsonException exception)
        {
            throw new EmbeddingVectorValidationException(
                "The persisted embedding JSON is malformed.",
                exception);
        }
    }

    public static double CosineSimilarity(
        IReadOnlyList<double> left,
        IReadOnlyList<double> right)
    {
        if (left.Count != right.Count)
        {
            throw new EmbeddingVectorValidationException("Embedding dimensions must match for comparison.");
        }

        var score = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            score += left[index] * right[index];
        }

        if (!double.IsFinite(score))
        {
            throw new EmbeddingVectorValidationException("The cosine similarity score is not finite.");
        }

        return Math.Clamp(score, -1d, 1d);
    }
}
