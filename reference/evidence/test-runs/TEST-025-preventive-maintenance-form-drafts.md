---
id: TEST-025
type: test-run
title: Preventive-maintenance form draft workflow verification
status: executed
recordedAtUtc: 2026-07-28T10:53:34Z
testedCommit: ea003c3cf2e64d17913a2f45cc38f8fcbe6ce1df
sourceBranch: feat/preventive-maintenance-form-drafts
evidenceLevel: locally-executed
---

# Preventive-Maintenance Form Draft Workflow Verification

## Objective

Verify the backend draft-form endpoints and their separation from official
inspection history and maintenance-search projection behavior.

## Execution Identity

- Tested commit: `ea003c3cf2e64d17913a2f45cc38f8fcbe6ce1df`
- Source branch: `feat/preventive-maintenance-form-drafts`
- Execution date: 2026-07-28 UTC

## Commands

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-build `
  --filter "FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests"
```

## Results

- Focused draft-form endpoint tests: passed.

## Test Counts

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| `PreventiveMaintenanceFormDraftEndpointsTests` | 8 | 0 | 0 | 8 |

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

The Release build, full test suite, and native SQL Server 2019 suite were not
rerun for this focused ownership correction. This record makes no production,
deployment, or final workflow-readiness claim.

## Limitations

The routes remain draft-only. They do not submit forms, create file numbers,
acknowledge a form, complete schedules, create corrective handoffs, process
RMRFs, or expose frontend behavior.
