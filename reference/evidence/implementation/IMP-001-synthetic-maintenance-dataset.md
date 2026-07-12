---
id: IMP-001
type: implementation
title: Synthetic maintenance dataset and Development seeder
status: reviewed
recordedAtUtc: 2026-07-12T01:10:00Z
sourceBranch: main
evidenceLevel: source-inspected
---

# Synthetic Maintenance Dataset And Development Seeder

## Objective

Provide fictional, deterministic maintenance records for backend, retrieval,
and client development without importing institutional records.

## Source Identity

- Source branch: `main` at merge commit `00e5401`.
- Relevant commits: `18ae9ab`, `5ecb45c`, `a024d5f`, `93df1d7`, `35141fd`,
  `71769e7`, `8ef1cbdb`, `882304e`, and `252dea2`.
- Implementation dates are knowable from Git history: the initial fixture and
  seeder work is dated 2026-07-10; later hardening follows the listed commits.
- Current source paths: `server/Data/Seeding/`,
  `server/Data/Seeding/Resources/synthetic-maintenance-v1.json`,
  `server/Data/Seeding/Resources/synthetic-maintenance-v1.schema.json`.

## Implementation Summary

The repository contains a versioned operational fixture with deterministic
actors, assets, schedules, inspections, references, and fixture-owned IDs. The
Development-only commands support deterministic seed, reset, and projection
rebuild behavior. Preflight validation rejects unsupported references, unsafe
fixture content, unmapped properties, invalid counts, and reset dependencies.

## Architecture And Contracts

The operational fixture is separate from the test-only retrieval evaluation
manifest. The seeder maps only approved operational fields to existing domain
entities and keeps unresolved Page 2, acknowledgement, RMRF, and workflow rules
out of final schema decisions.

## Important Files

- `server/Data/Seeding/SyntheticMaintenanceDataset.cs`
- `server/Data/Seeding/SyntheticMaintenanceDatasetLoader.cs`
- `server/Data/Seeding/SyntheticMaintenanceDatasetValidator.cs`
- `server/Data/Seeding/SyntheticMaintenanceSeeder.cs`
- `server/Program.cs`
- `tests/UniPM.Api.Tests/Seeding/`

## Database Changes

The implementation uses existing `Asset`, `PreventiveMaintenanceSchedule`, and
`InspectionRecord` persistence. It does not establish a production import
contract for unresolved GSD workflows.

## Tests Present

The source tree contains fixture schema, loader, preflight, seed idempotency,
safe reset, dependency, QR, actor, and sensitive-data tests. This record does
not claim those historical tests were executed on the implementation commits.

## Verification Status

Source-inspected only for the historical chronology. A fresh current-state
execution is recorded in `TEST-001-current-backend-baseline.md`.

## Known Limitations

The fixture is fictional and provisional. It is based on visible Page 1 blank
forms and does not finalize Page 2, acknowledgement, RMRF, location authority,
or completed institutional sample rules.

## Related Evidence

- [IMP-005](IMP-005-retrieval-benchmark.md)
- [TEST-001](../test-runs/TEST-001-current-backend-baseline.md)
