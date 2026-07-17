using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.Retrieval;

namespace UniPM.RetrievalBenchmark;

public sealed class BenchmarkRunnerOptions
{
    public required IReadOnlyList<string> Channels { get; init; }
    public required string OutputDirectory { get; init; }
    public bool KeepDatabase { get; init; }
    public EmbeddingOptions? Embeddings { get; init; }
}

public sealed record BenchmarkRunResult(
    string JsonReportPath,
    string MarkdownReportPath,
    string? KeptDatabaseName);

public sealed class SqlServerBenchmarkRunner
{
    public Task<BenchmarkRunResult> RunAsync(
        BenchmarkRunnerOptions options,
        CancellationToken cancellationToken = default)
        => RunAsync(options, embeddingServiceOverride: null, cancellationToken);

    internal async Task<BenchmarkRunResult> RunAsync(
        BenchmarkRunnerOptions options,
        IEmbeddingService? embeddingServiceOverride,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var connectionString = RequireEnvironment("UNIPM_SQLSERVER_TEST_CONNECTION");
        var repositoryRoot = FindRepositoryRoot();
        var manifestPath = Path.Combine(
            repositoryRoot,
            "tests",
            "UniPM.Api.Tests",
            "Retrieval",
            "Fixtures",
            RetrievalBenchmarkVocabulary.ManifestFileName);
        var datasetPath = Path.Combine(
            repositoryRoot,
            "server",
            "Data",
            "Seeding",
            "Resources",
            SyntheticMaintenanceSeedOptions.DatasetFileName);
        var lexiconPath = Path.Combine(
            repositoryRoot,
            "server",
            "Retrieval",
            "Resources",
            MaintenanceIssueLexiconOptions.LexiconFileName);

        await using var database = await TemporarySqlServerDatabase.CreateAsync(
            connectionString,
            options.KeepDatabase,
            cancellationToken);
        var contextFactory = new BenchmarkDbContextFactory(database.ConnectionString);

        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        var datasetLoader = new SyntheticMaintenanceDatasetLoader(
            new SyntheticMaintenanceSeedOptions { DatasetPath = datasetPath },
            new SyntheticMaintenanceDatasetValidator());
        var dataset = await datasetLoader.LoadAsync(cancellationToken);
        var lexiconLoader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
        {
            LexiconPath = lexiconPath
        });
        var issueNormalizer = new MaintenanceIssueNormalizer(lexiconLoader);
        var projector = new MaintenanceSearchDocumentProjector(contextFactory, issueNormalizer);
        var seeder = new SyntheticMaintenanceSeeder(contextFactory, datasetLoader, projector);
        await seeder.SeedAsync(cancellationToken);
        await projector.RebuildAsync(cancellationToken);

        var manifest = await new RetrievalEvaluationManifestLoader(dataset)
            .LoadAsync(manifestPath, cancellationToken);
        var selectedChannels = options.Channels.ToHashSet(StringComparer.Ordinal);
        var needsLexical = selectedChannels.Contains("lexical") || selectedChannels.Contains("fused");
        var needsSemantic = selectedChannels.Contains("semantic") || selectedChannels.Contains("fused");
        var reportLexical = selectedChannels.Contains("lexical");
        var reportSemantic = selectedChannels.Contains("semantic");
        var reportFused = selectedChannels.Contains("fused");
        var channels = new List<IBenchmarkRetrievalChannel>();
        SqlServerLexicalMaintenanceRetriever? lexicalRetriever = null;
        SqlServerSemanticMaintenanceRetriever? semanticRetriever = null;
        IEmbeddingService? embeddingService = null;

        if (needsLexical)
        {
            await SqlServerBenchmarkReadiness.WaitForFullTextAsync(
                database.ConnectionString,
                cancellationToken);
            lexicalRetriever = new SqlServerLexicalMaintenanceRetriever(contextFactory);
            var probeInspection = dataset.Inspections
                .FirstOrDefault(inspection => !string.IsNullOrWhiteSpace(inspection.Remarks))
                ?? throw new InvalidOperationException("The operational fixture has no inspection remarks for the lexical readiness probe.");
            var probeTerm = probeInspection.Remarks
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(term => term.Trim(' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"'))
                .FirstOrDefault(term => term.Length >= 3)
                ?? throw new InvalidOperationException("The operational fixture has no usable term for the lexical readiness probe.");
            await SqlServerBenchmarkReadiness.WaitForLexicalContentAsync(
                lexicalRetriever,
                new LexicalMaintenanceSearchRequest(
                    probeTerm,
                    AssetId: probeInspection.AssetId,
                    AssetCategory: probeInspection.AssetCategory),
                probeInspection.Id,
                cancellationToken);
        }

