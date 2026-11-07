---
id: TEST-197
type: test-run
title: NLA V3 flexibility follow-up local paired verification
status: executed
recordedAtUtc: 2026-11-07T02:55:43Z
testedCommit: d59b4a3c3d893d244b0d1ca7f40a5551ceb3397e
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: locally-executed
---

# TEST-197: NLA V3 flexibility follow-up local paired verification

## Source identity and environment

- Frozen baseline: ef0feecee2b7f2092cd97086b8da2296c6f30e64, branch
  feature/nla-final-hardening.
- Candidate implementation: 74cedcbcd7f3a3a5bd3d9aca20c4fe15ea932daf;
  complete-plan test commit: d59b4a3c3d893d244b0d1ca7f40a5551ceb3397e.
  Branch: feature/nla-flexibility-evidence-hardening.
  feature/nla-flexibility-evidence-hardening.
- OS: Windows 11, build 10.0.26200.
- .NET SDK: 10.0.300; runtime 10.0.8.
- Provider: disabled for endpoint tests; fake handler only for guard tests.
- Database: endpoint acceptance tests use unique EF InMemory databases. No
  report preview/execution or native SQL calculation ran.

## Commands and results

The candidate's focused NLA regression command on the exact tested commit:

    dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PmAnalyticsV3FinalHardeningTests|FullyQualifiedName~PmAnalyticsV3GuardCorrectionTests|FullyQualifiedName~PmAnalyticsV3AcceptanceTests'

Result on the complete-plan test commit: 163 passed, 0 failed, 0 skipped;
22-second test duration. The exact existing focused matrix reported 108 passed,
0 failed, 0 skipped on both ef0fee and the candidate. The final paired
regression expansion reported ef0fee: 4 passed, 24 failed, 0 skipped; candidate:
28 passed, 0 failed, 0 skipped. Baseline and candidate expansion durations
were 8 and 11 seconds.

The 28 expansion tests comprise four grouped/ungrouped thousand forms, nine
invalid threshold operands including mixed separators, one grouped
ranking-limit control, two fake-guard threshold cases, three on-time
paraphrases, four missed/late distinctions, two ambiguous repair controls,
two contextual non-operational paraphrases, and one compound-outcome
clarification. The initial attempt at the three mixed-separator additions
failed 3/9 numeric cases; the focused token-capture correction then passed the
same nine-case target, and the complete 163-case filter and 28-case expansion
passed on the final source/test commit. The nine invalid-threshold cases assert
the complete safe plan as well as status and clarification fields.

The baseline command and candidate run logs/TRX files are retained as ignored
local artifacts:

- artifacts/nla-flexibility-evidence/baseline-dev-matrix-dotnet-test.log
- artifacts/nla-flexibility-evidence/follow-up-frozen-matrix-final.log
- artifacts/nla-flexibility-evidence/frozen-baseline-regression-expansion-v2.log
- artifacts/nla-flexibility-evidence/candidate-regression-expansion-v2.log
- artifacts/nla-flexibility-evidence/d25bb90-v3-focused-regression-suite.log
- artifacts/nla-flexibility-evidence/d25bb90-frozen-matrix.log
- artifacts/nla-flexibility-evidence/d25bb90-regression-expansion.log
- artifacts/nla-flexibility-evidence/frozen-baseline-regression-expansion-v3.log
- artifacts/nla-flexibility-evidence/frozen-baseline-regression-expansion-v4.log
- artifacts/nla-flexibility-evidence/malformed-numeric-full-token-final.log
- artifacts/nla-flexibility-evidence/malformed-numeric-full-token-corrected.log
- artifacts/nla-flexibility-evidence/74cedcb-v3-focused-regression-suite.log
- artifacts/nla-flexibility-evidence/74cedcb-regression-expansion.log
- artifacts/nla-flexibility-evidence/74cedcb-frozen-matrix.log
- artifacts/nla-flexibility-evidence/d59b4a3-complete-invalid-plan.log
- artifacts/nla-flexibility-evidence/d59b4a3-regression-expansion.log
- artifacts/nla-flexibility-evidence/d59b4a3-v3-focused-regression-suite.log
- tests/UniPM.Api.Tests/TestResults/d59b4a3-complete-invalid-plan.trx
- tests/UniPM.Api.Tests/TestResults/d59b4a3-regression-expansion.trx
- tests/UniPM.Api.Tests/TestResults/d59b4a3-v3-focused-regression-suite.trx

No raw provider request/response, GSD question, private data, or secret is
recorded. Per-query latency, CPU, memory, and provider costs were not measured.
The command durations above are test-harness duration only.

## Verification gaps

No SQL Server 2019 test was required or run because no calculation, SQL, or
schema changed. Local web E2E was not run because no web source changed, the
isolated worktree has no installed web/node_modules, and the available local
Node runtime is 24.x while the repository requires 22.x. Web CI is checked on
the pushed candidate branch and recorded separately. No live DeepSeek calls
were made; no provider-performance claim is supported.
