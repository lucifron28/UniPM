# PM analytics interpretation evaluator

This BCL console tool calls the production INaturalLanguageAnalyticsInterpreter in-process. It does not start the web host, authenticate users, access SQL Server, or call the analytics execution endpoint.

The evaluator has two explicit modes:

- rule-based runs the deterministic strict-template parser as a control baseline. It does not measure multilingual model quality.
- ollama calls the production Ollama interpreter using a preloaded local model. It is restricted to HTTP loopback on port 11434, requires a matching local model tag, and never downloads a model.

Always select exactly one split. Development is for the approved development run. Held-out evaluation must use the frozen implementation and model configuration; do not tune or rerun against held-out results. The evaluator checks the case file SHA-256 against the split manifest before any interpretation starts.

## Invocation

From the repository root, provide the exact full source commit SHA.

    dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval -- --mode rule-based --split dev --source-sha <full-commit-sha>

    dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval -- --mode ollama --split dev --source-sha <full-commit-sha> --model qwen3:4b-instruct

Optional arguments are shown by --help. The default report path is under ignored artifacts/evaluation/pm-analytics-interpretation/. The local model must already be installed; the tool does not download or start Ollama.

A zero exit code means a report was written. It is not a quality threshold or pass claim.

## Report interpretation

Overall status accuracy uses every selected case. Complete-plan accuracy uses expected Valid cases only. Exact clarification-field accuracy uses expected NeedsClarification cases only. Unsupported rejection uses expected Unsupported cases, including the Adversarial subset. Per-field Valid accuracy includes metric, category, cycle, department/null, groupBy, and presentation. Provider and evaluator errors remain distinct from Unsupported and count as zero correct against the expected denominators.

The report compares the five strict-template English controls separately from free-phrasing Valid cases; both remain in the aggregate Valid denominator. It also records language and case-class groups, status confusion, latency percentiles, estimated provider attempts, successful model responses, deterministic-guard cases, and rule-based cases. Estimated attempts count interpreter paths that reached the provider boundary; a configuration or budget error may occur before an HTTP request.

Ollama reports include the local model tag and digest, runtime version, prompt version and fingerprint, fixed generation settings, and optional token and duration usage. Usage totals are marked unavailable when successful responses lack usage metadata; failed responses do not have complete token totals. Cost is null for this local provider. The report contains case IDs, provisional expected labels, validated normalized plans, enum-valued predictions, correctness flags, and fixed failure codes. It omits question text, prompts, model-generated prose, credentials, endpoint values, and raw provider payloads. The corpus labels are synthetic developer labels, not independent human validation; the results do not establish institutional language quality.