        if (needsSemantic)
        {
            if (options.Embeddings is null)
            {
                throw new InvalidOperationException("Semantic benchmarking requires embedding configuration.");
            }

            embeddingService = embeddingServiceOverride
                ?? new OpenAiCompatibleEmbeddingService(
                    new HttpClient(),
                    Options.Create(options.Embeddings));
            var indexer = new MaintenanceSearchDocumentEmbeddingIndexer(
                contextFactory,
                embeddingService,
                Options.Create(options.Embeddings));
            var indexResult = await indexer.RebuildAsync(cancellationToken);
            if (indexResult.Failed > 0
                || indexResult.Created + indexResult.Updated + indexResult.Skipped != indexResult.Total)
            {
                throw new InvalidOperationException(
                    $"Semantic embedding rebuild did not reconcile the benchmark documents: total={indexResult.Total}, created={indexResult.Created}, updated={indexResult.Updated}, skipped={indexResult.Skipped}, failed={indexResult.Failed}.");
            }

            await using (var verificationContext = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                var documentCount = await verificationContext.MaintenanceSearchDocuments.CountAsync(cancellationToken);
                var embeddingCount = await verificationContext.MaintenanceSearchDocumentEmbeddings.CountAsync(cancellationToken);
                if (embeddingCount != documentCount)
                {
                    throw new InvalidOperationException(
                        $"Semantic embedding rebuild produced {embeddingCount} embeddings for {documentCount} search documents.");
                }
            }

            semanticRetriever = new SqlServerSemanticMaintenanceRetriever(
                contextFactory,
                embeddingService,
                issueNormalizer);
        }

        if (reportLexical)
        {
            channels.Add(new DelegateBenchmarkRetrievalChannel(
                new BenchmarkChannelMetadata
                {
                    RetrievalChannel = "lexical",
                    ResultLimit = BenchmarkEvaluationService.ResultLimit,
                    FullTextSearchReady = true
                },
                async (request, token) =>
                {
                    var result = await lexicalRetriever!.SearchAsync(
                        ToLexicalRequest(request),
                        token);
                    return result
                        .Select(item => new BenchmarkRetrievedResult(item.InspectionId, item.RawLexicalRank))
                        .ToArray();
                }));
        }

        if (reportSemantic)
        {
            var descriptor = embeddingService!.Descriptor;
            channels.Add(new DelegateBenchmarkRetrievalChannel(
                new BenchmarkChannelMetadata
                {
                    RetrievalChannel = "semantic",
                    ResultLimit = BenchmarkEvaluationService.ResultLimit,
                    ProviderKey = descriptor.ProviderKey,
                    ModelKey = descriptor.ModelKey,
                    Dimensions = descriptor.Dimensions,
                    EmbeddingProfile = descriptor.EmbeddingProfile
                },
                async (request, token) =>
                {
                    var result = await semanticRetriever!.SearchAsync(
                        ToSemanticRequest(request),
                        token);
                    return result
                        .Select(item => new BenchmarkRetrievedResult(item.InspectionId, item.RawSemanticScore))
                        .ToArray();
                }));
        }

        if (reportFused)
        {
            var descriptor = embeddingService!.Descriptor;
            var fusedRetriever = new FusedMaintenanceRetriever(lexicalRetriever!, semanticRetriever!);
            channels.Add(new DelegateBenchmarkRetrievalChannel(
                new BenchmarkChannelMetadata
                {
                    RetrievalChannel = FusedMaintenanceSearchResult.RetrievalChannelValue,
                    ResultLimit = BenchmarkEvaluationService.ResultLimit,
                    ProviderKey = descriptor.ProviderKey,
                    ModelKey = descriptor.ModelKey,
                    Dimensions = descriptor.Dimensions,
                    EmbeddingProfile = descriptor.EmbeddingProfile,
                    FusionMethod = ReciprocalRankFusion.Method,
                    ReciprocalRankConstant = ReciprocalRankFusion.ReciprocalRankConstant,
                    CandidateLimit = Math.Max(
                        BenchmarkEvaluationService.ResultLimit,
                        FusedMaintenanceQueryBuilder.DefaultCandidateDepth),
                    SemanticDegradationPolicy = "Semantic unavailable or failed returns lexical-only results and marks the run degraded."
                },
                async (request, token) =>
                {
                    var response = await fusedRetriever.SearchAsync(ToFusedRequest(request), token);
                    if (response.IsDegraded)
                    {
                        throw new InvalidOperationException(
                            "The fused benchmark cannot score a degraded retrieval response.");
                    }

                    return response.Results
                        .Select(item => new BenchmarkRetrievedResult(
                            item.InspectionId,
                            item.FusionScore,
                            item.LexicalRank,
                            item.SemanticRank,
                            item.FusionScore))
                        .ToArray();
                }));
        }

        var report = await new BenchmarkEvaluationService()
            .RunAsync(manifest, channels, cancellationToken: cancellationToken);
        if (embeddingServiceOverride is not null && (reportSemantic || reportFused))
        {
            report.Warnings.Add(reportSemantic && reportFused
                ? "Semantic and fused metrics that depend on the deterministic injected embedding service are pipeline-validation results only; they are not semantic-model or fused-retrieval quality evidence."
                : reportFused
                    ? "Fused metrics that depend on the deterministic injected embedding service are pipeline-validation results only; they are not fused-retrieval quality evidence."
                    : "Semantic metrics that depend on the deterministic injected embedding service are pipeline-validation results only; they are not semantic-model quality evidence.");
        }
        var writer = new BenchmarkReportWriter();
        await writer.WriteAsync(report, options.OutputDirectory, cancellationToken);

