---
id: TEST-010
type: test-run
title: Authentication inspection identity-binding verification
status: executed
recordedAtUtc: 2026-07-13T01:23:51Z
testedCommit: 37981a4203e69db249e2eea00729b9e0d45b97bc
sourceBranch: feat/auth-scaffolding
evidenceLevel: locally-executed
supersedes: TEST-009
---

# Authentication Inspection Identity-Binding Verification

## Commands And Results

```powershell
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-AuthVerification.ps1
```

- Release build: passed with zero warnings and zero errors.
- Full Release suite: 254 passed, 0 failed, and 17 skipped.
- The fresh-volume authentication harness completed with exit code `0` and
  recorded `worktreeClean: true`.

## Corrected Inspection Authorization Behavior

- An Inspector can submit an inspection only when `InspectorUserId` matches
  the authenticated JWT subject.
- An Inspector attempting to submit another user's ID receives HTTP 403.
- A nonexistent or inactive referenced inspection user is rejected before any
  inspection or schedule write.
- GSD retains the provisional on-behalf submission path only for an active
  referenced user. A separate submitter audit field remains deferred pending
  the planned backend-hardening contract work.

These cases are covered by real-JWT endpoint tests in the executed Release
suite; the Docker harness confirms the seeded Inspector submission succeeds
against a fresh SQL Server database.

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
artifacts/evidence/20260713-012307Z-37981a4203e6-authentication
```

```text
d39d4d37dd634c70754f123c2e32ba0d8041ae63f59e60925f22797e67939209  auth-verification.json
692c17db81adf0bd0bc3cff406124dc6091599e65ffba5ef3d06171df36da93e  verification-summary.json
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
