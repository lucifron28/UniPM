using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.MaintenanceReview;

internal sealed class OpenAiCompatibleSummaryService(
    HttpClient httpClient,
    IOptions<SummaryOptions> optionsAccessor)
    : ISummaryService
{
    private readonly SummaryOptions options = optionsAccessor.Value;

    public SummaryServiceDescriptor Descriptor
        => new(
            options.Enabled,
            options.ProviderKey?.Trim() ?? string.Empty,
            options.Model?.Trim() ?? string.Empty);

    public async Task<SummaryGenerationResult> GenerateAsync(
        SummaryGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var thinkingMode = EnsureEnabledAndConfigured();
        var promptLength = request.SystemMessage.Length + request.UserMessage.Length;
        if (string.IsNullOrWhiteSpace(request.SystemMessage)
            || string.IsNullOrWhiteSpace(request.UserMessage)
            || request.TemplateVersion != MaintenanceReviewPromptBuilder.TemplateVersion
            || promptLength > options.MaxPromptCharacters)
        {
            throw new SummaryServiceDataException("The summary prompt is outside the configured bounds.");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = options.Model,
            ["temperature"] = 0,
            ["max_tokens"] = Math.Max(1, options.MaxOutputCharacters / 4),
            ["messages"] = new[]
            {
                new { role = "system", content = request.SystemMessage },
                new { role = "user", content = request.UserMessage }
            }
        };
        if (thinkingMode.Length > 0)
        {
            payload["thinking"] = new { type = thinkingMode };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri())
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellation.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new SummaryServiceExecutionException("The summary provider request timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new SummaryServiceExecutionException(
                "The summary provider request could not be completed.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new SummaryServiceExecutionException("The summary provider returned an unsuccessful response.");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(linkedCancellation.Token);
                using var document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: linkedCancellation.Token);
                if (!document.RootElement.TryGetProperty("choices", out var choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0
                    || !choices[0].TryGetProperty("message", out var message)
                    || !message.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.String)
                {
                    throw new SummaryServiceDataException("The summary provider returned no usable content.");
                }

                var text = content.GetString()?.Trim() ?? string.Empty;
                if (text.Length == 0 || text.Length > options.MaxOutputCharacters)
                {
                    throw new SummaryServiceDataException("The summary provider returned content outside the configured bounds.");
                }

                return new SummaryGenerationResult(text);
            }
            catch (SummaryServiceDataException)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new SummaryServiceExecutionException("The summary provider response timed out.");
            }
            catch (JsonException exception)
            {
                throw new SummaryServiceDataException("The summary provider returned malformed data.", exception);
            }
        }
    }

    private string EnsureEnabledAndConfigured()
    {
        if (!options.Enabled)
        {
            throw new SummaryServiceAvailabilityException("Summary generation is disabled by configuration.");
        }

        var providerKey = options.ProviderKey?.Trim() ?? string.Empty;
        var model = options.Model?.Trim() ?? string.Empty;
        if (providerKey.Length == 0 || providerKey.Length > 64)
        {
            throw new SummaryServiceAvailabilityException("Summary:ProviderKey must be configured as a non-secret value of 64 characters or fewer.");
        }

        if (model.Length == 0 || model.Length > 256)
        {
            throw new SummaryServiceAvailabilityException("Summary:Model must be configured and cannot exceed 256 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseAddress)
            || !Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var baseAddress)
            || baseAddress.Scheme is not ("http" or "https"))
        {
            throw new SummaryServiceAvailabilityException("Summary:BaseAddress must be an absolute HTTP or HTTPS endpoint.");
        }

        if (!Uri.TryCreate(options.Path, UriKind.Relative, out var relativePath)
            || relativePath.IsAbsoluteUri
            || options.Path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new SummaryServiceAvailabilityException("Summary:Path must be a relative request path.");
        }

        var requestUri = new Uri(baseAddress, relativePath);
        if (!string.Equals(requestUri.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(requestUri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase)
            || requestUri.Port != baseAddress.Port)
        {
            throw new SummaryServiceAvailabilityException("Summary:Path must resolve to the configured base endpoint.");
        }

        if (!options.AllowRemoteProvider && !IsLocalEndpoint(requestUri))
        {
            throw new SummaryServiceAvailabilityException("Remote summary endpoints require Summary:AllowRemoteProvider=true and a separate privacy review.");
        }

        if (options.TimeoutSeconds is < 1 or > 300
            || options.MaxPromptCharacters < 1
            || options.MaxOutputCharacters < 1
            || options.MaxSourceTextCharacters < 1)
        {
            throw new SummaryServiceAvailabilityException("Summary timeout and size limits are outside the supported bounds.");
        }

        var thinkingMode = options.ThinkingMode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (thinkingMode is not ("" or "enabled" or "disabled"))
        {
            throw new SummaryServiceAvailabilityException(
                "Summary:ThinkingMode must be empty, enabled, or disabled.");
        }

        return thinkingMode;
    }

    private Uri BuildRequestUri()
        => new(new Uri(options.BaseAddress!, UriKind.Absolute), options.Path);

    private static bool IsLocalEndpoint(Uri endpoint)
        => endpoint.IsLoopback
            || string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);
}