        return new BenchmarkRunResult(
            Path.Combine(options.OutputDirectory, "retrieval-benchmark.json"),
            Path.Combine(options.OutputDirectory, "retrieval-benchmark.md"),
            database.KeptDatabaseName);
    }

    private static LexicalMaintenanceSearchRequest ToLexicalRequest(BenchmarkChannelRequest request)
    {
        var filters = request.Filters;
        return new LexicalMaintenanceSearchRequest(
            request.QueryText,
            request.Limit,
            filters.AssetId,
            filters.AssetCategory,
            filters.Building,
            filters.Department,
            filters.Location,
            filters.IsOperational,
            filters.DateFrom,
            filters.DateTo);
    }

    private static SemanticMaintenanceSearchRequest ToSemanticRequest(BenchmarkChannelRequest request)
    {
        var filters = request.Filters;
        return new SemanticMaintenanceSearchRequest(
            request.QueryText,
            request.Limit,
            filters.AssetId,
            filters.AssetCategory,
            filters.Building,
            filters.Department,
            filters.Location,
            filters.IsOperational,
            filters.DateFrom,
            filters.DateTo);
    }

    private static FusedMaintenanceSearchRequest ToFusedRequest(BenchmarkChannelRequest request)
    {
        var filters = request.Filters;
        return new FusedMaintenanceSearchRequest(
            request.QueryText,
            request.Limit,
            filters.AssetId,
            filters.AssetCategory,
            filters.Building,
            filters.Department,
            filters.Location,
            filters.IsOperational,
            filters.DateFrom,
            filters.DateTo);
    }

    private static string RequireEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Environment variable '{name}' is required.");
        }

        return value;
    }

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

internal sealed class BenchmarkDbContextFactory(string connectionString)
    : IDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}

internal sealed class TemporarySqlServerDatabase : IAsyncDisposable
{
    private readonly string baseConnectionString;
    private readonly bool keepDatabase;

    private TemporarySqlServerDatabase(
        string connectionString,
        string databaseName,
        string baseConnectionString,
        bool keepDatabase)
    {
        ConnectionString = connectionString;
        DatabaseName = databaseName;
        this.baseConnectionString = baseConnectionString;
        this.keepDatabase = keepDatabase;
    }

    public string ConnectionString { get; }
    public string DatabaseName { get; }
    public string? KeptDatabaseName => keepDatabase ? DatabaseName : null;

    public static async Task<TemporarySqlServerDatabase> CreateAsync(
        string baseConnectionString,
        bool keepDatabase,
        CancellationToken cancellationToken)
    {
        var databaseName = $"UniPMBenchmark_{Guid.NewGuid():N}";
        var databaseBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        };
        var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new TemporarySqlServerDatabase(
            databaseBuilder.ConnectionString,
            databaseName,
            baseConnectionString,
            keepDatabase);
    }

    public async ValueTask DisposeAsync()
    {
        if (keepDatabase)
        {
            return;
        }

        var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        };
        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]";
        await command.ExecuteNonQueryAsync();
    }
}

internal static class SqlServerBenchmarkReadiness
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public static async Task WaitForFullTextAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        const string readinessSql = """
            SELECT CONVERT(bit,
                CASE
                    WHEN ISNULL(TRY_CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')), 0) <> 1 THEN 0
                    WHEN ISNULL(FULLTEXTCATALOGPROPERTY(N'UniPMMaintenanceRetrieval', 'PopulateStatus'), -1) <> 0 THEN 0
                    WHEN EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes AS fullTextIndex
                        INNER JOIN sys.tables AS tables
                            ON tables.object_id = fullTextIndex.object_id
                        INNER JOIN sys.schemas AS schemas
                            ON schemas.schema_id = tables.schema_id
                        INNER JOIN sys.fulltext_catalogs AS catalog
                            ON catalog.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                        WHERE schemas.name = N'dbo'
                          AND tables.name = N'MaintenanceSearchDocuments'
                          AND catalog.name = N'UniPMMaintenanceRetrieval'
                          AND fullTextIndex.is_enabled = 1) THEN 1
                    ELSE 0
                END);
            """;

        Exception? lastException = null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = readinessSql;
                var value = await command.ExecuteScalarAsync(cancellationToken);
                if (value is bool ready && ready)
                {
                    return;
                }
            }
            catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = exception;
            }

            if (attempt < MaxAttempts - 1)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "SQL Server Full-Text Search did not become ready for the benchmark database.",
            lastException);
    }

    public static async Task WaitForLexicalContentAsync(
        ILexicalMaintenanceRetriever retriever,
        LexicalMaintenanceSearchRequest request,
        Guid expectedInspectionId,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                var results = await retriever.SearchAsync(request, cancellationToken);
                if (results.Any(result => result.InspectionId == expectedInspectionId))
                {
                    return;
                }
            }
            catch (LexicalMaintenanceRetrievalException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                lastException = exception;
            }

            if (attempt < MaxAttempts - 1)
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "SQL Server Full-Text Search did not expose the seeded probe document before the benchmark started.",
            lastException);
    }
}
