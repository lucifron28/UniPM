---
id: TEST-142
type: test-run
title: Supplementary live DeepSeek V3 development comparison
status: executed
recordedAtUtc: 2026-10-23T04:44:07.6078074Z
testedCommit: c47633cec9822d7445b6f8f03f528ad4ff455313
sourceBranch: feature/deepseek-manuscript-evaluation
evidenceLevel: real-provider-executed
---

# Supplementary live DeepSeek V3 development comparison

## Authorization and source identity

The researcher explicitly approved starting the 18-question DeepSeek
comparison in chat on October 23, 2026. The committed
[protocol](../../evaluation/manuscript-2026-10-23/deepseek-comparison-protocol.md)
set a US$1 conservative estimated budget ceiling, at most 18 provider HTTP
requests, and no retries. The run began from a clean checkout of the tested
commit. That commit adds evaluation tooling and the protocol only; it does
not change the production interpreter, prompt or labels.

The task branch starts from updated main and includes committed work from
`feature/nla-guided-interpretation` through
`6437fa7b1e9585a6d4ab7aa377fe0d99dcae1503` to retain the reviewed corpus and
actual interpreter. Existing uncommitted Web/mobile edits in the original
checkout were excluded. No merge to main, deployment, or live database
operation occurred.

The frozen corpus SHA-256 remains
`dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25`,
identical to TEST-139. The 18 questions, expectations and 69 declared plan
assertions were unchanged. Earlier diagnostic revisions to V3D-008 and
V3D-009 remain disclosed. No model result was used to alter a label or rerun
a failed case.

## Commands and retained artifacts

```powershell
dotnet build tools/UniPM.PmAnalytics.V3Evaluation/UniPM.PmAnalytics.V3Evaluation.csproj --nologo -v minimal
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll deepseek artifacts/deepseek-v3-2026-10-23
```

Build exit 0, with zero warnings and errors. Evaluation exit 0, indicating that
all 18 cases completed without provider/execution errors. This is not a claim
that all expectations passed. The evaluation ran once, from
`2026-10-23T04:43:44.9799961Z` to `2026-10-23T04:44:07.6078074Z`.

Raw build and evaluation logs are ignored under
`artifacts/deepseek-v3-preparation-2026-10-23/`. Identity, typed observations,
report, persistent request count and `SHA256SUMS.txt` are ignored under
`artifacts/deepseek-v3-2026-10-23/`. No raw request bodies, provider payloads,
credentials, authorization headers or token maps were persisted.

| Identity | SHA-256 |
|---|---|
| Evaluator assembly | `cab5686a9827273f1b09f76d8ab30c88e4d19b3e3f0ff9d5b4828bd28ac40a30` |
| Backend assembly | `cacf7a25b51324b2b982ad1fcd156b2ef49ef7d2389ccf8b3fd0948f23d7d64b` |
| V3 system prompt | `f2ae538c29b7764d16c5a9e11ff916a260e1ab328f6434be47e482147c328b55` |
| Raw local report | `5297c2595d198b2fb666ef2fc50d4adfb73356250768681d67aa169dfbe31ae1` |
| Sanitized observations JSON | `61c22b191eb4026888aa7eadf50a85678533a9be6009e4e632e6cfec3b9683e3` |
| Case-comparison CSV | `c02ebe034ff41115f3c36ba3c465d549994b37d3f518740687d495d2c705d35e` |

## Execution conditions

The evaluator instantiated the actual `PmAnalyticsV3QuestionInterpreter` in
Development and explicitly selected DeepSeek for each case, bypassing saved
exact-question mappings. It injected the existing DeepSeek client through the
same private-field mechanism as the earlier smoke runner, using an observation
handler that blocked a second HTTP attempt for any case. Production retry
behavior was unchanged. Sanitization and the safety guard remained active.

The run used schema 3, catalog 2, the existing `PmAnalyticsV3ModelPrompt`,
configured `deepseek-flash`, temperature 0, thinking disabled, 1024 maximum
output tokens, 32 KiB request limit, 16 KiB response limit and a 60-second
per-call timeout. The provider reported model alias `deepseek-flash` and
fingerprint `e72d3c7f663d3e1383b00b3884cb4501` for all 17 responses.
No provider-side version beyond the alias was returned. These identities do
not establish which model version served the earlier respondent sessions.

Interpretation time was fixed to the corpus instant, June 1, 2025 UTC. A
disposable EF InMemory catalog contained one fictional fire-extinguisher asset
in North Block and no Research department. It supported catalog resolution,
without live SQL access. No report preview, confirmation or execution ran.

