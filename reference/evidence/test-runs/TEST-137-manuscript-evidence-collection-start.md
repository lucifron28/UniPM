---
id: TEST-137
type: test-run
title: Initial manuscript evidence collection and NLA regression check
status: reviewed
reviewedAt: 2026-10-23
reviewedBy: Researcher / repository owner, approval in this chat
reviewScope: Evidence record and stated limitations; no additional execution asserted
recordedAtUtc: 2026-10-23
testedCommit: 79852bf075ca082781365bdeb5981e8a2d0422a2
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: locally-executed
---

# Initial manuscript evidence collection

## Version and boundary

The run used commit `79852bf075ca082781365bdeb5981e8a2d0422a2`
plus existing uncommitted Web and mobile changes. The working tree was not
clean and is not presented as a frozen release. Initial UTC time and hashes of
those files, the V3 candidate corpus, review ledger and calculation fixtures
are retained in ignored `artifacts/manuscript-verification-2026-10-23/baseline.json`.
No application source or test code was edited during this collection.

## Executed regression check

Command from the repository root:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter FullyQualifiedName~PmAnalyticsV3RegressionTests --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=nla-regression.trx' --results-directory artifacts/manuscript-verification-2026-10-23
```

The actual command used the absolute results-directory path and piped standard
output and errors through `Tee-Object` to `nla-regression.log` in that directory.

Result: exit 0; 17 passed, 0 failed, 0 skipped. Test-reported duration was one
second. The .NET 10 project and references compiled as part of this command;
no separate build or full test suite was run.

The class includes deterministic parser checks, synthetic calculation
assertions, grouping, thresholds, category cycles, source membership,
pagination, missing coverage, empty populations and signed-preview checks.
Calculation-path tests use disposable EF InMemory stores. These passes are
automated engineering evidence, not a 17-question interpretation accuracy
score, native SQL Server verification, or BB-01 through BB-32 execution.

Retained ignored artifacts under `artifacts/manuscript-verification-2026-10-23/`:

- `nla-regression.log`
- `nla-regression.trx`
- `nla-regression-results.csv`, extracted per-test names, outcomes and durations

## Corpus diagnostic blocked before interpretation

```powershell
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll diagnose
```

Result: exit 1, `Manifest fileSha256 does not match the current candidate file.`
The output is retained in `nla-development-diagnostics.json`, which contains
an error message rather than a diagnostic JSON result. No case interpretations
or accuracy scores were produced. No retry was attempted.

Read-only SHA-256 inspection established:

- Current bytes: `de687fa7925e4b40472bba93f4fc6456e651603d9fd6b03e479191b58d05b6c2`.
- Bytes with CRLF converted to LF in memory:
  `b7362e13106b8872353b934ceedd33c70e356334c67343a78e55f316453348e6`.
- Manifest expected hash:
  `b7362e13106b8872353b934ceedd33c70e356334c67343a78e55f316453348e6`.
- The current candidate file contains 18 CRLF line endings.

The discrepancy is attributable to line endings. No file bytes, manifest,
expected label or review decision were changed to bypass the gate. All 18
expected labels remain pending human review.

## Pending manuscript evidence

The supplied PDF reports 32 black-box passes, including seven NLA cases.
This run does not corroborate those individual historical executions. The new
collection register keeps all 32 cases unexecuted until actual evidence is
associated with each. The 18 development questions are a separate set and must
not be added to those 32 as if they were the same test population.

SUS, ISO-informed evaluation and historical BB records await user delivery.
No physical-device, browser, real-provider or native SQL Server check was run.
No live database connection, write, migration, reset, seed or deployment was
performed.
