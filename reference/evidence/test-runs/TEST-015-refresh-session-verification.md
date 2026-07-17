---
id: TEST-015
type: test-run
title: Refresh-session ordinary verification
status: executed
recordedAtUtc: 2026-07-17T15:45:19Z
testedCommit: b4db9cfae3ffed1f2235c467285a15df2fc3f699
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

## Results

- Restore: passed.
- Release build: passed with 0 warnings and 0 errors.
- Full ordinary Release suite: 280 passed, 0 failed, 23 skipped.
- Focused authentication suite: 30 passed, 0 failed, 0 skipped.
- Focused coverage includes login cookie attributes, hash-only persistence,
  refresh rotation/replay family revocation, idempotent logout, and exact-origin
  credentialed CORS, including rejection of malformed supplied Origin headers.

## SQL Server Verification

Executed against the local Docker SQL Server with a non-default ignored `.env`
password and a process-only test connection. The focused SQL Server refresh
suite passed 3 tests covering fresh and forward migration, token-hash
uniqueness, rowversion concurrency, simultaneous refresh, and refresh versus
logout. These races returned no HTTP 500 and left no more than the allowed
active session state.

## Limitations

This record is ordinary local verification only. It does not claim production
authentication readiness, penetration testing, complete CSRF elimination, IIS
deployment verification, final RBAC approval, or mobile secure-storage
validation.
