---
id: TEST-027
type: test-run
title: Preventive-maintenance form acknowledgement verification
status: executed
recordedAtUtc: 2026-07-28T14:07:23Z
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
$env:UNIPM_SQLSERVER_TEST_CONNECTION = "<local native SQL Server connection>"

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-restore `
  --filter "FullyQualifiedName~Acknowledging_submitted_form_completes_schedules_and_projects_search_documents"
```

## Results

The native SQL Server acknowledgement test compiled and started, but neither
attempt reached the application endpoint.

The first process-scoped connection attempt failed during the encryption
handshake:

```text
The instance of SQL Server you attempted to connect to requires encryption but
this machine does not support it.
```

The requested rerun used the documented native form-test connection settings:

```text
Server=.;Database=master;Integrated Security=True;Encrypt=False;
TrustServerCertificate=True;
```

It instead failed during Windows Authentication with:

```text
The target principal name is incorrect. Cannot generate SSPI context.
```

Both failures occurred while creating the temporary SQL Server test database.
They do not establish a defect in acknowledgement persistence, schedule
completion, or search-document projection.

## Test Counts

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Native SQL acknowledgement rerun | 0 | 1 | 0 | 1 |

## Behavior Covered

- The new native SQL test is present for acknowledgement persistence, form
  status transition, completion of every linked schedule, search-document
  projection, and absence of generated embeddings.
- The native execution remains blocked by the local encryption configuration
  before those assertions run.
- The prior focused in-memory endpoint run on
  `f1aaf27604a8dbe25a7e70dc8c2174d139e39a1b` passed 2 tests, failed 0, and
  skipped 1 existing optional SQL constraint test.

## SQL Server Verification

`UNIPM_SQLSERVER_TEST_CONNECTION` was configured only in the test process
using Windows Authentication. The first connection was blocked by encryption
negotiation; the requested rerun with the documented native form-test settings
was blocked by the local SSPI principal context. No account or credential has
been retained.

## AI-Provider Verification

Not applicable. Form acknowledgement does not call an embedding or summary
provider.

## Generated Artifacts

No retained artifacts were required. Test output was reviewed locally; no
signature payload, secret, connection string, prompt, or vector was recorded.

## Skipped Verification

The complete Release suite and native SQL Server 2019 suite were not run. The
requested rerun was executed once and was not retried after the SSPI failure.

## Limitations

This verification does not establish native SQL acknowledgement behavior,
corrective-action handoff, RMRF, OEM, frontend, deployment, or production
readiness.
