---
id: TEST-027
type: test-run
title: Preventive-maintenance form acknowledgement verification
status: executed
recordedAtUtc: 2026-07-28T12:26:30Z
testedCommit: f1aaf27604a8dbe25a7e70dc8c2174d139e39a1b
sourceBranch: feat/preventive-maintenance-form-acknowledgement
evidenceLevel: locally-executed
---

# Preventive-Maintenance Form Acknowledgement Verification

## Objective

Verify the focused backend acknowledgement transition, schedule completion,
official-history visibility, search-document publication, validation, and
authorization behavior.

## Execution Identity

- Tested commit: `f1aaf27604a8dbe25a7e70dc8c2174d139e39a1b`
- Source branch: `feat/preventive-maintenance-form-acknowledgement`
- Execution date: 2026-07-28 UTC

## Command

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --filter "FullyQualifiedName~Acknowledgement_"
```

## Results

Both newly added acknowledgement endpoint tests passed. The filter also
selected one existing optional SQL Server form-domain constraint test, which
was skipped because `UNIPM_SQLSERVER_TEST_CONNECTION` was not configured.

## Test Counts

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Filtered acknowledgement run | 2 | 0 | 1 | 3 |

## Behavior Covered

- Successful form acknowledgement persists the acknowledgement metadata and
  server-calculated signature checksum.
- All linked schedules become `Completed` with a completion timestamp.
- Acknowledged inspection rows enter official history and receive
  maintenance-search documents.
- The acknowledgement path does not generate embeddings.
- Invalid signature input, repeated acknowledgement, wrong form status, and
  unauthorized Inspector acknowledgement are rejected.

## SQL Server Verification

The existing
`SqlServerDomainContractTests.Preventive_form_status_file_number_and_acknowledgement_constraints_are_enforced`
test was skipped because no process-scoped SQL Server test connection was
configured. This record does not claim native SQL Server transaction execution.

## AI-Provider Verification

Not applicable. Form acknowledgement does not call an embedding or summary
provider.

## Generated Artifacts

No retained artifacts were required. Test output was reviewed locally; no
signature payload, secret, connection string, prompt, or vector was recorded.

## Skipped Verification

The complete Release suite and native SQL Server 2019 suite were not run. The
single targeted command was run once, as required by the task.

## Limitations

This verification does not establish corrective-action handoff, RMRF, OEM,
frontend, deployment, or production readiness.
