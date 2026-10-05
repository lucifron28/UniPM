using UniPM.Api.Features.Schedules;
using UniPM.Api.Features.ReferenceData;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.Reports;

internal abstract class NaturalLanguageAnalyticsInterpretationPipeline
    : INaturalLanguageAnalyticsInterpreter
{
    public async Task<PmAnalyticsInterpretationResult> InterpretAsync(
        string question,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(question)
            || question.Length > 512
            || question.Any(char.IsControl))
        {
            return new PmAnalyticsInterpretationResult(
                PmAnalyticsInterpretationStatus.Unsupported,
                null,
                [],
                null,
                "InvalidQuestion");
        }

        var assessment = NaturalLanguageAnalyticsQuestionGuard.Assess(question);
        if (assessment.FixedResult is not null)
        {
            return assessment.FixedResult;
        }

        var candidate = await InterpretCandidateAsync(
            assessment.SanitizedQuestion,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (candidate is null || candidate.ClarificationFields is null)
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
        }

        if (candidate.Status == PmAnalyticsInterpretationStatus.Unsupported)
        {
            if (candidate.Plan is not null
                || candidate.ClarificationFields.Count > 0
                || candidate.Presentation is not null)
            {
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
            }

            return candidate with { Code = "RequestNotSupported" };
        }

        if (candidate.Status == PmAnalyticsInterpretationStatus.NeedsClarification)
        {
            var fields = candidate.ClarificationFields.Distinct().ToArray();
            if (candidate.Plan is not null
                || candidate.Presentation is not null
                || fields.Length == 0
                || fields.Any(field => !Enum.IsDefined(field)))
            {
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
            }

            return candidate with { ClarificationFields = fields, Code = null };
        }

        if (candidate.Status != PmAnalyticsInterpretationStatus.Valid
            || candidate.Plan is null
            || candidate.ClarificationFields.Count != 0
            || candidate.Presentation is null)
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
        }

        if (assessment.ExplicitAssetCategory is null)
        {
            return NaturalLanguageAnalyticsQuestionGuard.NeedsClarification(
                PmAnalyticsClarificationField.AssetCategory) with { Usage = candidate.Usage };
        }

        if (assessment.ExplicitYear is null)
        {
            return NaturalLanguageAnalyticsQuestionGuard.NeedsClarification(
                PmAnalyticsClarificationField.Year) with { Usage = candidate.Usage };
        }

        if (assessment.ExplicitMonth is null)
        {
            return NaturalLanguageAnalyticsQuestionGuard.NeedsClarification(
                PmAnalyticsClarificationField.Month) with { Usage = candidate.Usage };
        }

        if (!PmAnalyticsPlanValidator.TryNormalize(candidate.Plan, out var plan, out _))
        {
            if (PreventiveMaintenanceCycle.TryParse(candidate.Plan.PmCycle, out var year, out var month)
                && AssetCategoryCatalog.TryNormalize(candidate.Plan.AssetCategory, out var category)
                && (year == 9999 && month == 12
                    || !CpmpScheduleFrequency.IsValid(category, month)))
            {
                return new PmAnalyticsInterpretationResult(
                    PmAnalyticsInterpretationStatus.Unsupported,
                    null,
                    [],
                    null,
                    "UnsupportedPmCycle",
                    candidate.Usage);
            }

            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
        }

        if (!NaturalLanguageAnalyticsQuestionGuard.MatchesExplicitScope(assessment, plan))
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
        }

        var expectedPresentation = NaturalLanguageAnalyticsQuestionGuard.PresentationFor(
            assessment.SanitizedQuestion,
            plan.Metric);
        if (candidate.Presentation != expectedPresentation)
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
        }

        if (!PmAnalyticsCanonicalQuestion.TryCreate(plan, out _))
        {
            return new PmAnalyticsInterpretationResult(
                PmAnalyticsInterpretationStatus.Unsupported,
                null,
                [],
                null,
                "UnsafeDepartment",
                candidate.Usage);
        }

        return candidate with { Plan = plan, Code = null };
    }

    protected abstract Task<PmAnalyticsInterpretationResult> InterpretCandidateAsync(
        string sanitizedQuestion,
        CancellationToken cancellationToken);
}

internal sealed class RuleBasedNaturalLanguageAnalyticsInterpreter()
    : NaturalLanguageAnalyticsInterpretationPipeline
{
    protected override Task<PmAnalyticsInterpretationResult> InterpretCandidateAsync(
        string sanitizedQuestion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!PmAnalyticsQuestionParser.TryParse(sanitizedQuestion, out var plan, out _)
            || plan is null)
        {
            return Task.FromResult(new PmAnalyticsInterpretationResult(
                PmAnalyticsInterpretationStatus.Unsupported,
                null,
                [],
                null,
                "QuestionNotSupported"));
        }

        return Task.FromResult(new PmAnalyticsInterpretationResult(
            PmAnalyticsInterpretationStatus.Valid,
            plan,
            [],
            NaturalLanguageAnalyticsQuestionGuard.PresentationFor(sanitizedQuestion, plan.Metric),
            null));
    }
}

internal sealed class ConfiguredNaturalLanguageAnalyticsInterpreter(
    IOptionsMonitor<NaturalLanguageAnalyticsOptions> options,
    RuleBasedNaturalLanguageAnalyticsInterpreter ruleBased,
    OllamaNaturalLanguageAnalyticsInterpreter ollama,
    IHostEnvironment environment)
    : INaturalLanguageAnalyticsInterpreter
{
    public Task<PmAnalyticsInterpretationResult> InterpretAsync(
        string question,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return ruleBased.InterpretAsync(question, cancellationToken);
        }

        bool enabled;
        try
        {
            enabled = options.CurrentValue.Enabled;
        }
        catch (OptionsValidationException)
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }
        catch (InvalidOperationException)
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }

        return enabled
            ? ollama.InterpretAsync(question, cancellationToken)
            : ruleBased.InterpretAsync(question, cancellationToken);
    }
}
