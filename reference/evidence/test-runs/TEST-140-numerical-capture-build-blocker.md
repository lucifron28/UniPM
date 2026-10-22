---
id: TEST-140
type: test-run
title: Controlled numerical capture blocked by running API build lock
status: executed
recordedAtUtc: 2026-10-23
testedCommit: 89cdc38820e828bf66bab1fa5822236a73f1548c
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: locally-executed
---

# Controlled numerical capture build blocker

Ten controlled requests were prepared for seven existing synthetic fixtures:
counts, progress, compliance, matching denominators, grouped thresholds,
category cycles, Q1/Q4 source selection, pagination, missing coverage and
zero eligible schedules. A temporary capture tool reuses the existing private
test-fixture execution helper to capture actual backend query-service
responses. It uses disposable EF InMemory stores, not a live SQL database.
No application source or existing test code was modified by this operation.
Preexisting Web/mobile changes were left untouched.

The source commit above and SHA-256 hashes of the helper source, backend query
service, fixture file, prepared requests and capture program were saved in
ignored `artifacts/manuscript-numerical-2026-10-23/baseline.json` before execution.

## Attempted command

```powershell
dotnet run --project artifacts/manuscript-numerical-2026-10-23/runner/NumericalCapture.csproj -- reference/evaluation/pm-analytics-v3/synthetic-calculation-fixtures.json artifacts/manuscript-numerical-2026-10-23/requests.json artifacts/manuscript-numerical-2026-10-23/backend-responses.json
```

The command exited 1 during project-reference compilation. MSBuild reported
MSB3027/MSB3021 because the running local UniPM.Api process held the target
API executable open. MSBuild performed its ten automatic file-copy retries;
the agent did not start a second run or stop the API process.

Raw output is retained in ignored
`artifacts/manuscript-numerical-2026-10-23/backend-capture.log`.

## Outcome

Zero numerical cases executed. No captured backend-response file or numerical
pass/fail table was produced. Prepared requests and fixture expectations are
not execution evidence. No provider calls, live database connections, data
writes, migrations, reseeding or deployments occurred.

The proposed continuation is one isolated capture-tool build using existing
project-reference binaries with `-p:BuildProjectReferences=false`, after
explicit authorization to continue following this environment failure. This
continuation was not executed in this record.
