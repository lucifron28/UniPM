---
id: TEST-024
type: test-run
title: Preventive-maintenance form domain verification
status: executed
recordedAtUtc: 2026-07-27T10:37:10Z
testedCommit: 5152c04
sourceBranch: refactor/preventive-maintenance-form-domain
evidenceLevel: locally-executed
---

# Preventive-Maintenance Form Domain Verification

## Scope

Verified the backend preventive-maintenance form domain foundation: one form
with multiple inspection rows, supported form statuses, filtered unique
non-null file numbers, one acknowledgement per form, the academic-year format
constraint, and migration preservation of existing inspections with a null
form link. The existing one-inspection-per-schedule constraint remains
unchanged.

No form commands, acknowledgement endpoints, file-number generation, schedule
completion changes, handoff/RMRF processing, OEM retrieval, frontend work, or
signature indexing was evaluated.

## Commands

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore

$connection = "Server=.;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;"
$env:UNIPM_SQLSERVER_TEST_CONNECTION = $connection
$env:UNIPM_SQLSERVER2019_TEST_CONNECTION = $connection
dotnet test .\UniPM.slnx -c Release --no-build
```

The SQL connection was supplied through the process environment only. No
connection string containing credentials was committed.

## Results

- Release build: passed with 0 warnings and 0 errors.
- SQL-enabled solution suite: 341 passed, 0 failed, 1 skipped.
- The only skipped test was the optional real-provider embedding smoke test.
- The new form constraint and migration-preservation tests passed against the
  native SQL Server 2019 instance.

## Limitations

This is local development compatibility evidence only. It does not validate
production IIS deployment, final workflow command behavior, RMRF/WMS
processing, source authority, or real institutional data.
