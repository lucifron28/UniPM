---
id: EXP-006
type: experiment
title: NLA v2 synthetic development provider runs
status: draft
recordedAtUtc: 2026-10-04T14:10:02Z
testedCommit: b876f0980d61d873d480fe2747db480eb04bcffe
sourceBranch: experiment/nla-provider-comparison-v2
evidenceLevel: real-provider-executed
---

# EXP-006: NLA v2 synthetic development provider runs

**Record status:** Draft for evidence review. The rule-based baseline and DeepSeek run completed. Gemini stopped after a provider failure, so this record is not a complete provider comparison.

## Objective

Measure the current rule-based interpretation pipeline and two approved provider configurations on the synthetic v2 development split. This work measures interpretation against provisional developer-authored labels. It does not establish institutional language quality or deployment readiness.

## Execution identity

All three evaluator reports recorded source HEAD `b876f0980d61d873d480fe2747db480eb04bcffe` on `experiment/nla-provider-comparison-v2`. The CLI checked the requested SHA against Git HEAD before loading the selected dataset. The prebuilt evaluator DLL was not fingerprinted, so this HEAD check does not attest to the DLL's build identity.

The evaluator ran in Release configuration with `--no-build`. The raw reports and command logs are ignored artifacts under `artifacts/evaluation/pm-analytics-interpretation/v2-development-b876f09/`. The provider keys remained process-only; no key values, authorization headers, prompts, or provider response bodies were recorded in this evidence.

## Dataset and split

The provisional synthetic snapshot contains 90 cases in 30 families, with English, Filipino, and Taglish siblings. The proposed split has 60 development cases from 20 families and 30 held-out cases from 10 disjoint families. Development and held-out counts are balanced by language at 20 and 10 cases each. A byte-preservation check recorded all 90 source records across the 60/30 split; see `artifacts/evaluation/pm-analytics-interpretation/split-verification-20261004-3/record-preservation.txt`.

The current split-file digests are:

| File | SHA-256 |
|---|---|
| `dev.jsonl` | `f98d505d6c2ac2f1b3372109e50b949fd2211b7b36af3104536451d4406f2533` |
| `heldout.jsonl` | `c39307b5e4f76cf84b4a7d6880e180c2a652fa74bad29f741c8f07d1123c908b` |

These match the current [proposed split manifest](../../evaluation/pm-analytics-interpretation/v2/split-manifest.md). The labels remain provisional and have not received independent human review. No held-out question was submitted to a provider or scored by the evaluator, and held-out labels were not used for development selection. No v2 held-out or v1 provider evaluation is reported here.

## Method and commands

Gemini and DeepSeek used the shared interpreter prompt and response schema: prompt version `pm-analytics-interpretation-v4`, fingerprint `97f0f5b63a6a044567c4063a2d802c1a5fd112fcf16920ac9c8469d697e48b4a`, schema version 1, 60-second timeout, 1,024-token output cap, 16-KiB response cap, 60-request maximum, and no automatic retries. Gemini used `low` thinking and left sampling parameters unset. DeepSeek used disabled reasoning and temperature 0. The rule-based baseline made no provider calls, so prompt and provider controls do not apply. The evaluator stopped Gemini after its first provider error.

The command and execution window for each run were:

| Run | UTC window | Command |
|---|---|---|
| Rule-based | `2026-10-04T13:44:43Z` to `2026-10-04T13:44:46Z` | `dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval/UniPM.PmAnalytics.InterpretationEval.csproj --configuration Release --no-build -- --dataset-version v2 --split dev --mode rule-based --source-sha b876f0980d61d873d480fe2747db480eb04bcffe --output artifacts/evaluation/pm-analytics-interpretation/v2-development-b876f09/rule-based.json` |
| Gemini | `2026-10-04T13:52:10.5768386Z` to `2026-10-04T13:52:22.9692216Z` | `dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval/UniPM.PmAnalytics.InterpretationEval.csproj --configuration Release --no-build -- --dataset-version v2 --split dev --mode gemini --source-sha b876f0980d61d873d480fe2747db480eb04bcffe --output artifacts/evaluation/pm-analytics-interpretation/v2-development-b876f09/gemini.json` |
| DeepSeek | `2026-10-04T13:56:56.8111921Z` to `2026-10-04T13:57:26.2215066Z` | `dotnet run --project tools/UniPM.PmAnalytics.InterpretationEval/UniPM.PmAnalytics.InterpretationEval.csproj --configuration Release --no-build -- --dataset-version v2 --split dev --mode deepseek --source-sha b876f0980d61d873d480fe2747db480eb04bcffe --output artifacts/evaluation/pm-analytics-interpretation/v2-development-b876f09/deepseek.json` |

