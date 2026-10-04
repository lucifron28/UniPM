---
id: TEST-059
type: test-run
title: PM analytics environment and evaluator provenance verification
status: executed
recordedAtUtc: 2026-10-04T08:00:48Z
testedCommit: 79ab459584688b60cc3698de5eaf31aa2dd74f90
sourceBranch: feature/nla-interpretation-evaluation
evidenceLevel: locally-executed
---

# PM analytics environment and evaluator provenance verification

## Objective

Record focused tests for the Development-only local-model selection boundary
and the evaluator's source-revision check. The run does not establish model
quality, production behavior, or verification of the later commits.

## Execution identity

The tests ran with repository HEAD
`79ab459584688b60cc3698de5eaf31aa2dd74f90` and the in-scope working changes
identified by fingerprint
`d675c1fc40941ff2873971fe4e29c2fd171766f90195d16cbd1e4a11e5d9cfb0`. The
fingerprint is SHA-256 over the sorted relative paths and per-file SHA-256
values for the nine changed source, test, project, and planning files, joined
with LF. The API and evaluator commits made after this run are not represented
as tested commits.

## Commands and results

The successful native Windows Release run used:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~NaturalLanguageAnalyticsInterpretationTests|FullyQualifiedName~PmAnalyticsEvaluationProvenanceTests' --logger 'trx;LogFileName=hardening-retry-79ab459.trx' --results-directory artifacts/evaluation/pm-analytics-interpretation
```

| Started UTC | Finished UTC | Exit | Passed | Failed | Skipped | Total |
|---|---|---:|---:|---:|---:|---:|
| 2026-10-04T07:54:23.977Z | 2026-10-04T07:54:41.824Z | 0 | 23 | 0 | 0 | 23 |

This run used the deterministic provider stub and disposable local Git
repositories. The environment tests confirmed the enabled provider is selected
in Development, while disabled Development and enabled Production/Staging use
the rule-based interpreter without sending a request; Production and Staging
also do not read the experimental setting. Provenance tests covered matching
and mismatching HEADs, unavailable Git/repository revisions, and early failure
before dataset, provider, or report work. The CLI mismatch check used a local
loopback listener as a request sentinel and observed no connection.

An initial attempt used the same filter at HEAD
`79ab459584688b60cc3698de5eaf31aa2dd74f90`, with working-change fingerprint
`c8ddbae8de66d92f48d44742ee075aed6972ff9d70d05914be77d9cfe7c492f1`. It ran
from 2026-10-04T07:52:33.283Z to 2026-10-04T07:53:04.683Z and exited 1 during
test-project compilation because the two xUnit `InlineData` arguments were
not compile-time constants. It used the same command and options above with
`--logger 'trx;LogFileName=hardening-79ab459.trx'`. No tests ran and no TRX was
produced. The fixture was corrected, and the single focused retry above
passed.

## Artifacts and limits

- Successful run log: `artifacts/evaluation/pm-analytics-interpretation/hardening-retry-79ab459.log`.
- Successful run TRX: `artifacts/evaluation/pm-analytics-interpretation/hardening-retry-79ab459.trx`.
- Compile-only attempt log: `artifacts/evaluation/pm-analytics-interpretation/hardening-79ab459.log`.

No live model, remote provider, or SQL Server test ran. The complete backend
suite, web/browser tests, and CI at the resulting commits were not run here.
The later commits require exact-head CI before they can be described as
verified. The Git check verifies checkout HEAD equality; it does not attest to
the evaluator binary's build provenance or a clean working tree.
