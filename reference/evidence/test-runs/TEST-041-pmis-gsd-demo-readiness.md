---
id: TEST-041
type: test-run
title: PMIS GSD demonstration readiness verification
status: executed
recordedAtUtc: 2026-09-14T03:39:43Z
testedCommit: c9deb6d6b90b51f59c527b4d23f6202c8030cb22
sourceBranch: feature/pmis-gsd-demo-readiness
evidenceLevel: locally-executed
---

# PMIS GSD demonstration readiness verification

## Execution identity

- Tested implementation commit: `c9deb6d6b90b51f59c527b4d23f6202c8030cb22`
- Runbook smoke-test commit: `88dab27d596f357ce3b7873646a65b8e08f8087d`
- Execution date: 2026-09-14 Asia/Manila
- Database: isolated local development database with no real records
- SQL Server: major version 15, Full-Text Search installed, compatibility level
  150

No credentials, connection strings, access tokens, raw signatures, machine
paths, or real institutional records are retained in this record.

## Results

| Check | Result |
| --- | --- |
| Focused form-review and inspection-contract Vitest files | 8 passed, 0 failed |
| Orval generation and committed-client drift check | passed |
| Web type check and production build | passed |
| Focused preventive-maintenance form API tests | 18 passed, 0 failed |
| Native SQL form constraints, concurrent file numbers, and acknowledgement publication | 3 passed, 0 failed |
| Database migration command | passed; database already current |
| Development-user seed | passed; 5 users and 5 roles ready |
| Synthetic maintenance seed | passed; 20 assets, 34 schedules, 30 inspections |

The native SQL test process required the Windows user identity for integrated
authentication. Attempts from the restricted process failed before test logic
ran. The successful result above came from the same targeted command under the
authenticated Windows process.

## Commands

```powershell
npm run test:run -- `
  src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx `
  src/features/inspections/inspection-contract.test.ts

npm run api:check
npm run build

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --filter "FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests" `
  --logger "console;verbosity=minimal"

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release --no-build `
  --filter "FullyQualifiedName~Preventive_form_status_file_number_and_acknowledgement_constraints_are_enforced|FullyQualifiedName~Concurrent_form_submissions_assign_distinct_provisional_file_numbers|FullyQualifiedName~Acknowledging_submitted_form_completes_schedules_and_projects_search_documents" `
  --logger "console;verbosity=minimal"

dotnet run --project server -c Release --no-build --no-launch-profile -- `
  --seed-development-users

dotnet run --project server -c Release --no-build --no-launch-profile -- `
  --seed-synthetic
```

Process-scoped database and development-user settings were supplied during SQL
and seed execution and are intentionally omitted.

## Limits

- The complete backend and web suites were not run.
- Flutter checks were not rerun because this branch does not change mobile
  code. `TEST-039` covers the current confirmed mobile forms, and `TEST-040`
  records the prior physical-device workflow acceptance.
- No new physical-device walkthrough, browser-to-mobile rehearsal, or backup
  video was completed in this run.
- No external AI provider was configured or contacted.
- Production deployment, release networking, and real GSD data remain outside
  this validation result.

## Result

**CONDITIONAL PASS.** The code, generated contract, web presentation, native SQL
invariants, and isolated demo setup passed. The branch is ready for review. The
actual GSD demonstration should wait until the current build completes one
fresh walkthrough on the intended physical device and the backup video is
recorded.
