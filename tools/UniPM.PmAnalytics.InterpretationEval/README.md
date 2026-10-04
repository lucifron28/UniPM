# PM analytics interpretation evaluator

This BCL console tool calls the production INaturalLanguageAnalyticsInterpreter in-process. It does not start the web host, authenticate users, access SQL Server, or call the analytics execution endpoint.

The evaluator's historical v1 modes are:

- rule-based runs the deterministic strict-template parser as a control baseline. It does not measure multilingual model quality.
- ollama calls the production Ollama interpreter using a preloaded local model. It is restricted to HTTP loopback on port 11434, requires a matching local model tag, and never downloads a model.

The historical v1 corpus remains the default and is preserved as prior evidence. Dataset v2 is a separate, provisional 90-case corpus (60 development cases and 30 proposed held-out cases) with language siblings grouped by family. Its English, Filipino, and Taglish labels have not received human review, so it is not frozen and does not establish linguistic validation. V2 held-out execution is rejected before Git, corpus, credential, or provider access.

Gemini and DeepSeek modes use the same sanitized-question interpreter, prompt, output contract, deterministic grounding, plan validator, and evaluator. They are available only with `--dataset-version v2 --split dev`; neither provider can run against v1. Their model, endpoint, output limit, timeout, and request budget are fixed, so `--model` and `--base-address` are rejected for these modes. Gemini reads `GEMINI_API_KEY` from the process environment and uses `gemini-3.8-flash` with low thinking; DeepSeek reads `DEEPSEEK_API_KEY` and uses `deepseek-flash` with thinking disabled and temperature zero. Keys must not be passed on the command line. Each run is limited to 60 requests, a 60-second timeout, 1,024 output tokens, and a 16 KiB response; the API clients do not retry.

Always select exactly one split. Development is for the approved development run. Held-out evaluation must use the frozen implementation and model configuration; do not tune or rerun against held-out results. The evaluator checks the case file SHA-256 against the split manifest before any interpretation starts.

## Invocation

From the repository root, provide the exact full source commit SHA.

    dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval -- --mode rule-based --split dev --source-sha <full-commit-sha>

    dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval -- --mode ollama --split dev --source-sha <full-commit-sha> --model qwen3:4b-instruct

    dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval -- --dataset-version v2 --mode rule-based --split dev --source-sha <full-commit-sha>

    dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval -- --dataset-version v2 --mode gemini --split dev --source-sha <full-commit-sha>

    dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval -- --dataset-version v2 --mode deepseek --split dev --source-sha <full-commit-sha>

Optional arguments are shown by --help. The default report path is under ignored artifacts/evaluation/pm-analytics-interpretation/. The local model must already be installed; the tool does not download or start Ollama.

Before it reads the corpus, fetches model metadata, creates a provider client, or writes a report, the evaluator runs native Git at the discovered repository root and compares `rev-parse --verify HEAD^{commit}` with `--source-sha`. A mismatch, unavailable Git command, or unavailable repository revision stops setup with a fixed code. The report records the verified HEAD. This checks the checkout commit; it does not attest that a prebuilt evaluator binary came from that commit or that the working tree is clean.

A zero exit code means a report was written. It is not a quality threshold or pass claim.

## Report interpretation

Overall status accuracy uses every executed selected case; skipped cases marked `NotExecuted` are excluded from all score denominators. Complete-plan accuracy uses executed expected Valid cases only. Exact clarification-field accuracy uses executed expected NeedsClarification cases only. Unsupported rejection uses executed expected Unsupported cases, including the Adversarial subset. Per-field Valid accuracy includes metric, category, cycle, department/null, groupBy, and presentation. Provider and evaluator errors remain distinct from Unsupported and count as zero correct against the expected denominators.

The v1 report compares strict-template English controls separately from free-phrasing Valid cases; both remain in the aggregate Valid denominator. It also records language and case-class groups, status confusion, latency percentiles, estimated provider attempts, successful model responses, deterministic-guard cases, and rule-based cases. Estimated attempts count interpreter paths that reached the provider boundary; a configuration or budget error may occur before an HTTP request.

Ollama reports include the local model tag and digest, runtime version, prompt version and fingerprint, fixed generation settings, and optional token and duration usage. Usage totals are marked unavailable when successful responses lack usage metadata; failed responses do not have complete token totals. Cost is null for this local provider. The report contains case IDs, provisional expected labels, validated normalized plans, enum-valued predictions, correctness flags, and fixed failure codes. It omits question text, prompts, model-generated prose, credentials, endpoint values, and raw provider payloads. The corpus labels are synthetic developer labels, not independent human validation; the results do not establish institutional language quality.

V2 reports retain the v1 scoring definitions and add score breakdowns by language, input style, expected status, and expected metric. They distinguish planned, evaluated, and not-executed cases. A terminal provider failure makes the quality evaluation incomplete; remaining cases are marked not executed and excluded from score denominators and case latency. An HTTP-success response with malformed interpretation output is scored as a model failure and does not by itself make a fully processed run incomplete. Usage completeness and estimated cost are reported separately. Cost estimates use a dated 2026-10-04 profile: Gemini is priced at the standard full-input-rate upper bound, while DeepSeek uses peak-rate upper bounds and prices unattributed input as cache misses. If billed input or output token usage is missing or invalid, estimated cost is unavailable. These estimates are not invoices.

V2 cloud reports record the provider/model and API version when known, common prompt fingerprint and output-schema version, fixed generation settings, actual request attempts, per-attempt usage and latency, provider-reported version/fingerprint where available, and failure codes. They do not include raw questions, prompts, responses, credentials, or endpoint values. Running the development split does not authorize or freeze the proposed held-out split.
