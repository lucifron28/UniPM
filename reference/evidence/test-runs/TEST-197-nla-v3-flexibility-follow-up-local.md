---
id: TEST-197
type: test-run
title: NLA V3 flexibility follow-up local paired verification
status: executed
recordedAtUtc: 2026-11-07T03:21:27Z
testedCommit: 6064fa17e78a88de82dff20748521ef414d101a2
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: locally-executed
---

# TEST-197: NLA V3 flexibility follow-up local paired verification

## Source identity and environment

- Frozen baseline: ef0feecee2b7f2092cd97086b8da2296c6f30e64, branch
  feature/nla-final-hardening.
- Candidate implementation: 74cedcbcd7f3a3a5bd3d9aca20c4fe15ea932daf;
  numeric-token test commit: d59b4a3c3d893d244b0d1ca7f40a5551ceb3397e;
  sentence-period correction and test: 6064fa17e78a88de82dff20748521ef414d101a2.
  Branch: feature/nla-flexibility-evidence-hardening.
- OS: Windows 11, build 10.0.26200.
- .NET SDK: 10.0.300; runtime 10.0.8.
- Provider: disabled for endpoint tests; fake handler only for guard tests.
- Database: endpoint acceptance tests use unique EF InMemory databases. No
  report preview/execution or native SQL calculation ran.

## Commands and results

The candidate's focused NLA regression command on the exact tested source/test
commit 6064fa17e78a88de82dff20748521ef414d101a2:

    dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PmAnalyticsV3FinalHardeningTests|FullyQualifiedName~PmAnalyticsV3GuardCorrectionTests|FullyQualifiedName~PmAnalyticsV3AcceptanceTests'

Result: 164 passed, 0 failed, 0 skipped; 16-second test duration. The exact
existing focused matrix reported 108 passed, 0 failed, 0 skipped on both ef0fee
and the candidate. The final paired 29-case regression expansion reported
ef0fee: 5 passed, 24 failed, 0 skipped; candidate: 29 passed, 0 failed, 0
skipped. Baseline and candidate expansion durations were 9 and 10 seconds.

The 29 expansion tests comprise four grouped/ungrouped thousand forms, nine
invalid threshold operands including mixed separators, one grouped
ranking-limit control, two fake-guard threshold cases, three on-time
paraphrases, four missed/late distinctions, two ambiguous repair controls,
two contextual non-operational paraphrases, one compound-outcome
clarification, and one sentence-final-period threshold control. The initial
attempt at the three mixed-separator additions failed 3/9 numeric cases; the
focused token-capture correction then passed the same nine-case target. The
sentence-period test first reproduced a dropped threshold on the
pre-correction candidate; removing only the end-punctuation rejection fixed
it. The corrected test passed alone, and the complete focused filter and
29-case expansion passed on the final source/test commit. The nine
invalid-threshold cases assert the complete safe plan as well as status and
clarification fields.

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
- artifacts/nla-flexibility-evidence/frozen-baseline-regression-expansion-v5.log
- artifacts/nla-flexibility-evidence/candidate-regression-expansion-v5.log
- artifacts/nla-flexibility-evidence/final-corrected-focused-suite-v5.log
- tests/UniPM.Api.Tests/TestResults/final-corrected-focused-suite-v5.trx

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
