---
id: TEST-025
type: test-run
title: Preventive-maintenance form draft workflow verification
status: executed
recordedAtUtc: 2026-07-28T08:07:11Z
testedCommit: 463d38c235d9b74833fcd9f0b679453b62d5fe9a
sourceBranch: feat/preventive-maintenance-form-drafts
evidenceLevel: locally-executed
---

# Preventive-Maintenance Form Draft Workflow Verification

## Objective

Verify the backend draft-form endpoints and their separation from official
inspection history and maintenance-search projection behavior.

## Execution Identity

- Tested commit: `463d38c235d9b74833fcd9f0b679453b62d5fe9a`
- Source branch: `feat/preventive-maintenance-form-drafts`
- Execution date: 2026-07-28 UTC

## Commands

```powershell
dotnet build .\UniPM.slnx -c Release --no-restore

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-build `
  --filter "FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests"
```

## Results

- Release build: passed with 0 warnings and 0 errors.
- Focused draft-form endpoint tests: passed.

## Test Counts

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| `PreventiveMaintenanceFormDraftEndpointsTests` | 5 | 0 | 0 | 5 |

## SQL Server Verification

Not executed for this draft-workflow endpoint change. The existing form-domain
schema and migration verification remains recorded in TEST-024.

## AI-Provider Verification

Not applicable. The draft workflow does not call embeddings, retrieval, or an
LLM provider.

## Generated Artifacts

No retained artifacts were required. Build and test output was reviewed locally
and no secrets, connection strings, prompts, or vectors were recorded.

## Skipped Verification

The full test suite and native SQL Server 2019 suite were not run for this
focused endpoint change. This record makes no production, deployment, or final
workflow-readiness claim.

## Limitations

The routes remain draft-only. They do not submit forms, create file numbers,
acknowledge a form, complete schedules, create corrective handoffs, process
RMRFs, or expose frontend behavior.
