---
id: TEST-072
type: test-run
title: Web photo preview CI fixture correction
status: executed
recordedAtUtc: 2026-10-10T18:07:42Z
testedCommit: f6464bd77b8a7d9e83cc0d103cdc52ee26d8c528
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

- Tested source commit: `f6464bd77b8a7d9e83cc0d103cdc52ee26d8c528`.
- Branch: `feat/inspection-photo-evidence`.
- Repository: `lucifron28/UniPM`.
- Previous failing CI head: `496c30438ccb092672465a2123cdb99c9b18d74b`.

## Commands and results

- `npm run format:check`: passed.
- `npm run test:run -- src/features/inspections/inspection-workflow.test.tsx -t "shows a private photo thumbnail with an authorized larger preview"`: passed, 1 test; 5 skipped by the name filter.
- `npm run test:run`: passed, 207 tests across 28 files.
- `git diff --check`: passed for the test-fixture correction.

## Failure and correction

GitHub Web CI run 193 on the previous head failed the photo-preview test. MSW's
Undici response path raised `object.stream is not a function` while adapting the
typed-array fixture body. The test now uses `HttpResponse.arrayBuffer(...)`,
which produced a completed photo query in local verification.

The follow-up Web CI runs 194 and 195 on `e317ab199595e8cb00dc3348deec4f2f371933c7`
stopped at `format:check`; Prettier required a line-wrap adjustment in the
fixture. That formatting correction is included in the tested commit above.
The focused test and full web unit suite pass on that commit.

## Remaining verification

Exact-head Backend CI and Web CI for the next evidence-updated pushed head are
pending. Native SQL Server migration execution, physical camera behavior, iOS
build, staging acceptance, deployment, and production file-storage backup and
retention remain NOT VERIFIED as recorded in [TEST-071](TEST-071-inspection-photo-evidence-and-gps-removal.md).
