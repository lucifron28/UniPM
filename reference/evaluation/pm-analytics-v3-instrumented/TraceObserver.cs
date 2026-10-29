using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UniPM.Api.Features.Reports;

// Evaluation-only source overlay. This file is never compiled by server/server.csproj.
internal static class Test182Trace
{
    private sealed class State(string id)
    {
        public string Id { get; } = id;
        public Stopwatch Clock { get; } = Stopwatch.StartNew();
        public List<object> Events { get; } = [];
        public int ObserverErrors { get; set; }
    }
    private static readonly AsyncLocal<State?> Current = new();
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task Observe(HttpContext context, Func<Task> next)
    {
        var id = context.Request.Headers["X-Test182-Request"].ToString();
        var directory = Environment.GetEnvironmentVariable("TEST182_TRACE_DIRECTORY");
        if (directory is null || context.Request.Path != "/api/v1/analytics/pm/v3/interpret"
            || !Regex.IsMatch(id, @"^(V3D|V3U)-[0-9]{3}-(DeterministicParser|DeepSeek)$"))
        {
            await next();
            return;
        }
        var state = new State(id);
        Current.Value = state;
        try { await next(); }
        finally
        {
            state.Clock.Stop();
            try
            {
                var data = new { requestId = id, serverElapsedMs = state.Clock.Elapsed.TotalMilliseconds,
                    httpStatus = context.Response.StatusCode, observerErrors = state.ObserverErrors,
                    events = state.Events };
                using var file = new FileStream(Path.Combine(directory, id + ".json"), FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(file, data, Options);
            }
            catch { Console.Error.WriteLine("TEST182 observer persistence failed; measurement unavailable."); }
            Current.Value = null;
        }
    }

    public static void Event(string stage, object? value)
    {
        var state = Current.Value;
        if (state is null) return;
        try
        {
            state.Events.Add(new { stage, elapsedMs = state.Clock.Elapsed.TotalMilliseconds,
                value = JsonSerializer.SerializeToElement(value, Options) });
        }
        catch { state.ObserverErrors++; }
    }

    public static void ProviderBody(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            JsonElement? Pick(string name) => root.TryGetProperty(name, out var item) ? item.Clone() : null;
            // Allowlist only response metadata. Never retain requests, headers, prompts or reasoning.
            Event("providerReportedMetadata", new { usage = Pick("usage"), model = Pick("model"),
                systemFingerprint = Pick("system_fingerprint"), created = Pick("created") });
        }
        catch { Event("providerMetadataUnavailable", new { reason = "ResponseNotJson" }); }
    }
}
