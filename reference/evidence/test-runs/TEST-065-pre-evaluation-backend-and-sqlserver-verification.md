---
id: TEST-065
type: test-run
title: Pre-evaluation backend and SQL Server verification
status: executed
recordedAtUtc: 2026-10-10T04:59:56Z
testedCommit: 5d68d8e39a884a085dc99dd448ac9b9d865ebc03
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# Pre-evaluation backend and SQL Server verification

## Objective

Verify the schedule coverage and deferral changes against the full backend
suite and native SQL Server 2019, including migration rollback and reapply.

## Execution identity

The exact backend and test source commit was
5d68d8e39a884a085dc99dd448ac9b9d865ebc03 on
feat/pre-evaluation-ux-motion. The run began at 2026-10-10T03:52:39Z and ended
at 2026-10-10T03:58:50Z. Environment: Windows, .NET SDK 10.0.300, native SQL
Server 2019 version 15.0.2000.5, Full-Text Search installed, database
compatibility level 150.

The captured metadata reported a clean worktree before execution. No AI
provider was called.

## Commands

| Command | Result |
| --- | --- |
| dotnet restore ./UniPM.slnx | Passed. |
| dotnet build ./UniPM.slnx --configuration Release --no-restore | Passed. One existing CS8602 warning remains in PreventiveMaintenanceFormEndpoints.cs:540. |
| dotnet test ./UniPM.slnx --configuration Release --no-build | 384 passed, 0 failed, 1 optional provider test skipped. |
| dotnet test ./UniPM.slnx --configuration Release --no-build --filter FullyQualifiedName~SqlServer | 37 passed, 0 failed, 0 skipped. |
| dotnet ef migrations has-pending-model-changes --project server/server.csproj --startup-project server/server.csproj --configuration Release --no-build | Passed. No pending model changes. A process-only dummy connection setting allowed design-time model creation; this command did not connect to or modify a database. |

## SQL Server verification

The SQL Server-filtered run passed all 37 tests on the native SQL Server 2019
instance with Full-Text Search and compatibility level 150. The review-migration
round-trip test applied the migration, rolled it back, and reapplied it. It
verified unchanged IDs for the seeded Asset, schedule, PM form, inspection, and
acknowledgement, plus the deferral cycle and its persisted fields. The same test
confirmed review metadata columns disappeared on rollback and returned after
reapply. The remaining SQL tests covered reference Full-Text Search, coverage
and deferral behavior, concurrency, and uniqueness.

The backend capture summary reports 385 total tests: 384 passed and one
optional provider smoke test skipped. The separate SQL Server filter reports
37 of 37 passed. The optional provider smoke test is unrelated to PM workflow
and remained skipped.

## Generated artifacts

Raw output is retained, ignored by Git, under:

artifacts/evidence/20261010-035238Z-5d68d8e39a88/

The verification-summary.json records the tested SHA, environment metadata,
stage exit codes, and test counts. Raw logs are not committed.

## Limitations

This run verifies local backend and SQL Server behavior only. It does not prove
GSD acceptance, deployment behavior, or institutional effective-date approval.
The Release warning listed above remains outside this change.
