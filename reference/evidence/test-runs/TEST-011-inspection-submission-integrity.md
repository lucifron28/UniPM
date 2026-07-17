---
id: TEST-011
type: test-run
title: Inspection submission integrity baseline
status: executed
recordedAtUtc: 2026-07-17T04:46:33Z
testedCommit: 6518a2d
sourceBranch: fix/inspection-submission-integrity
evidenceLevel: locally-executed
---

# Inspection Submission Integrity Baseline

## Objective

Verify the committed inspection-submission integrity implementation without
claiming SQL Server execution when its required local connection is absent.

## Execution Identity

- Tested commit: `6518a2d`
- Branch: `fix/inspection-submission-integrity`
- Execution date: 2026-07-17 UTC

## Environment

The local .NET 10 Release build environment had no
`UNIPM_SQLSERVER_TEST_CONNECTION`. Docker Compose could not reach a Docker
daemon, so SQL Server-backed tests remained intentionally skipped.

## Commands

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet test .\UniPM.slnx -c Release --no-build
```

## Results

- Restore: succeeded; all projects were up to date.
- Release build: succeeded with 0 warnings and 0 errors.
- Full Release test suite: succeeded.

## Test Counts

- Passed: 268
- Failed: 0
- Skipped: 20

The non-SQL endpoint tests covered successful submission, schedule completion,
search-document creation, and the friendly sequential duplicate conflict.

## SQL Server Verification

The three `SqlServerInspectionSubmissionIntegrityTests` were skipped because
`UNIPM_SQLSERVER_TEST_CONNECTION` was not configured. The local Docker daemon
was unavailable, so no temporary SQL Server database could be created.

The generated migration script was inspected and contained only the duplicate
preflight, the index replacement with a unique index, and the EF migration
history insert. This inspection is not SQL Server execution evidence.

## AI-Provider Verification

Not applicable. This change does not call an embedding or summary provider.

## Generated Artifacts

No reviewed artifacts were committed. A temporary local migration script was
generated for inspection and was not retained as evidence.

## Skipped Verification

The migration-preflight rejection, physical SQL Server unique-index enforcement,
and concurrent endpoint persisted-state assertions require a reachable SQL
Server and remain unexecuted in this record.

## Limitations

This test run does not claim that the concurrent SQL Server test produced one
HTTP 201, one HTTP 409, one inspection, one search document, and one completed
schedule. The committed test asserts that outcome when SQL Server is configured.
