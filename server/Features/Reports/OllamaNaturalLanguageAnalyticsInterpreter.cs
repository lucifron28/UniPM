using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.Reports;

internal sealed class OllamaNaturalLanguageAnalyticsInterpreter : ModelNaturalLanguageAnalyticsInterpreter
{
    internal new const string PromptVersion = ModelNaturalLanguageAnalyticsInterpreter.PromptVersion;
    internal const int GenerationTemperature = 0;
    internal const int GenerationSeed = 42;
    internal const int ContextTokens = 4096;

    internal new static string SystemPrompt => ModelNaturalLanguageAnalyticsInterpreter.SystemPrompt;

    internal new static string PromptFingerprint => ModelNaturalLanguageAnalyticsInterpreter.PromptFingerprint;

    internal OllamaNaturalLanguageAnalyticsInterpreter(
        HttpClient httpClient,
        IOptionsMonitor<NaturalLanguageAnalyticsOptions> options,
        NaturalLanguageAnalyticsModelClientRunLedger? ledger = null)
        : base(new OllamaNaturalLanguageAnalyticsModelClient(httpClient, options, ledger))
    {
    }
}