Two stages were retained. Typed model candidates were captured in memory after
the existing output parser. Final responses came from the actual interpreter
after constraints, plan validation and catalog resolution. Both used the
original exact comparison implementation. The deterministic TEST-139 result
stopped at the guard/rule-parser boundary and did not include catalog
resolution, so the stage distinction matters.

## Results

| Measure | Observed result |
|---|---|
| Cases evaluated | 18 |
| Cases with provider responses | 17 |
| Pre-provider safety rejections | 1, V3D-018 |
| Actual HTTP requests / retries / provider errors | 17 / 0 / 0 |
| Final status matches | 16/18, 88.89% |
| Final clarification-set matches | 15/18, 83.33% |
| Final plan-disposition matches | 16/18, 88.89% |
| Final declared plan assertion matches | 67/69, 97.10% |
| Final cases matching all declared expectations | 13/18, 72.22% |
| Exact final matches among provider-exposed cases only | 12/17, 70.59% |
| Typed model candidate status and exact-case matches | 9/17, 52.94%, for each measure |
| Typed model candidate declared assertion matches | 68/69, 98.55% |
| Expected unsupported cases finally rejected without a plan | 3/3; only two reached the provider |
| Unexpected executable final interpretation on an ambiguous case | 1, V3D-011 |

There were no unavailable or skipped cases. Backend controls changed the model
classification in six cases, V3D-007 through V3D-010, V3D-015 and V3D-016.
The unchanged frozen comparison retains all five final differences:

- V3D-005 requested time-basis clarification instead of returning Valid. All
  six declared plan assertions still matched.
- V3D-006 changed the building value from North Block to NORTH BLOCK during
  catalog normalization. The model candidate matched the label.
- V3D-007 requested Period clarification after the backend rejected the model's
  continuous-range interpretation. Its draft represented records with no
  completed-count measure, differing from the asserted draft measure.
- V3D-011 returned Valid and ranked by scheduledCount despite the expected
  Top clarification. This is an ambiguity-handling defect. It prevents a claim
  that the evaluated DeepSeek path always clarifies an unstated ranking
  criterion. No proposed report was executed.
- V3D-015 correctly requested catalog clarification but returned lowercase
  department instead of the expected Department field identifier.

The two representation differences were not normalized into passes after
observing the results. No application fix or further provider run occurred.
The evaluation is complete as a measurement, with unresolved behavior findings.

## Usage, latency and estimated cost

Provider-reported usage totaled 66,543 input tokens and 2,766 output tokens.
Input usage included 63,104 cached tokens and 3,439 cache-miss tokens. Reported
reasoning tokens were zero. Usage fields were complete for all 17 responses.
Transport-response latency had nearest-rank p50 of 1,257.34 ms and p95 of
1,621.99 ms over 17 provider calls. These are not browser task times.

Using the conservative peak, uncached prices recorded in the protocol,
the total estimate is US$0.0232821. This deliberately ignores cache discounts
and off-peak rates. The sum reserved before requests was US$0.1645686, below
the US$1 experiment ceiling. No invoice or account balance was checked.
Prices were checked against
[the official pricing page](https://api-docs.deepseek.com/quick_start/pricing/)
on October 23, 2026.

## Sanitized supporting files and manuscript use

The [case CSV](../../evaluation/manuscript-2026-10-23/deepseek-case-comparison.csv)
contains all 18 questions, expected/actual classifications, exact-match flags,
assertion counts and differences. The
[observations JSON](../../evaluation/manuscript-2026-10-23/deepseek-comparison-observations.json)
retains the execution identity, usage and typed plans, with free-text detail
fields omitted. These selected files were inspected for sensitive material;
they contain synthetic questions and a fictional catalog only.

The [manuscript table and proposed text](../../evaluation/manuscript-2026-10-23/manuscript-deepseek-comparison.md)
distinguish model responses from final backend outcomes. This new execution
addresses the absence of a comparable live-provider development measurement.
It does not demonstrate that DeepSeek matched 18/18 questions or that earlier
respondent exposure constituted an accuracy experiment.

No held-out data, repeated-run stability, numerical report correctness,
SQL Server 2019 execution, authentication/browser workflow, GSD acceptance,
physical-device behavior, original SUS responses or interview records were
verified. No full test suite, web checks or mobile checks ran. Existing approved
evidence and source labels were left unchanged.
