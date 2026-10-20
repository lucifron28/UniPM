---
id: TEST-093
type: test-run
title: Windows PowerShell staging connection builder compatibility
status: executed
recordedAtUtc: 2026-10-18T02:55:13Z
testedCommit: 49fe7736d0134adae5d3e5580225ed9b08af6232
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
guideWorkingPatchSha256: 76B6923F4EDE0F00AF264878C1D42D6D2F42BD0B23E1758E990510157A1D99E7
---

# Windows PowerShell staging connection builder compatibility

## Execution identity

The check used base commit `49fe7736d0134adae5d3e5580225ed9b08af6232` on
`deploy/azure-staging` plus the guide working patch whose SHA-256 is recorded
above. It ran locally in Windows PowerShell 5.1. The reported
`$connection.DataSource` keyword error happened before initializer or `sqlcmd`
execution, so no database writes occurred.

## Command and result

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File artifacts/azure-staging-connection-builder-winps5-20261018/run-builder-check.ps1
```

Exit code 0. The runner extracted and executed the builder statements from the
documented block, substituting a synthetic server, database, and login. It
generated a synthetic password at runtime with a semicolon and quote, kept it
out of output, and checked serialization and reparsing of every connection
setting. The `.ConnectionString` getter, postflight indexer references, and
`Clear()` cleanup passed. Output: `artifacts/azure-staging-connection-builder-winps5-20261018/windows-powershell-5.1.log`.

No SQL connection, Azure access, migration, initializer, or seed command ran.
No application code or tracked test files changed. The runner and raw output
remain under ignored `artifacts/`.
