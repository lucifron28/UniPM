---
id: TEST-027
type: test-run
title: Preventive-maintenance form acknowledgement verification
status: executed
recordedAtUtc: 2026-07-28T14:18:23Z
testedCommit: 65d2432d77f4219b973144b6f49df8722bbd8027
sourceBranch: feat/preventive-maintenance-form-acknowledgement
evidenceLevel: locally-executed
---

# Preventive-Maintenance Form Acknowledgement Verification

## Objective

Verify the focused backend acknowledgement transition, schedule completion,
official-history visibility, search-document publication, validation, and
authorization behavior.

## Execution Identity

- Tested source commit: `65d2432d77f4219b973144b6f49df8722bbd8027`
- Source branch: `feat/preventive-maintenance-form-acknowledgement`
- Execution date: 2026-07-28 UTC

## Command

```powershell
$env:UNIPM_SQLSERVER_TEST_CONNECTION =
  "Server=.;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;"

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-build `
  --filter "FullyQualifiedName~Acknowledging_submitted_form_completes_schedules_and_projects_search_documents"
```

## Results

The native SQL acknowledgement test passed after execution moved from the
Codex sandbox identity to the native interactive Windows account. A read-only
preflight confirmed Windows Authentication to the default local instance and
SQL Server major version 15 before the test run.

Earlier sandbox-only attempts failed before the test database was created due
to TLS negotiation and SSPI principal-context errors. Those attempts did not
reach the application endpoint and are superseded by this successful run.

## Test Counts

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Native SQL acknowledgement test | 1 | 0 | 0 | 1 |

## Behavior Covered

- The native SQL test passed acknowledgement persistence, form status
  transition, completion of every linked schedule, search-document projection,
  and absence of generated embeddings.
- The prior focused in-memory endpoint run on
  `f1aaf27604a8dbe25a7e70dc8c2174d139e39a1b` passed 2 tests, failed 0, and
  skipped 1 existing optional SQL constraint test.

## SQL Server Verification

The test used a process-scoped Windows Authentication connection to the local
native SQL Server 2019 default instance. No password, account name, or other
credential was retained.

## AI-Provider Verification

Not applicable. Form acknowledgement does not call an embedding or summary
provider.

## Generated Artifacts

No retained artifacts were required. Test output was reviewed locally; no
signature payload, secret, connection string, prompt, or vector was recorded.

## Skipped Verification

The complete Release suite and native SQL Server 2019 suite were not run. Only
the focused acknowledgement test was rerun after the native Windows identity
was available.

## Limitations

This verification does not establish corrective-action handoff, RMRF, OEM,
frontend, deployment, or production readiness.
