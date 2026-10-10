---
id: TEST-072
type: test-run
title: Web photo preview CI fixture correction
status: executed
recordedAtUtc: 2026-10-10T18:04:12Z
testedCommit: f48599c01a4ca23257b48a416cbc3988f7c21be0
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: locally-executed
---

# Web photo preview CI fixture correction

## Objective

Correct the photo-preview test fixture that failed in GitHub Web CI. The test
response used a typed-array body that the CI MSW/Undici response path could not
consume. The React Query request stayed pending, so the thumbnail assertion
timed out. No production authentication, storage, or preview behavior was
changed.

## Execution identity

- Tested source commit: `f48599c01a4ca23257b48a416cbc3988f7c21be0`.
- Branch: `feat/inspection-photo-evidence`.
- Repository: `lucifron28/UniPM`.
- Previous failing CI head: `496c30438ccb092672465a2123cdb99c9b18d74b`.

## Commands and results

- `npm run test:run -- src/features/inspections/inspection-workflow.test.tsx -t "shows a private photo thumbnail with an authorized larger preview"`: passed, 1 test; 5 skipped by the name filter.
- `npm run test:run`: passed, 207 tests across 28 files.
- `git diff --check`: passed for the test-fixture correction.

## Failure and correction

GitHub Web CI run 193 on the previous head failed the photo-preview test. MSW's
Undici response path raised `object.stream is not a function` while adapting the
typed-array fixture body. The test now uses `HttpResponse.arrayBuffer(...)`,
which produced a completed photo query in local verification. The focused test
and full web unit suite both pass on the tested source commit.

## Remaining verification

Exact-head Backend CI and Web CI for the evidence-updated pushed head are
pending. Native SQL Server migration execution, physical camera behavior, iOS
build, staging acceptance, deployment, and production file-storage backup and
retention remain NOT VERIFIED as recorded in [TEST-071](TEST-071-inspection-photo-evidence-and-gps-removal.md).