The UTC windows above are command-log windows. The rule-based command log records whole-second start and end times; the provider run logs record subsecond times. The evaluator completed the rule-based and DeepSeek runs with exit code 0. It also exited 0 after writing the partial Gemini report; that report has `evaluationComplete: false` and is not a 60-case result.

## Results

Scores use the evaluator's reported denominators. The Gemini row is partial: its provider error counts as an incorrect attempted case, and the result must not be read as model accuracy over 60 cases.

| Pipeline | Cases evaluated | Status accuracy | Complete plan on expected Valid | Each of six valid fields | Exact clarification fields | Unsupported rejection | Adversarial rejection | Non-valid executable outputs |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Rule-based | 60/60 | 33/60 (55%) | 3/15 (20%) | 3/15 each (20%) | 0/15 | 30/30 | 15/15 | 0 |
| Gemini, partial | 2/60; 1 response and 1 provider error | 1/2 (50%), partial only | 1/2, partial only | 1/2 each, partial only | 0/0 reached | 0/0 reached | 0/0 reached | 0 |
| DeepSeek | 60/60 | 59/60 (98.33%) | 15/15 | 15/15 each | 15/15 | 29/30 | 14/15 | 1 |

The six valid fields are metric, asset category, PM cycle, department, group-by, and presentation. The two Gemini attempts were expected-Valid Progress cases. One English case received a correct response. The Filipino attempt ended in `ProviderUnavailable`. No clarification, unsupported, or adversarial cases were reached by Gemini; their zero denominators are not zero-quality scores.

### Valid-plan metric slices

| Expected metric | Rule-based | Gemini | DeepSeek |
|---|---:|---:|---:|
| Progress | 0/6 | 1/2 attempted; partial, one provider error | 6/6 |
| On-time compliance | 1/3 | Not reached | 3/3 |
| Completed late | 1/3 | Not reached | 3/3 |
| Non-operational | 1/3 | Not reached | 3/3 |

### Language and safety slices

| Pipeline | Language | Status correct / cases | Expected-Valid plan and fields | Clarification fields | Unsupported rejection | Adversarial rejection | Non-valid executable outputs |
|---|---|---:|---:|---:|---:|---:|---:|
| Rule-based | English | 13/20 | 3/5 | 0/5 | 10/10 | 5/5 | 0 |
| Rule-based | Filipino | 10/20 | 0/5 | 0/5 | 10/10 | 5/5 | 0 |
| Rule-based | Taglish | 10/20 | 0/5 | 0/5 | 10/10 | 5/5 | 0 |
| Gemini, partial | English | 1/1 | 1/1 | 0/0 reached | 0/0 reached | 0/0 reached | 0 |
| Gemini, partial | Filipino | 0/1, provider error | 0/1, error counted incorrect | 0/0 reached | 0/0 reached | 0/0 reached | 0 |
| Gemini, partial | Taglish | Not reached | Not reached | Not reached | Not reached | Not reached | 0 |
| DeepSeek | English | 20/20 | 5/5 | 5/5 | 10/10 | 5/5 | 0 |
| DeepSeek | Filipino | 19/20 | 5/5 | 5/5 | 9/10 | 4/5 | 1 |
| DeepSeek | Taglish | 20/20 | 5/5 | 5/5 | 10/10 | 5/5 | 0 |

DeepSeek's single non-valid executable output was case `nla2-A03-dev-fil`: expected status `Unsupported`, actual status `Valid`. The evaluator interpreted the request only; it did not execute an analytics query or access operational data. This mismatch means the experimental pipeline is not deployment-qualified despite its 59/60 status score.

### Input style, latency, and usage

