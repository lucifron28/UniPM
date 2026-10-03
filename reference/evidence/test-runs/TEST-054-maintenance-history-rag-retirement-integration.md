---
id: TEST-054
type: test-run
title: Maintenance-history RAG retirement integration verification
status: executed
recordedAtUtc: 2026-10-03T07:11:49Z
testedCommit: 7cbb09280e3ac168ef46846cf4df0ac4253169e2
sourceBranch: refactor/retire-maintenance-history-rag
evidenceLevel: locally-executed
---

# Maintenance-history RAG retirement integration verification

## Scope and identity

Verification ran at `7cbb09280e3ac168ef46846cf4df0ac4253169e2`. The branch
integrates the maintenance-history RAG retirement with current schedule-read
authorization and the merged web/mobile workflow. Local verification covers
the Release build, migration rehearsal, HTTP smoke, EF model check, and
focused/full backend suites.

## Commands and results

- `dotnet build .\UniPM.slnx --configuration Release`. Passed with 0 errors and
  one existing CS8602 warning at `PreventiveMaintenanceFormEndpoints.cs:442`.
- `pwsh -NoProfile -File .\scripts\evidence\Invoke-RagRetirementMigrationVerification.ps1`.
  Passed on SQL Server 2019, compatibility 150, with Full-Text Search. Up to
  Down to Up preserved IDs, row counts, and full-row SHA-256 hashes for
  `Assets`, `PreventiveMaintenanceSchedules`, `InspectionRecords`,
  `PreventiveMaintenanceForms`, `PreventiveMaintenanceAcknowledgements`,
  `InspectionLocationAttempts`, `ReferenceDocuments`,
  `ReferenceDocumentApplicabilities`, `ReferenceDocumentSections`, and
  `ReferenceDocumentSectionEmbeddings`. Down recreated retired derived tables
  empty. The reference full-text catalog remained available. The owned
  database was dropped.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/evidence/Invoke-RagRetirementHttpSmoke.ps1 -ExpectedCommitSha 7cbb09280e3ac168ef46846cf4df0ac4253169e2`.
  Passed. API root, liveness, and readiness returned 200; health bodies were
  `Healthy`. Retired maintenance-review GET and POST returned 404. OpenAPI
  returned 200 with zero retired paths and operations. Anonymous schedules
  returned 401; GSD login returned 200; authenticated schedules returned 200
  with an empty JSON array. Embeddings were disabled and provider credentials
  were unset. Legacy review and summary flags were set only as route-removal
  probes.
- `dotnet ef migrations has-pending-model-changes --project .\server\server.csproj --startup-project .\server\server.csproj --configuration Release --no-build`.
  The first attempt exited 1 because no design-time connection was configured.
  A rerun with a process-only native `master` connection exited 0 and reported
  no pending model changes. The connection override was restored in `finally`;
  the override applied only to this EF invocation. Backend test hosts retained
  no `DefaultConnection`.
- Focused backend command: `dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AuthorizationPolicyTests|FullyQualifiedName~ScheduleQueryEndpointsTests|FullyQualifiedName~ScheduleBatchAssignmentEndpointsTests|FullyQualifiedName~ScheduleOpenApiContractTests|FullyQualifiedName~SqlServerDomainContractTests|FullyQualifiedName~ReferenceDocumentSqlServerTests" --logger "trx;LogFileName=focused-backend-sql.trx" --results-directory "artifacts\rag-retirement\20261003T065227Z-7cbb0928"`. Passed 62/62 with no skips. The SQL opt-ins were process-only and restored; test hosts had no `DefaultConnection`.
- Full backend command: `dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --configuration Release --no-build --logger "trx;LogFileName=backend-full-sql.trx" --results-directory "artifacts\rag-retirement\backend-full-20261003T070532Z-7cbb0928"`. Passed 259, failed 0, skipped 1 (260 total). The skip was `OpenAiCompatibleEmbeddingSmokeTests.Configured_provider_returns_stable_vectors`; provider credentials were not configured. SQL opt-ins were process-only and restored.

## Artifacts and limitations

Raw output is ignored under `artifacts/rag-retirement/`, including
`20261003T065227Z-7cbb0928/`,
`20261003T065227237Z-7cbb092/`,
`backend-full-20261003T070532Z-7cbb0928/`, and
`execution-20261003T000000Z-7cbb0928/`. The HTTP cleanup record confirms the
owned API process stopped and the generated database was removed. The harness
restored its process environment in `finally`. No password or access token was
written to the artifacts.

Exact-head CI, GSD acceptance, real-provider behavior, IIS deployment, and
production operation are not claimed here.
