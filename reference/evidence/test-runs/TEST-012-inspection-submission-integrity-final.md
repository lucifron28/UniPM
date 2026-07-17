---
id: TEST-012
type: test-run
title: Inspection submission integrity final verification
status: executed
recordedAtUtc: 2026-07-17T04:46:33Z
testedCommit: 6e8d4b8
sourceBranch: fix/inspection-submission-integrity
evidenceLevel: locally-executed
---

# Inspection Submission Integrity Final Verification

## Objective

Record the final Release verification after adding explicit unknown-schedule
coverage to the inspection submission endpoint.

## Execution Identity

- Tested commit: `6e8d4b8`
- Branch: `fix/inspection-submission-integrity`
- Execution date: 2026-07-17 UTC

## Commands

```powershell
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet test .\UniPM.slnx -c Release --no-build
```

## Results

- Release build: succeeded with 0 warnings and 0 errors.
- Full Release test suite: 269 passed, 0 failed, 20 skipped.
- The additional endpoint test confirms an authenticated request for an unknown
  schedule receives HTTP 404.

## SQL Server Verification

`UNIPM_SQLSERVER_TEST_CONNECTION` was not configured and the local Docker
daemon was unavailable. The SQL Server migration-preflight, unique-index, and
concurrent endpoint tests were skipped. This record does not claim their
execution or their persisted-state assertions.

## Limitations

TEST-011 remains the earlier local baseline for commit `6518a2d`. This record
supersedes it for final branch-head test counts only; neither record proves the
SQL Server concurrency behavior until a reachable SQL Server test connection is
configured.
