---
id: TEST-026
type: test-run
title: Preventive-maintenance form submission verification
status: executed
recordedAtUtc: 2026-07-28T00:00:00Z
testedCommit: 773f23e073420edd60a5bdbe804c367b34ee7f10
sourceBranch: feat/preventive-maintenance-form-submission
evidenceLevel: locally-executed
---

# Preventive-Maintenance Form Submission Verification

## Objective

Verify the focused backend submission transition for preventive-maintenance
form drafts, including provisional file-number assignment and ownership rules.

## Execution Identity

- Tested commit: `773f23e073420edd60a5bdbe804c367b34ee7f10`
- Source branch: `feat/preventive-maintenance-form-submission`
- Execution date: 2026-07-28 UTC

## Command

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --filter "FullyQualifiedName~Concurrent_form_submissions_assign_distinct_provisional_file_numbers"
```

## Results

- The focused native SQL Server concurrency test passed using a process-scoped
  Windows Authentication connection to the local SQL Server instance.

## Test Counts

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| `Concurrent_form_submissions_assign_distinct_provisional_file_numbers` | 1 | 0 | 0 | 1 |

## SQL Server Verification

The final commit's native SQL Server concurrency test executed and passed. It
submits two eligible forms concurrently and verifies distinct provisional file
numbers. TEST-024 records earlier form-domain migration and constraint
verification.

## AI-Provider Verification

Not applicable. Form submission does not call embeddings, retrieval, or an LLM
provider.

## Generated Artifacts

No retained artifacts were required. Test output was reviewed locally; no
secrets, connection strings, prompts, or vectors were recorded.

## Skipped Verification

The complete Release test suite and native SQL Server 2019 suite were not run.
The earlier focused endpoint suite passed 10/10 on
`62f10e68c4b94d0b3b3e043fc7f1e7ee26e3add8`, before the final sequence-bound
and deadlock-retry corrections. This record does not claim production,
deployment, acknowledgement, or final workflow readiness.

## Limitations

The tested submission behavior uses a provisional configurable file-number
format. It does not acknowledge forms, complete schedules, publish inspection
rows to official history or retrieval, create corrective handoffs, process
RMRFs, or expose frontend behavior.
