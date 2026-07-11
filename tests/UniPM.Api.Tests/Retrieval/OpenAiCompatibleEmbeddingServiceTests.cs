using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class OpenAiCompatibleEmbeddingServiceTests
{
    [Fact]
    public async Task Disabled_provider_fails_without_network_access()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = false
            });

        await Assert.ThrowsAsync<EmbeddingServiceAvailabilityException>(
            () => service.GenerateBatchAsync(["pressure"]));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Provider_sends_bounded_openai_compatible_request_and_parses_vectors()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"data":[{"index":0,"embedding":[3,4]}]}""",
                Encoding.UTF8,
                "application/json")
        });
        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "local-provider",
                BaseAddress = "http://localhost:8080",
                Model = "local-model",
                ApiKey = "test-key",
                Dimensions = 2,
                MaxBatchSize = 2,
                MaxInputCharacters = 20
            });

        var vectors = await service.GenerateBatchAsync(["low pressure"]);

        Assert.Single(vectors);
        Assert.Equal(0.6d, vectors[0].Values[0], precision: 12);
        Assert.Single(handler.Requests);
        Assert.Contains("\"model\":\"local-model\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"input\":[\"low pressure\"]", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.Requests[0].AuthorizationScheme);
    }

    [Fact]
    public async Task Remote_provider_is_rejected_by_default()
    {
        var service = CreateService(
            new RecordingHandler(_ => throw new InvalidOperationException()),
            new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "remote-provider",
                BaseAddress = "https://remote.example.test",
                Model = "remote-model",
                Dimensions = 2
            });

        var exception = await Assert.ThrowsAsync<EmbeddingServiceAvailabilityException>(
            () => service.GenerateBatchAsync(["pressure"]));

        Assert.Contains("AllowRemoteProvider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Absolute_path_cannot_override_a_local_base_address()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "local-provider",
                BaseAddress = "http://localhost:8080",
                Path = "https://remote.example.test/v1/embeddings",
                Model = "local-model",
                Dimensions = 2
            });

        var exception = await Assert.ThrowsAsync<EmbeddingServiceAvailabilityException>(
            () => service.GenerateBatchAsync(["pressure"]));

        Assert.Contains("relative", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Batch_and_input_limits_are_enforced()
    {
        var service = CreateService(
            new RecordingHandler(_ => throw new InvalidOperationException()),
            new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "local-provider",
                BaseAddress = "http://localhost:8080",
                Model = "local-model",
                Dimensions = 2,
                MaxBatchSize = 1,
                MaxInputCharacters = 4
            });

        await Assert.ThrowsAsync<EmbeddingVectorValidationException>(
            () => service.GenerateBatchAsync(["one", "two"]));
        await Assert.ThrowsAsync<EmbeddingVectorValidationException>(
            () => service.GenerateBatchAsync(["five!"]));
    }

    [Fact]
    public async Task Model_length_is_validated_before_network_access()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Network should not be called."));
        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "local-provider",
                BaseAddress = "http://localhost:8080",
                Model = new string('m', 257),
                Dimensions = 2
            });

        var exception = await Assert.ThrowsAsync<EmbeddingServiceAvailabilityException>(
            () => service.GenerateBatchAsync(["pressure"]));

        Assert.Contains("256", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Provider_errors_and_invalid_responses_are_typed()
    {
        var errorService = CreateService(
            new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)),
            new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "local-provider",
                BaseAddress = "http://localhost:8080",
                Model = "local-model",
                Dimensions = 2
            });
        await Assert.ThrowsAsync<EmbeddingServiceExecutionException>(
            () => errorService.GenerateBatchAsync(["pressure"]));

        var invalidService = CreateService(
            new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[]}")
            }),
            new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "local-provider",
                BaseAddress = "http://localhost:8080",
                Model = "local-model",
                Dimensions = 2
            });
        await Assert.ThrowsAsync<EmbeddingVectorValidationException>(
            () => invalidService.GenerateBatchAsync(["pressure"]));
    }

    private static OpenAiCompatibleEmbeddingService CreateService(
        HttpMessageHandler handler,
        EmbeddingOptions options)
    {
        return new OpenAiCompatibleEmbeddingService(
            new HttpClient(handler),
            Options.Create(options));
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                body,
                request.Headers.Authorization?.Scheme));
            return responder(request);
        }
    }

    private sealed record RecordedRequest(string Body, string? AuthorizationScheme);
}
