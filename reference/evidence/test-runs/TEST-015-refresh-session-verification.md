---
id: TEST-015
type: test-run
title: Refresh-session ordinary verification
status: executed
recordedAtUtc: 2026-07-17T15:45:19Z
testedCommit: 29b6e43476dac2f0ca014aa556f15c602187f19f
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
- Full ordinary Release suite: 273 passed, 0 failed, 20 skipped.
- Focused authentication suite: 23 passed, 0 failed, 0 skipped.
- Focused coverage includes login cookie attributes, hash-only persistence,
  refresh rotation/replay family revocation, idempotent logout, and exact-origin
  credentialed CORS, including rejection of malformed supplied Origin headers.

## SQL Server Verification

Not executed. The local Docker SQL Server container was healthy, but the local
`.env` retained the example default SA password. No credentials were changed
and no SQL connection string was recorded. Fresh/forward migration, SQL Server
token-hash uniqueness, rowversion concurrency, and transactional rotation remain
pending a securely configured local SQL Server test connection.

## Limitations

This record is ordinary local verification only. It does not claim production
authentication readiness, penetration testing, complete CSRF elimination, IIS
deployment verification, final RBAC approval, or mobile secure-storage
validation.
