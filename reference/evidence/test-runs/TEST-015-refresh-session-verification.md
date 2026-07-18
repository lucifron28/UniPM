---
id: TEST-015
type: test-run
title: Refresh-session ordinary verification
status: executed
recordedAtUtc: 2026-07-18T01:10:00Z
testedCommit: fa75e162067e8dd4adbf9e53e28931a9080f6d01
sourceBranch: feat/auth-refresh-sessions
evidenceLevel: locally-executed
---

# Refresh-Session Ordinary Verification

## Commands

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet test .\UniPM.slnx -c Release --no-build
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AuthEndpointsTests"
git diff --check
```

The SQL Server test connection was supplied only through the local process
environment; its value is intentionally not recorded:

```powershell
$env:UNIPM_SQLSERVER_TEST_CONNECTION = "<local SQL Server test connection>"
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~SqlServerRefreshSessionTests"
Remove-Item Env:UNIPM_SQLSERVER_TEST_CONNECTION
```

## Results

- Restore: passed.
- Release build: passed with 0 warnings and 0 errors.
- Full ordinary Release suite: 280 passed, 0 failed, 24 skipped.
- Focused authentication suite: 30 passed, 0 failed, 0 skipped.
- Focused coverage includes login cookie attributes, hash-only persistence,
  refresh rotation/replay family revocation, idempotent logout, and exact-origin
  credentialed CORS, including rejection of malformed supplied Origin headers.

## SQL Server Verification

Executed against the local Docker SQL Server with a non-default ignored `.env`
password and a process-only test connection. The focused SQL Server refresh
suite passed 4 tests covering fresh and forward migration, token-hash
uniqueness, rowversion concurrency, simultaneous refresh, refresh versus
logout, and a forced rotation-write failure. The forced failure verified that
the failed transaction was released before fallback family revocation, which
returned the generic refresh rejection and left no active refresh token.

## Limitations

This record is ordinary local verification only. It does not claim production
authentication readiness, penetration testing, complete CSRF elimination, IIS
deployment verification, final RBAC approval, or mobile secure-storage
validation.
