---
id: TEST-135
type: test-run
title: NLA MVP Phase 1 and Phase 2 focused acceptance and UI verification
status: executed
recordedAtUtc: 2026-10-22T14:30:21Z
testedCommit: "backend b70342965edd5209486626966cea66aefb4527ba; web 87f7722852fe0079bf5fa254969746a63a0ff6fb"
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: locally-executed
---

# NLA MVP Phase 1 and Phase 2 focused acceptance and UI verification

This record covers a focused backend acceptance run at commit
`b70342965edd5209486626966cea66aefb4527ba` and a focused web test rerun of the
working patch later committed as `87f7722852fe0079bf5fa254969746a63a0ff6fb`.
It does not establish completion of all 14 acceptance scenarios.

## Executed checks

- Backend command: `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter FullyQualifiedName~PmAnalyticsV3AcceptanceTests --logger 'console;verbosity=minimal' 2>&1 | Tee-Object artifacts/nla-audit-fix/backend-acceptance.log`
- Result: 9 passed, 0 failed, 0 skipped.
- Backend source commit: `b70342965edd5209486626966cea66aefb4527ba`.
- Raw output: [`artifacts/nla-audit-fix/backend-acceptance.log`](../../../artifacts/nla-audit-fix/backend-acceptance.log) (ignored local artifact).
- No live provider request was made for this test run, per the run operator. The test log contains aggregate test counters only.

The focused web test ran from `web/` against UI changes based on
`b70342965edd5209486626966cea66aefb4527ba`. The first run used an intermediate
uncommitted UI patch whose exact patch identity was not recorded. The first-run
command was:

```powershell
npm exec -- vitest run src/features/reports/pm-analytics-v3.test.tsx 2>&1 | Tee-Object ../artifacts/nla-audit-fix/web-focused.log
```

The first run passed 17 tests and failed 1 because the clarification warning
was not visible in the preview. The correction retained the warning in the
preview and was committed as
`87f7722852fe0079bf5fa254969746a63a0ff6fb`. The rerun command was:

```powershell
npm exec -- vitest run src/features/reports/pm-analytics-v3.test.tsx 2>&1 | Tee-Object ../artifacts/nla-audit-fix/web-focused-rerun.log
```

The rerun tested the working patch later committed as
`87f7722852fe0079bf5fa254969746a63a0ff6fb` and passed 18/18 tests in 6.63
seconds (exit code 0). The first-run output is in
[`artifacts/nla-audit-fix/web-focused.log`](../../../artifacts/nla-audit-fix/web-focused.log);
the passing rerun is in
[`artifacts/nla-audit-fix/web-focused-rerun.log`](../../../artifacts/nla-audit-fix/web-focused-rerun.log).

## Reported results without retained output

The earlier draft also listed web typecheck, 8 endpoint tests, 17 V3 regression
tests, and 2 native SQL Server tests as passing. No raw output for those runs
was found for this record, and their exact tested source state cannot be
established here. They remain reported results, not corroborated TEST-135
execution evidence. The native SQL test source uses isolated test databases;
this record does not claim that a native SQL run occurred.

## Demonstration data and remaining limits

The earlier draft's `UniPMDb` counts and record identifiers are unverified and
are withdrawn as confirmed ground truth. The configured connection target did
not pass the loopback-only check, so no database connection or query was made.
No migration, seed, write, or provisioning operation was attempted. The
previous acknowledged-versus-provisional comparison also does not establish
the V3 non-operational count: V3 counts completed non-operational inspection
rows regardless of form status, including completed Draft and Submitted forms.

Two live DeepSeek requests are documented separately in TEST-134; they are not
part of this backend acceptance run. Azure deployment and physical mobile
verification were not run for TEST-135.
