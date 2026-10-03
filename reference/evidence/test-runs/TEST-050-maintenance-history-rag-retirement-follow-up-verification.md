---
id: TEST-050
type: test-run
title: Maintenance-history RAG retirement follow-up verification
status: executed
recordedAtUtc: 2026-10-03T01:05:22Z
testedCommit: 73c2d4531d8b5bbf5bb4ba2e9681ed0b028bad4b
sourceBranch: refactor/retire-maintenance-history-rag
evidenceLevel: locally-executed
---

# Maintenance-history RAG retirement follow-up verification

## Objective and execution identity

Complete the HTTP smoke and populated migration rehearsal left unverified in
TEST-049, then run focused SQL Server 2019 checks, the full backend test
project, and EF model consistency. All application and database checks below
target `73c2d4531d8b5bbf5bb4ba2e9681ed0b028bad4b`. The Release solution build
ran at `4aa1fb88137a2186bf02553d5aa2e6796b9e79c9`; C# sources were unchanged
between that build and the tested commit. The worktree was clean, and both
working and staged `git diff --check` passed.

The host used .NET SDK 10 and native SQL Server 2019 (major version 15), with
Full-Text Search installed, compatibility level 150, and Windows integrated
authentication. Disposable database names were unique to each run. No provider
credentials or real reference-document content were configured. Raw output is
ignored under `artifacts/rag-retirement/`.

## Commands and results

| Check | Result |
|---|---|
| `dotnet build UniPM.slnx --configuration Release --no-restore` at `4aa1fb8` | Passed. One existing CS8602 nullable warning at `PreventiveMaintenanceFormEndpoints.cs:442`. |
| `pwsh -NoProfile -File scripts/evidence/Invoke-RagRetirementHttpSmoke.ps1 -ExpectedCommitSha 73c2d4531d8b5bbf5bb4ba2e9681ed0b028bad4b` | Passed. Root and both health checks returned 200; health bodies were `Healthy`. GET and POST `/api/v1/maintenance-review` returned 404. `/openapi/v1.json` returned 200 with no maintenance-review path or operation. `GET /api/v1/schedules/` returned 200 with a valid empty JSON array. Embeddings were disabled, provider credentials blank, and the retired review/summary flags set true for the route-removal probe. |
| `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/evidence/Invoke-RagRetirementMigrationVerification.ps1` | Passed at the tested commit. IDs, row counts, and full-row SHA-256 hashes remained unchanged through Up → Down → Up in `Assets`, `PreventiveMaintenanceSchedules`, `InspectionRecords`, `PreventiveMaintenanceForms`, `PreventiveMaintenanceAcknowledgements`, `InspectionLocationAttempts`, `ReferenceDocuments`, `ReferenceDocumentApplicabilities`, `ReferenceDocumentSections`, and `ReferenceDocumentSectionEmbeddings`. Down recreated the two retired storage tables empty (0/0 rows); they and their catalog were absent after Up. Reference Full-Text Search remained available. |
| Focused SQL Server 2019 `dotnet test` filter below | Passed 4/4, 0 failed, 0 skipped. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-build --no-restore` | Passed 248, failed 0, skipped 1 (249 total). The only skip was the optional configured-provider smoke test; provider credentials were unavailable. |
| `dotnet ef migrations has-pending-model-changes --project server/server.csproj --startup-project server/server.csproj --configuration Release --no-build` | Passed; no pending model changes. |

The focused SQL filter was:

```text
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName=UniPM.Api.Tests.SqlServerDomainContractTests.Sql_Server_2019_with_full_text_search_applies_migrations_and_executes_reference_containstable|FullyQualifiedName=UniPM.Api.Tests.SqlServerDomainContractTests.Sql_Server_2019_creates_the_separate_reference_full_text_catalog|FullyQualifiedName=UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Fixture_seed_preserves_unrelated_synthetic_records_and_reference_sections_are_full_text_searchable|FullyQualifiedName=UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Institutional_embedding_rebuild_indexes_future_sections_and_refreshes_stale_vectors'
```

Both SQL test connection variables were process-only and restored after each
run.

## Diagnostics and cleanup

The earlier TEST-049 migration attempt stopped on historical SQL referencing
`PmCycle` before that column existed, then depended on Development identities
that were absent from the empty database. `DevelopmentDemoSeeder.CreateReadyContextAsync`
calls `MigrateAsync`, so it also advances beyond the pre-retirement baseline;
the corrected helper applies the prior migration directly through EF and uses
synthetic PM/reference rows without demo identities. The earlier HTTP smoke
stopped before requests because Windows PowerShell could not resolve
`System.Net.Http.HttpClient`; the corrected harness uses Windows-compatible
HTTP requests.

The first elevated migration attempt failed on an unquoted `RowCount` alias
and a nullable/empty restore-flag condition. The correction quotes the alias
and treats null and empty environment-variable values as equivalent when
verifying restoration. Review also corrected the fixture's September period
value. The single corrected migration retry passed.

The first elevated HTTP attempt reached SQL Server 2019 but startup failed when
the process-only `Jwt:AccessTokenMinutes` setting was empty and could not bind
to its integer option. The harness now supplies `15`; the corrected HTTP run at
the tested commit passed once. Its first non-elevated SQL preflight had been
blocked by unavailable Windows integrated-auth credentials in the sandbox; no
database was created by that attempt.

The first focused SQL invocation used the wrong SQL Server 2019 test-variable
name and skipped all four selected tests. The same filter was rerun with the
fixture's correct variable name and passed 4/4. The first EF check lacked
`ConnectionStrings__DefaultConnection`; rerunning with a process-only native
master connection reported no pending model changes. Each process-only setting
was restored in `finally`.

HTTP response JSON was saved before cleanup. Cleanup verified the API process
identity before stopping only that owned PID, dropped only the generated
disposable database, and confirmed no such database remained. No shared master
database or pre-existing application process was removed. The raw HTTP,
migration, test, and EF logs remain in ignored run directories, including
`artifacts/rag-retirement/20261003T005109310Z-73c2d45/`,
`artifacts/rag-retirement/20261003T005120Z-73c2d453/`, and
`artifacts/rag-retirement/20261003T-sql-focused/`.

## Limitations

The exact-head Web CI workflow was not run as part of this local record; no CI
result is claimed. Real-provider behavior, GSD acceptance, IIS deployment, and
production operation remain unverified. TEST-049 is unchanged.
