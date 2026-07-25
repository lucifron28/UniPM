---
id: TEST-023
type: test-run
title: Reference-document foundation verification
status: executed
recordedAtUtc: 2026-07-25T05:45:03Z
testedCommit: 14ec87d4e585e137db1c25b672941d134a131de6
sourceBranch: feat/reference-document-foundation
evidenceLevel: locally-executed
---

# Reference-Document Foundation Verification

## Scope

Verified the separate fictional reference-document persistence foundation,
including ordered sections, section embedding constraints, Development-only
seed/reset commands, and a separate SQL Server Full-Text catalog. No real
institutional/OEM source, provider call, retrieval endpoint, synthesis, or
production deployment claim is included.

## Commands

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet test .\UniPM.slnx -c Release --no-build

$env:UNIPM_SQLSERVER2019_TEST_CONNECTION =
"<native SQL Server 2019 master/test connection>"
$env:UNIPM_SQLSERVER_TEST_CONNECTION =
"<native SQL Server 2019 master/test connection>"
dotnet test .\UniPM.slnx -c Release --no-build
```

## Results

- Restore and Release build: passed with zero warnings.
- Ordinary suite: 289 passed, 27 skipped, 0 failed. SQL-gated tests and the
  optional real-provider smoke test were intentionally skipped without their
  process environment configuration.
- SQL-enabled suite: 315 passed, 1 skipped, 0 failed.
- The only SQL-enabled skip was the optional real-provider embedding smoke
  test; no provider configuration was supplied.
- Native SQL Server 2019 was selected through process-only connection settings.
  The dedicated major-version-15 gate and reference Full-Text migration test
  executed successfully.

## Limitations

This is development compatibility evidence only. It does not validate source
authority, production IIS deployment, real embedding quality, extraction,
combined evidence retrieval, or generated review content.
