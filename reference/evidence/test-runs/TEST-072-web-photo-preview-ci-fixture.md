---
id: TEST-072
type: test-run
title: Web photo preview CI fixture correction
status: executed
recordedAtUtc: 2026-10-10T18:17:53Z
testedCommit: a7e0fbed11fa721d0077786356a3f31a0dd78dbc
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: locally-executed
---

# Web photo preview CI fixture correction

## Objective

Correct the photo-preview test that failed in GitHub Web CI. The JSDOM XHR
adapter could not complete Axios `responseType: "blob"` through MSW/Undici, so
the React Query request stayed pending and the thumbnail assertion timed out.
No production authentication, storage, or preview behavior was changed.

## Execution identity

- Tested source commit: `a7e0fbed11fa721d0077786356a3f31a0dd78dbc`.
- Branch: `feat/inspection-photo-evidence`.
- Repository: `lucifron28/UniPM`.
- Previous failing CI heads: `496c30438ccb092672465a2123cdb99c9b18d74b`, `e317ab199595e8cb00dc3348deec4f2f371933c7`, `99e5563fda9a02f3c593661e825fa05294114757`, and `7cd82b987acf028a3dced2891e31952069a0efa4`.

## Commands and results

- `npm run format:check`: passed.
- `npm run test:run -- src/features/inspections/inspection-workflow.test.tsx -t "shows a private photo thumbnail with an authorized larger preview"`: passed, 1 test; 5 skipped by the name filter.
- `npm run test:run`: passed, 207 tests across 28 files.
- `git diff --check`: passed for the test-fixture correction.

## Failure and correction

GitHub Web CI run 193 on the previous head failed the photo-preview test. MSW's
Undici response path raised `object.stream is not a function` while adapting a
typed-array fixture body.

The follow-up Web CI runs 194 and 195 on `e317ab199595e8cb00dc3348deec4f2f371933c7`
stopped at `format:check`; Prettier required a line-wrap adjustment in the
fixture. That formatting correction is included in the current tested commit.

Web CI runs 196 and 197 on `99e5563fda9a02f3c593661e825fa05294114757` passed
formatting but still failed the photo test with `object.stream` for an
ArrayBuffer body. Runs 198 and 199 on
`7cd82b987acf028a3dced2891e31952069a0efa4` failed the same way with a string
body. These results identify the failure at the JSDOM XHR/Blob adapter boundary,
not in the response fixture body.

The test now stubs `httpClient.get` only for the photo URL with an in-memory
Blob, checks that the request uses `responseType: "blob"`, and leaves all other
API calls on MSW. It tests thumbnail and preview rendering without claiming to
test binary transport or image decoding. The focused test, full web unit suite,
and format check pass locally on the tested commit.

## Remaining verification

Exact-head CI for the pushed PR head is recorded in
[TEST-073](TEST-073-photo-preview-exact-head-ci.md). Any later source or
evidence commit needs its own exact-head workflow verification. Native SQL
Server migration execution, physical camera behavior, iOS build, staging
acceptance, deployment, and production file-storage backup and retention remain
NOT VERIFIED as recorded in [TEST-071](TEST-071-inspection-photo-evidence-and-gps-removal.md).
