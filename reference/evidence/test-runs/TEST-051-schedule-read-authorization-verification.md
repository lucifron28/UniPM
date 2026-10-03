---
id: TEST-051
type: test-run
title: Schedule read authorization verification
status: executed
evidenceLevel: locally-executed
testedCommit: 1634fdb6ba974cd9298083deea1397874d5505ee
---

# Schedule read authorization verification

## Objective

Verify schedule list/detail authorization, the OpenAPI contract, and focused and full backend behavior after schedule reads became authenticated. Flutter verification was attempted but did not complete.

## Execution identity

- Web contract and tests: `1634fdb6ba974cd9298083deea1397874d5505ee`.
- Backend build and tests: `cfb582c8011492bb0f7b9415057f3b41ce2c0e50` (C# source was unchanged in the later OpenAPI artifact commit).
- Branch: `fix/schedule-read-authorization`.

## Commands and results

- `dotnet build UniPM.slnx --configuration Release --verbosity minimal`. Passed, 0 errors and 1 existing CS8602 warning.
- `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AuthorizationPolicyTests|FullyQualifiedName~ScheduleQueryEndpointsTests|FullyQualifiedName~ScheduleBatchAssignmentEndpointsTests|FullyQualifiedName~ScheduleOpenApiContractTests' --logger 'trx;LogFileName=schedule-read-authorization-focused.trx' --results-directory artifacts/schedule-read-authorization/20261003T-build-cfb582c8/focused-results --verbosity minimal`. 47 passed, 0 failed/skipped.
- `dotnet test UniPM.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFileName=full-backend-standard-no-sql-optins.trx' --results-directory artifacts/schedule-read-authorization/20261003T-build-cfb582c8/full-standard-results --verbosity minimal`. 394 passed, 0 failed, 41 skipped (435 total); no SQL opt-ins.
- Hidden Development API export fetched only `/openapi/v1.json` on an owned loopback port. It used a unique unused database name without creating or contacting that database; AI, embedding, review, and summary options were disabled. Process environment was restored and only the owned API process was stopped. The contract sanity check passed; the snapshot adds 401/403 responses for schedule list and detail GET operations.
- `npm run api:pull` and `npm run api:generate`. Passed. Generated changes are limited to schedule GET error response types; DTO/model content is unchanged.
- `npm run api:contract:test`. 7 passed.
- `npm run test:run -- src/features/schedules/schedule-workflow.test.tsx src/api/http-client.test.ts`. 26 passed across 2 files.
- `npm run api:check`. Generation completed, but the local drift guard reported 44 generated model files modified in Windows status despite zero content diffs. Those zero-diff files were restored; no retry was run.
- `flutter test test/mobile_foundation_test.dart test/preventive_maintenance_form_draft_test.dart`. Not verified: bootstrap stalled without producing a log or Dart runner. Only the owned command process was stopped.

## Limitations and artifacts

An earlier test attempt was invalid because the executor-added dummy `DefaultConnection` enabled SQL Server registration in InMemory test hosts. Native SQL verification is not claimed. EF consistency was not applicable. CI is outside this local record. Raw outputs and TRX files are ignored under `artifacts/schedule-read-authorization/20261003T-build-cfb582c8/` and `artifacts/auth-schedule-openapi/20261003T034231Z-a27badcf/`.
