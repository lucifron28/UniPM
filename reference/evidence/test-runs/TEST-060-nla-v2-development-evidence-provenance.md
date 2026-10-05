---
id: TEST-060
type: test-run
title: NLA v2 development exact-source verification and provenance limitation
status: executed
recordedAtUtc: 2026-10-05T01:17:38Z
testedCommit: eabaebc3993b15ae928753d5dab999082f43f019
sourceBranch: experiment/nla-provider-comparison-v2
evidenceLevel: locally-executed
---

# NLA v2 development exact-source verification and provenance limitation

## Objective

Verify the committed development implementation separately from the historical
provider runs, and document the failed source-to-binary equivalence check.
No model evaluation, provider request, corpus tuning, or held-out evaluation
was authorized or performed during this verification.

## Execution identity

The verification checkout was a clean, detached worktree at
`eabaebc3993b15ae928753d5dab999082f43f019`. Its relative path was
`artifacts/verification/nla-eaba-source`. The working evidence drafts in the
primary checkout were not build inputs. Git HEAD and clean tracked state were
checked before and after the fresh evaluator build.

The recorded experiment remains [EXP-007](../experiments/EXP-007-nla-v2-development-provider-matrix.md).
These later checks do not change the earlier measurements or their provenance.
The user explicitly authorized continuing verification and publication after
the mismatch, with `binaryEquivalenceEstablished=false` and no stronger claim
about the historical run binaries.

## Provenance metadata

```yaml
experimentalSourceCommit: eabaebc3993b15ae928753d5dab999082f43f019
devDatasetSha256: f98d505d6c2ac2f1b3372109e50b949fd2211b7b36af3104536451d4406f2533
evaluatorDllSha256Used: 3AC406E7E01BAAE8923CA887B47713B85194E09E0C22998AE1C0845B5F76EC31
apiDllSha256Used: 9EEC0DE801D545CF60E834FAD864336003D44B318A718E424E5DC9C306EFB26C
rebuiltEvaluatorDllSha256: 3CA88354E7F4309B4C6F6B39AF833A2108126F2297D4EE810CCDDE7FF9C98590
rebuiltApiDllSha256: 4BB2EA1F92BCCA0B9C315A4E2810016EA7877E9EFE9932F496595AC553DC1D33
binaryEquivalenceEstablished: false
exactSourceFocusedTests: verified
exactSourceFullBackend: verified
heldoutEvaluated: false
humanLanguageReview: pending
```

## Release rebuild and binary comparison

Command, from the detached checkout:

```powershell
dotnet build tools/UniPM.PmAnalytics.InterpretationEval/UniPM.PmAnalytics.InterpretationEval.csproj --configuration Release
```

The fresh build ran from `2026-10-05T00:34:38.9050737Z` to
`2026-10-05T00:34:57.0696605Z`, using .NET SDK `10.0.300`. It exited 0 with
0 errors and 5 warnings. Four NU1900 warnings reported unavailable NuGet
vulnerability-feed data; one CS8602 nullable warning identified
`server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs:442`.
No source fix was made for those warnings.

The evaluator and its copied API assembly were hashed from
`tools/UniPM.PmAnalytics.InterpretationEval/bin/Release/net10.0/` between
`2026-10-05T00:35:25.1342052Z` and `2026-10-05T00:35:25.6070912Z`.
Both rebuilt hashes differed from the preserved provider-run hashes above.
The initial verification stopped at that gate. The later user approval permits
verification and publication with the limitation retained.

Read-only diagnosis found different embedded informational/source versions,
module IDs, and PDB hashes. The preserved assemblies contain informational
version `1.0.0+f9a2ba02108ce7f27da64d5fc97e188a6db14267`; the rebuilt assemblies
contain `1.0.0+eabaebc3993b15ae928753d5dab999082f43f019`. The generated SourceLink
files also refer to those different commits and checkout roots. The generated
files support the metadata observation; they do not attest to source contents.

Assembly versions and type/field/method row counts match pairwise. A first
metadata-row comparison used unsuitable handle text and was discarded; it is
not evidence of member differences or equality. Exact member, method-body,
exception-region, and executable equivalence were NOT established. No claim
is made that the differences are limited to metadata, or that the historical
run binaries were proven to come from the exact committed source.

## Exact-source verification

