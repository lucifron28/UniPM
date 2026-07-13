---
id: TEST-009
type: test-run
title: Authentication and role-policy baseline
status: superseded
recordedAtUtc: 2026-07-13T00:48:03Z
testedCommit: 205c1ac3a55a2f35c36dc100dab2bc42a5a14327
sourceBranch: feat/auth-scaffolding
evidenceLevel: locally-executed
supersededBy: TEST-010
---

# Authentication And Role-Policy Baseline

## Commands And Results

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-AuthVerification.ps1
```

- Restore: passed.
- Release build: passed with zero warnings and zero errors.
- Full Release suite: 249 passed, 0 failed, and 17 skipped.
- The fresh-volume authentication harness completed with exit code `0` and
  recorded `worktreeClean: true`.

## Fresh SQL Server And Authentication Verification

- SQL Server 2025 Developer Edition with Full-Text Search started on a new
  Compose volume, and all EF Core migrations applied successfully.
- The Development-only synthetic fixture, five development users, and their
  exact role assignments were seeded before endpoint verification.
- Maintenance search documents were rebuilt before exercising the source-
  returning review path.
- Login succeeded for Admin, GSD, Inspector, Supervisor, and DepartmentHead;
  `/api/v1/auth/me` returned the authenticated GSD role.
- Anonymous asset creation returned HTTP 401, and an authenticated Admin
  without operational authority returned HTTP 403.
- GSD asset creation, Supervisor schedule creation, and Inspector inspection
  submission returned HTTP 201.
- DepartmentHead maintenance review returned HTTP 200 with one fictional
  source record. Summary generation was not requested.

## Artifacts And Hashes

The ignored artifact capture is retained under:

```text
artifacts/evidence/20260713-004730Z-205c1ac3a55a-authentication
```

```text
468e1b0d842490824b9a8d29e2f072dfffbe63636b87dae259295a8b99b9a21a  auth-verification.json
d97d75d38aa1eb45c28d42fbffba1e2ea36b3f285efe0c0a5d741304fe8241c8  verification-summary.json
```

The reviewed artifacts contain bounded role, status, expiry, short token-
fingerprint, and fictional-source metadata only. They contain no passwords,
JWT bodies, authorization headers, signing keys, connection strings, prompts,
token maps, embedding vectors, or provider payloads.

## Limitations

This is local SQL Server and Docker Compose evidence using ephemeral secrets and
fictional Development data. It does not establish IIS deployment, institutional
secret management, final GSD role authority, registration, password recovery,
refresh-token behavior, external identity-provider integration, production
account lifecycle, or production security assessment.