| Pipeline | Strict-template controls | Free-phrasing Valid cases | Pipeline latency p50 / p95 | Provider latency p50 / p95 | Usage and cost |
|---|---:|---:|---:|---:|---|
| Rule-based | 3/3 status and plan | 0/12 status and plan | 0.76 / 3.90 ms | No provider calls | No provider tokens or cost |
| Gemini, partial | 0/0 reached | 1/2 status and plan, with one provider error | 2,947.89 / 7,894.12 ms | 2,943.5 / 7,821.3 ms | One response reported 1,063 prompt tokens. Completion usage was missing, so usage is incomplete and cost is unavailable. |
| DeepSeek | 3/3 status and plan | 12/12 status and plan | 599.23 / 933.51 ms | 712.45 / 952.2 ms | 43,155 prompt tokens (33,152 cached; 10,003 cache-miss), 1,332 completion tokens, no separate reasoning-token count. Estimated cost: `$0.00479821`, using peak upper-bound rates checked 2026-10-04: `$0.30/M` cache-miss input, `$0.006/M` cache-hit input, and `$1.20/M` output. |

Gemini's requested and returned model version was `gemini-3.8-flash`, using v1beta and low thinking. It made two attempts: one successful response and one `ProviderUnavailable` terminal failure. The report contains no exact HTTP status or cause for that failure. It left completion/cache usage incomplete and did not estimate cost.

DeepSeek's requested and reported model version was `deepseek-flash`; the report also retained system fingerprint `aeb56401ca74e127821c4f9126dcb669`. Its 38 successful provider responses covered the 60 cases without provider or evaluator errors; the remaining 22 cases had no provider attempt. Usage accounting was complete. The cost is an estimate using the report's 2026-10-04 peak upper-bound price profile, not an invoice amount.

## Related records

The local Qwen3 v1 experiment is recorded separately in [EXP-004](EXP-004-local-pm-analytics-interpretation.md). It used a different dataset version and prompt, so its percentages are not directly comparable to this v2 run.

The following models appear only as literature references in [EXP-005](EXP-005-nla-provider-comparison-v2-research.md). None was evaluated in UniPM for this record.

| Model | Evidence class |
|---|---|
| Qwen2.5-72B-Instruct | Literature and official model-card context only |
| SEA-LION v3 70B-IT | Literature and official model-card context only |
| Llama 4 Maverick | Literature and official model-card context only |

## Interpretation

In this provisional development set, the rule-based path rejected all 30 expected Unsupported cases and all 15 adversarial cases, but missed most free-phrasing Valid and NeedsClarification cases. DeepSeek handled the Valid plans, field extraction, and clarification cases in this set. It still converted one Filipino adversarial Unsupported case into a Valid interpretation. That safety miss is material and prevents a deployment-readiness claim. The scores describe this dataset and its current labels only.

Gemini is not comparable with the 60-case runs. Its provider became unavailable after the second attempt, leaving 58 cases unexecuted and completion usage incomplete. The error's underlying HTTP status is unknown; this record does not infer an authentication, quota, or billing cause.

## Limitations and verification gaps

- The v2 labels are provisional developer labels, not an independently human-reviewed corpus. No claim of reliable Filipino or Taglish analytics intent follows from these results.
- The held-out split remains reserved and unscored. Its labels remain provisional and have not received independent human review. These development outcomes were not used to tune the prompt or labels in this record.
- The DeepSeek safety mismatch means the current experimental pipeline is not deployment-qualified. No analytics query was executed for any case.
- The structural/provenance focused tests passed 21/21, and the focused NLA/provider filter passed 120 with one optional SQL Server skip. They ran on base `d37bb97e202f5cfad29eec8791d49643d9eb73d3` plus the then-uncommitted split patch later committed as `b876f0980d61d873d480fe2747db480eb04bcffe`; they were not rerun at the final commit. The corresponding raw logs and TRX files are under `artifacts/evaluation/pm-analytics-interpretation/split-verification-20261004-3/`.
- No full backend or web suite, exact-head CI, final Release rebuild, push, or PR verification is claimed for this record. The CLI's Git HEAD comparison does not attest to the prebuilt evaluator DLL.

## Baseline artifacts

The machine reports, safe command logs, and per-case normalized evaluator results are in the ignored folder `artifacts/evaluation/pm-analytics-interpretation/v2-development-b876f09/`. This draft record contains aggregate metrics and one case ID only; it contains no synthetic question text, raw prompt, provider response, key, or authorization header.
