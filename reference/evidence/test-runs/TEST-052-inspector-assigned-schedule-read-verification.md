---
id: TEST-052
type: test-run
title: Inspector assigned-schedule read verification
status: executed
evidenceLevel: locally-executed
testedCommit: ab29344d4f247cf557b548a52b50e012bbfb583d
sourceBranch: fix/schedule-read-authorization
testedAtUtc: 2026-10-03T05:51:31Z
recordedAtUtc: 2026-10-03T06:02:12Z
---

# Inspector assigned-schedule read verification

## Scope

- Schedule list/detail reads authorize GSD, Inspector, and Supervisor. Anonymous requests return 401; authenticated roles outside the policy return 403.
- Inspector list/detail queries are limited to schedules assigned to the authenticated user. Unassigned, other-user, and nonexistent detail all return 404.
- Schedule writes and batch-assignment behavior are unchanged.

## Verification

- Release solution build passed with 0 errors and 1 existing CS8602 warning.
- Focused backend authorization, query, batch-assignment, and OpenAPI tests: 48 passed, 0 failed/skipped.
- Full standard backend suite: 395 passed, 0 failed, 41 skipped (436 total), exit 0. SQL opt-ins were not set.
- Flutter focused tests passed 55/55 at this same commit, including the Inspector assigned-schedule workflow.
- The Flutter bootstrap gap from TEST-051 is closed. The initial sandbox run could not write the per-user Flutter state or SDK cache lockfile; the cached Dart/Flutter tool entrypoint passed under approved elevation with process-only APPDATA redirected to ignored artifacts. Environment variables were restored; no SDK files were changed.
- OpenAPI operations and DTO shapes did not change, so client regeneration and web tests were not repeated. No CI result is claimed here.

## Commands and artifacts

- Backend build: `dotnet build UniPM.slnx --configuration Release --verbosity minimal`.
- Focused backend: `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AuthorizationPolicyTests|FullyQualifiedName~ScheduleQueryEndpointsTests|FullyQualifiedName~ScheduleBatchAssignmentEndpointsTests|FullyQualifiedName~ScheduleOpenApiContractTests' --logger 'trx;LogFileName=schedule-read-authorization-focused.trx' --results-directory artifacts\schedule-read-authorization\20261003T055131Z-ab29344\focused-results --verbosity minimal`.
- Full backend: `dotnet test UniPM.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFileName=full-backend-standard.trx' --results-directory artifacts\schedule-read-authorization\20261003T055131Z-ab29344\full-standard-results --verbosity minimal`.
- Full backend command is also preserved in `artifacts/schedule-read-authorization/20261003T055131Z-ab29344/full-backend.command.txt`; logs and TRX are in that run directory.
- Flutter command from `mobile/`: `& "$env:FLUTTER_ROOT\bin\cache\dart-sdk\bin\dart.exe" --packages="$env:FLUTTER_ROOT\packages\flutter_tools\.dart_tool\package_config.json" "$env:FLUTTER_ROOT\bin\cache\flutter_tools.snapshot" test test\mobile_foundation_test.dart test\preventive_maintenance_form_draft_test.dart`. The fully resolved invocation is retained in ignored run metadata.
- Flutter SDK: Flutter 3.44.5, Dart 3.12.2. Run log, exit code, metadata, and isolated tool state: `artifacts/schedule-inspector-flutter/20261003T135254-7ddd8c0e/`.
- Native SQL Server verification was not run. EF consistency was not applicable; no model or schema files changed.
