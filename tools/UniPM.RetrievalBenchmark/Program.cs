using System.Globalization;
using UniPM.Api.Features.Retrieval;

namespace UniPM.RetrievalBenchmark;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var result = await new SqlServerBenchmarkRunner().RunAsync(options);
            Console.WriteLine($"JSON report: {result.JsonReportPath}");
            Console.WriteLine($"Markdown report: {result.MarkdownReportPath}");
            if (result.KeptDatabaseName is not null)
            {
                Console.WriteLine($"Kept temporary database: {result.KeptDatabaseName}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Retrieval benchmark failed: {exception.Message}");
            return 1;
        }
    }

    public static BenchmarkRunnerOptions ParseOptions(string[] args)
    {
        var channels = new HashSet<string>(StringComparer.Ordinal);
        string? outputDirectory = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--channels" when index + 1 < args.Length:
                    foreach (var channel in args[++index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (channel is not ("lexical" or "semantic" or "fused"))
                        {
                            throw new ArgumentException($"Unsupported benchmark channel '{channel}'.");
                        }

                        channels.Add(channel);
                    }

                    break;
                case "--output" when index + 1 < args.Length:
                    outputDirectory = Path.GetFullPath(args[++index]);
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete benchmark argument '{args[index]}'.");
            }
        }

        if (channels.Count == 0)
        {
            channels.Add("lexical");
            channels.Add("semantic");
        }

        var embeddingOptions = channels.Contains("semantic", StringComparer.Ordinal)
            || channels.Contains("fused", StringComparer.Ordinal)
            ? ReadEmbeddingOptions()
            : null;

        return new BenchmarkRunnerOptions
        {
            Channels = channels.OrderBy(channel => channel, StringComparer.Ordinal).ToArray(),
            OutputDirectory = outputDirectory
                ?? Path.GetFullPath(Path.Combine("artifacts", "retrieval-benchmark")),
            KeepDatabase = ReadBoolean("UNIPM_BENCHMARK_KEEP_DATABASE"),
            Embeddings = embeddingOptions
        };
    }

    private static EmbeddingOptions ReadEmbeddingOptions()
    {
        if (!ReadBoolean("UNIPM_EMBEDDINGS_ENABLED"))
        {
            throw new InvalidOperationException(
                "Semantic benchmarking requires UNIPM_EMBEDDINGS_ENABLED=true.");
        }

        var dimensionsText = RequiredEnvironment("UNIPM_EMBEDDINGS_DIMENSIONS");
        if (!int.TryParse(dimensionsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimensions))
        {
            throw new InvalidOperationException("UNIPM_EMBEDDINGS_DIMENSIONS must be an invariant integer.");
        }

        return new EmbeddingOptions
        {
            Enabled = true,
            ProviderKey = RequiredEnvironment("UNIPM_EMBEDDINGS_PROVIDER_KEY"),
            BaseAddress = RequiredEnvironment("UNIPM_EMBEDDINGS_BASE_ADDRESS"),
            Path = Environment.GetEnvironmentVariable("UNIPM_EMBEDDINGS_PATH") ?? "/v1/embeddings",
            Model = RequiredEnvironment("UNIPM_EMBEDDINGS_MODEL"),
            ApiKey = Environment.GetEnvironmentVariable("UNIPM_EMBEDDINGS_API_KEY"),
            Dimensions = dimensions,
            TimeoutSeconds = ReadInteger("UNIPM_EMBEDDINGS_TIMEOUT_SECONDS", 30),
            MaxBatchSize = ReadInteger("UNIPM_EMBEDDINGS_MAX_BATCH_SIZE", 16),
            MaxInputCharacters = ReadInteger("UNIPM_EMBEDDINGS_MAX_INPUT_CHARACTERS", 4000),
            AllowRemoteProvider = ReadBoolean("UNIPM_EMBEDDINGS_ALLOW_REMOTE_PROVIDER")
        };
    }

    private static string RequiredEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Environment variable '{name}' is required.")
            : value;
    }

    private static bool ReadBoolean(string name)
        => bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value;

    private static int ReadInteger(string name, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : int.Parse(value, CultureInfo.InvariantCulture);
    }
}
