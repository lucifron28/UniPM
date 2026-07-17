---
id: IMP-010
type: implementation
title: Database-enforced inspection submission integrity
status: reviewed
recordedAtUtc: 2026-07-17T04:46:33Z
testedCommit: 6518a2d
sourceBranch: fix/inspection-submission-integrity
evidenceLevel: source-inspected
---

# Database-Enforced Inspection Submission Integrity

## Objective

Enforce the invariant that one preventive-maintenance schedule has at most one
inspection record, including when two submissions race after the endpoint's
friendly duplicate pre-check.

## Source Identity

- Relevant commits: `dd6a853`, `6518a2d`, `6e8d4b8`
- Source paths: `server/Data/ApplicationDbContext.cs`,
  `server/Features/Inspections/InspectionsEndpoints.cs`, and migration
  `20260717044115_EnforceOneInspectionPerSchedule`

## Implementation Summary

`InspectionRecord.ScheduleId` has a SQL Server unique index named
`IX_InspectionRecords_ScheduleId`. The existing application-level `AnyAsync`
check remains so ordinary repeated submissions receive a clear conflict before a
write is attempted. The database index is the concurrency authority.

The inspection endpoint catches only SQL Server unique-constraint
`DbUpdateException` values through `DatabaseConstraintViolation` and returns
HTTP 409 with `Schedule already has a recorded inspection.`. The inspection,
schedule completion update, and search-document creation remain in the same
`SaveChangesAsync` operation, so a rejected duplicate does not leave a partial
record set.

## Database Changes

The migration checks for duplicate `ScheduleId` values before changing the
existing index. It throws a clear migration error rather than deleting, merging,
or selecting an existing inspection. Its `Down` method restores the previous
non-unique index with the same name.

## Tests Present

- Sequential endpoint duplicate conflict and no additional inspection/search
  document.
- Unknown schedule submission returns HTTP 404.
- Successful submission persistence, schedule completion, and search-document
  assertions.
- SQL Server migration-preflight rejection for existing duplicate inspections.
- SQL Server unique-index enforcement.
- SQL Server-backed concurrent HTTP submission with one created inspection,
  one search document, and completed schedule assertions.

## Verification Status

The migration SQL was generated and inspected locally. TEST-011 and TEST-012
record the non-SQL Release runs. TEST-013 records successful local SQL Server
migration-preflight, unique-index, and concurrent endpoint execution.

## Known Limitations

The focused SQL Server integrity tests passed. The separate SQL-enabled full
suite remains blocked by a pre-existing retrieval-benchmark assertion mismatch
outside this branch's scope.
