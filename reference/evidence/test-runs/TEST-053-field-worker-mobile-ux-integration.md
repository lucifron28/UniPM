---
id: TEST-053
type: test-run
title: Field-worker mobile UX integration verification
status: executed
recordedAtUtc: 2026-10-03T06:28:06Z
testedCommit: 3f9b7045193593ef3c267750e6a0a37266c12266
sourceBranch: refactor/web-mobile-ux-audit
evidenceLevel: locally-executed
---

# Field-worker mobile UX integration verification

## Scope and identity

The UX merge integrated the verified schedule-read authorization, its tests, OpenAPI metadata, and evidence into the UX branch. Its sole conflict was in `INDEX.md`; both the TEST-051 and TEST-052 histories were preserved. Verification ran at merge commit `3f9b7045193593ef3c267750e6a0a37266c12266` on `refactor/web-mobile-ux-audit`.

## Results

- Release build passed with 0 errors and 1 existing CS8602 warning at `PreventiveMaintenanceFormEndpoints.cs:448`.
- Focused backend authorization, schedule-query, batch-assignment, and OpenAPI tests: 48 passed, 0 failed or skipped.
- Focused web tests: 21 passed across 2 files. Vitest emitted jsdom `scrollTo()` notices; the command exited 0.
- Focused Flutter integration: 71 passed across 5 files, exit 0, including home urgency/assignment and route-return behavior, QR/manual entry points, Inspector assignment filtering, GSD visibility, Draft reuse, and submission.
- The Flutter run used the installed Flutter 3.44.5 and Dart 3.12.2 SDK. `APPDATA` was redirected to ignored Capstone artifacts for the process; all process-only environment overrides were restored.
- Backend and Web CI succeeded on post-merge main `4c2a9f0d2f7d39c2f55cfcc9925d98038bef71bc` (runs 37102122813 and 37102122808); that tree matched approved `05df1c5`. This record does not claim CI for the tested UX commit.

## Commands and artifacts

- Build: `dotnet build .\UniPM.slnx --configuration Release`.
- Backend: `dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AuthorizationPolicyTests|FullyQualifiedName~ScheduleQueryEndpointsTests|FullyQualifiedName~ScheduleBatchAssignmentEndpointsTests|FullyQualifiedName~ScheduleOpenApiContractTests" --logger "trx;LogFileName=backend-focused.trx"`. Its results directory was the ignored Capstone `artifacts/schedule-read-authorization`; resolved command and metadata are retained in `backend-focused.meta.txt`.
- Web: `npm run test:run -- src/features/shared/detail-navigation.test.ts src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx`.
- Flutter from `mobile/`: `& "$env:FLUTTER_ROOT\bin\cache\dart-sdk\bin\dart.exe" --packages="$env:FLUTTER_ROOT\packages\flutter_tools\.dart_tool\package_config.json" "$env:FLUTTER_ROOT\bin\cache\flutter_tools.snapshot" test test\mobile_foundation_test.dart test\preventive_maintenance_form_draft_test.dart test\home_page_return_ux_test.dart test\scanned_asset_pm_presentation_test.dart test\field_worker_workflow_test.dart`. Fully resolved invocation is retained in ignored run metadata.
- Raw backend/Web logs and TRX files are under Capstone `artifacts/schedule-read-authorization/`. Flutter log, exit code, and metadata are under Capstone `artifacts/schedule-inspector-ux-flutter/20261003T141704-202b139f/`.
- Native SQL Server and physical-device verification were not run. No full backend suite or CI result for the tested UX commit is claimed.