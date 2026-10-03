using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;

namespace UniPM.Api.Tests;

public sealed partial class PmAnalyticsTests
{
    [Fact]
    public async Task Anonymous_interpretation_requests_receive_401()
    {
        var sentinel = new SentinelInterpreter(ValidResult());
        await using var application = TestApplicationFactory.UnauthenticatedWithInterpreter(sentinel);
        using var client = application.CreateClient();

        using var response = await InterpretAsync(
            client,
            "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, sentinel.CallCount);
    }

    [Fact]
    public async Task Non_gsd_roles_cannot_invoke_the_interpreter()
    {
        var sentinel = new SentinelInterpreter(ValidResult());
        await using var application = TestApplicationFactory.AuthenticatedWithInterpreter(
            ClosedPeriodNow,
            sentinel,
            AuthRoleCatalog.Inspector);
        using var client = application.CreateClient();

        using var response = await InterpretAsync(
            client,
            "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, sentinel.CallCount);
    }

    [Fact]
    public async Task Gsd_can_interpret_a_strict_question()
    {
        var sentinel = new SentinelInterpreter(ValidResult());
        await using var application = TestApplicationFactory.AuthenticatedWithInterpreter(
            ClosedPeriodNow,
            sentinel,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        using var response = await InterpretAsync(
            client,
            "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PmAnalyticsInterpretationResponse>();
        Assert.NotNull(result);
        Assert.Equal("Valid", result.Status);
        Assert.Equal("Progress", result.Plan?.Metric);
        Assert.Equal("2026-11", result.Plan?.PmCycle);
        Assert.Equal("Percent", result.Presentation);
        Assert.Equal("Show progress for fire-extinguisher in 2026-11", result.CanonicalQuestion);
        Assert.Null(result.Code);
        Assert.Equal(1, sentinel.CallCount);
    }

    [Fact]
    public async Task Provider_failures_return_a_safe_service_unavailable_response()
    {
        var sentinel = new SentinelInterpreter(
            failure: new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable));
        await using var application = TestApplicationFactory.AuthenticatedWithInterpreter(
            ClosedPeriodNow,
            sentinel,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        using var response = await InterpretAsync(
            client,
            "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal("ProviderUnavailable", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("provider payload", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, sentinel.CallCount);
    }

    [Fact]
    public async Task Interpretation_request_rejects_unmapped_json_members()
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(
            ClosedPeriodNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/interpret",
            new
            {
                question = "Show progress for fire-extinguisher in 2026-11",
                department = "GSD"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> InterpretAsync(HttpClient client, string question)
    {
        return client.PostAsJsonAsync("/api/v1/analytics/pm/interpret", new { question });
    }

    private static PmAnalyticsInterpretationResult ValidResult()
    {
        return new PmAnalyticsInterpretationResult(
            PmAnalyticsInterpretationStatus.Valid,
            new PmAnalyticsPlan(
                PmAnalyticsMetric.Progress,
                "fire-extinguisher",
                "2026-11",
                null,
                PmAnalyticsGroupBy.None),
            [],
            PmAnalyticsPresentation.Percent,
            null);
    }

    private sealed class SentinelInterpreter(
        PmAnalyticsInterpretationResult? result = null,
        Exception? failure = null) : INaturalLanguageAnalyticsInterpreter
    {
        internal int CallCount { get; private set; }

        public Task<PmAnalyticsInterpretationResult> InterpretAsync(
            string question,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (failure is not null)
            {
                return Task.FromException<PmAnalyticsInterpretationResult>(failure);
            }

            return Task.FromResult(result!);
        }
    }
}