The focused command below was attempted from the clean exact-source checkout.
The results path is recorded relative to the primary repository; its resolved
directory was outside the detached checkout.

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~NaturalLanguageAnalytics|FullyQualifiedName~PmAnalytics|FullyQualifiedName~GitSourceSha" --logger "trx;LogFileName=focused.trx" --results-directory artifacts/verification/nla-eaba-tests-20261005T005210Z/trx-focused
```

The recorded start was `2026-10-05T00:52:10.5137404Z`. Initially no exit
code or end time was recoverable: the session remained open, while an
escalated process snapshot found MSBuild node workers but no matching test
parent process. An open session was not treated as proof of a live test
command. A later metadata observation recovered exit code `1` and end time
`2026-10-05T01:07:13.8384254Z`. The log still contained only the initial restore
banner, and the results directory contained zero TRX files. This command
failed without a recoverable diagnostic cause or test counts; it is not a
passing or failing test-case result. The verification chain stopped, and the
user then explicitly authorized cleanup of confirmed owned processes and one
focused retry with reduced MSBuild parallelism. No source correction was
authorized or performed.

SDK `10.0.300` and installed runtimes `10.0.6`/`10.0.8` were recorded. The four
embedding-provider opt-in variables were cleared without reading their values;
both optional SQL connection variables were absent. An earlier wrapper parse
error occurred before dotnet was invoked and produced no test result. Only
the wrapper syntax was corrected. No source, test, model, corpus, prompt, or
retry policy was changed.

Publication stopped after the initial failure. The user explicitly approved
one focused retry with reduced parallelism and continuation only if it passed.
Before the retry, the old invocation returned its final exit code and its
confirmed owned workers were absent. No process was killed. The retry,
subsequent full backend suite, and standalone solution build all exited 0 at
the same clean source revision. The retry used one MSBuild worker, disabled
build servers and shared compilation, and separate stdout/stderr capture.
The cause of the initial failed restore was not established.

Results-directory arguments below are normalized relative to the primary
repository; actual process arguments used the corresponding absolute paths
outside the detached checkout. All commands ran from the detached checkout.

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --disable-build-servers -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NaturalLanguageAnalytics|FullyQualifiedName~PmAnalytics|FullyQualifiedName~GitSourceSha" --logger "trx;LogFileName=focused.trx" --results-directory artifacts/verification/nla-eaba-retry-final/trx-focused
dotnet test UniPM.slnx --configuration Release --no-build --disable-build-servers -m:1 -p:UseSharedCompilation=false --logger "trx;LogFileName=full.trx" --results-directory artifacts/verification/nla-eaba-retry-final/trx-full
dotnet build UniPM.slnx --configuration Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

| Check | Started UTC | Finished UTC | Exit | Passed | Failed | Skipped |
|---|---|---|---:|---:|---:|---:|
| Focused retry | 2026-10-05T01:14:04.8435876Z | 2026-10-05T01:14:21.4164872Z | 0 | 161 | 0 | 1 |
| Full backend | 2026-10-05T01:14:58.7903439Z | 2026-10-05T01:15:10.1742531Z | 0 | 394 | 0 | 28 |
| Standalone solution Release | 2026-10-05T01:15:40.5954459Z | 2026-10-05T01:15:42.2230607Z | 0 | N/A | N/A | N/A |

TRX counters and individual outcomes were inspected. The focused skip was the
optional SQL Server analytics fact. The full suite skipped 27 optional SQL
facts and one live embedding-provider smoke fact: 10 reference-document,
8 domain-contract, 4 refresh-session, 4 inspection-integrity, 1 analytics,
and 1 embedding-provider fact. These skips are not SQL or real-provider
verification. Native SQL tests were not separately enabled for this goal.
No Gemini/DeepSeek evaluator or held-out run was launched.

The standalone solution build reported 2 NU1900 vulnerability-feed warnings
and 0 errors. The earlier fresh evaluator build's CS8602 warning remains
recorded above; no source changes were made to suppress warnings.
`git diff --check` passed and `git status --porcelain` was empty in the detached
checkout after all checks. These results establish exact-source checks at
`eabaebc3993b15ae928753d5dab999082f43f019`, not binary equivalence with the
historical provider runs. Publication-head CI is a separate later execution;
its result must be verified on the pushed SHA, not inferred from this record.

## Artifact audit

The read-only audit matched all five EXP-007 development reports and command
logs on source and dataset digests, model/prompt identifiers, timings, scores,
retries, failures, latency, tokens, and the two V4 Pro safety-case IDs. All report
split fields are `dev`; no held-out result appeared in the run-artifact folder.
Only development corpus metadata was inspected. The 30 expected-Unsupported
cases include all 15 adversarial-tagged cases.

Independent decimal arithmetic using the cited peak profiles gives:

- DeepSeek Flash: `(33526 * 0.006 + 8473 * 0.30 + 1284 * 1.20) / 1000000 = 0.00428386 USD`.
- DeepSeek V4 Pro: `(36864 * 0.044 + 5024 * 1.32 + 1947 * 3.96) / 1000000 = 0.01596382 USD`.

These are conservative estimates, not invoices. Gemini costs remain
unavailable because usage is incomplete. No missing usage was inferred.
EXP-006's byte hash remains
`3657FE4AE3771D7E3916CE56C57D9ED2B0C98FC7D6C1E1A28E35A0D775275BC0`.
EXP-004/Qwen v1, EXP-005 literature research, and the formal testing baseline
tag remain separate and unchanged.

## Artifacts and limits

- Original development reports and command logs: `artifacts/evaluation/pm-analytics-interpretation/v2-development-eabaebc/`.
- Fresh rebuild log, UTC metadata, and hash comparison: `artifacts/verification/nla-eaba-source-logs-20261005T003438Z/`.
- Initial failed focused-attempt log and metadata: `artifacts/verification/nla-eaba-tests-20261005T005210Z/`.
- Successful retry, full-suite TRX/logs, and standalone Release metadata: `artifacts/verification/nla-eaba-retry-final/`.

No new provider experiment or held-out evaluation occurred. Independent
Filipino/Taglish human review, final corpus freeze, finalist selection, final
held-out evaluation, and production-provider choice remain pending. Pipeline
scores are not standalone model accuracy or institutional language-quality
verification. Exact-head publication CI is a separate later execution.
