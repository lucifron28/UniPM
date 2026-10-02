---
id: TEST-049
type: test-run
title: Maintenance-history RAG retirement and PMIS verification
status: executed
recordedAtUtc: 2026-10-02T18:41:22Z
testedCommit: f2a1eae2a80bbd5200102da613fb731210457a85
sourceBranch: refactor/retire-maintenance-history-rag
evidenceLevel: locally-executed
---

# Maintenance-history RAG retirement and PMIS verification

## Objective

Verify the retirement branch at one exact commit. Record incomplete migration
and runtime checks without treating them as successful verification.

## Execution identity and environment

All executions target f2a1eae2a80bbd5200102da613fb731210457a85 on
refactor/retire-maintenance-history-rag. Raw logs, TRX files, generated SQL,
and sanitized JSON metadata remain ignored under artifacts/rag-retirement/.

The Windows host used .NET SDK 10.0.300 and native SQL Server 2019 (major 15),
with Full-Text Search installed, compatibility 150, and Windows integrated
authentication. SQL test databases were disposable. Web checks used Node
24.15.0; CI specifies Node 22 and no local Node 22 runtime was available.
Provider credentials were not configured.

## Commands and results

| Command | Result |
|---|---|
| `dotnet build UniPM.slnx --configuration Release --verbosity minimal` | Passed, exit 0. One CS8602 warning at `PreventiveMaintenanceFormEndpoints.cs:442`. |
| Focused `dotnet test` with the six exact method filters below | Passed 6/6, exit 0, on native SQL Server 2019. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-build --no-restore --logger 'trx;LogFileName=verify-02-backend-tests-f2a1eae.trx' --results-directory artifacts/rag-retirement --verbosity minimal` | Passed 248, failed 0, skipped 1, exit 0 (249 total). |
| `dotnet ef migrations has-pending-model-changes --project server/server.csproj --startup-project server/server.csproj --configuration Release --no-build` | Passed, exit 0; no pending model changes. |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/rag-retirement/Verify-RetirementMigration.ps1` | Partial. Populated-data up/down/reapply preservation was not verified. |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/rag-retirement/Smoke-RetirementNoAi.ps1` | Not verified, exit 1. PowerShell failed before HTTP requests; owned-process and database cleanup succeeded. |
| `npm run lint` | Passed, exit 0. |
| `npm run typecheck` | Passed, exit 0. |
| `npm run api:contract:check` | Passed, exit 0. |
| `npm run api:contract:test` | Passed all 7 negative OpenAPI cases, exit 0. |
| `npm run test:run` | Passed 166 tests in 22 files, exit 0. |
| `npm run build` | Passed, exit 0; Vite transformed 2,202 modules. |
| `npm run format:check` | Failed, exit 1; Prettier flagged 99 files. No formatter rewrite was performed. |
| `npm run api:check` | Not verified, exit 1; Orval failed before generated-drift comparison. Generated files were restored from the clean index. |
| `npm run e2e` | Not verified, exit 1; Playwright hit temporary-cache EPERM errors before reporting tests. The owned run was interrupted. |

The six focused SQL methods were:

- `SqlServerDomainContractTests.Migration_preflight_preserves_line_order_when_canonicalizing_mixed_line_endings`
- `SqlServerDomainContractTests.Migration_preflight_rejects_unsupported_overlength_and_canonical_duplicates`
- `SqlServerDomainContractTests.Migration_preflight_canonicalizes_existing_codes_before_constraints`
- `SqlServerInspectionSubmissionIntegrityTests.Migration_preflight_rejects_duplicate_inspections_for_one_schedule`
- `SqlServerInspectionSubmissionIntegrityTests.Acknowledging_submitted_form_preserves_completed_schedule_status_and_completion_times`
- `SqlServerInspectionSubmissionIntegrityTests.Concurrent_form_submissions_assign_distinct_provisional_file_numbers`

## SQL and source verification

The full backend suite's only skip was
`OpenAiCompatibleEmbeddingSmokeTests.Configured_provider_returns_stable_vectors`;
the optional provider endpoint, model, key, and dimensions were not configured.

The retirement rehearsal did not complete a populated-data cycle. Its initial
baseline path failed with Invalid column name PmCycle in generated historical
SQL. The corrected helper established the disposable baseline with EF; app
startup applied the retirement migration, then demo seeding stopped because
required Development identities were absent. Snapshot/hash capture and explicit
SQL upgrade, rollback, reapply, and row-preservation checks did not run. Cleanup
dropped the owned database and found zero remaining retirement databases.

The source audit confirmed that `MapApiEndpoints` registers no
maintenance-review route. A search of active server C# outside migrations and
build output found no maintenance-review, search-projection, retrieval/fusion,
or rebuild implementation. The ReferenceDocument model, Full-Text retrieval,
semantic retrieval, section embeddings, and shared `IEmbeddingService` remain.
`git diff --check` passed, and a whitespace scan found no trailing whitespace
in the index or the two new records.

## Runtime and web limitations

The no-AI harness started its owned API process with embeddings disabled and
legacy flags enabled, then Windows PowerShell failed to resolve
`System.Net.Http.HttpClient`. No health, route, or OpenAPI request ran. Cleanup
stopped the process and dropped the disposable database.

The checkout has LF in the index and CRLF in sampled working files with
`core.autocrlf=true`, making that mismatch the likely cause of the 99-file
Prettier failure. Orval failed after cleaning output because esbuild was denied
access to the repository parent and could not resolve
`web/src/api/http-client.ts`; the drift comparison did not run. Generated files
were restored, the worktree is clean, and `git diff --check` passed.

Playwright could not write source maps under its OS temporary cache (EPERM);
it reported no test assertions before the owned invocation was interrupted.
No local retries were made. `npm run test:coverage` was not run locally to
avoid repeating the complete web unit suite. Exact-head Linux Web CI on Node 22
remains pending.

## Limitations

Runtime HTTP assertions, generated-client drift, populated-data migration
preservation, GSD acceptance, IIS deployment, and real-provider quality are not
verified by this record.
