---
id: TEST-026
type: test-run
title: Preventive-maintenance form submission verification
status: executed
recordedAtUtc: 2026-07-28T00:00:00Z
testedCommit: 62f10e68c4b94d0b3b3e043fc7f1e7ee26e3add8
sourceBranch: feat/preventive-maintenance-form-submission
evidenceLevel: locally-executed
---

# Preventive-Maintenance Form Submission Verification

## Objective

Verify the focused backend submission transition for preventive-maintenance
form drafts, including provisional file-number assignment and ownership rules.

## Execution Identity

- Tested commit: `62f10e68c4b94d0b3b3e043fc7f1e7ee26e3add8`
- Source branch: `feat/preventive-maintenance-form-submission`
- Execution date: 2026-07-28 UTC

## Command

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --filter "FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests"
```

## Results

- Focused preventive-maintenance form endpoint tests: passed.

## Test Counts

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| `PreventiveMaintenanceFormDraftEndpointsTests` | 10 | 0 | 0 | 10 |

## SQL Server Verification

Not executed for this focused endpoint change. The existing form-domain
migration and constraint verification remains recorded in TEST-024.

## AI-Provider Verification

Not applicable. Form submission does not call embeddings, retrieval, or an LLM
provider.

## Generated Artifacts

No retained artifacts were required. Test output was reviewed locally; no
secrets, connection strings, prompts, or vectors were recorded.

## Skipped Verification

The complete Release test suite and native SQL Server 2019 suite were not run.
This focused record does not claim production, deployment, acknowledgement, or
final workflow readiness.

## Limitations

The tested submission behavior uses a provisional configurable file-number
format. It does not acknowledge forms, complete schedules, publish inspection
rows to official history or retrieval, create corrective handoffs, process
RMRFs, or expose frontend behavior.
