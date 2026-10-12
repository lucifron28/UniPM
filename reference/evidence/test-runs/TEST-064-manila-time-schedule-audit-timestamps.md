---
id: TEST-064
type: test-run
title: Manila-time schedule audit timestamp regression verification
status: executed
recordedAtUtc: 2026-10-10T01:42:19Z
testedCommit: 7c3d5fd627fb9566ab330abe0adb395d35b2dd4b
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: ci-executed
---

# Manila-time schedule audit timestamp regression verification

## Root cause and correction

`formatScheduleDateTime` used the runtime's local timezone. The hosted Web CI
runner uses UTC, while the review timestamp fixture and institutional display
expect Asia/Manila. The review appeared as 4:00 AM in UTC instead of noon in
Manila, so the component test failed on CI and passed on the developer machine.
The formatter now sets `timeZone: 'Asia/Manila'` explicitly. This also keeps
schedule detail and deferral audit timestamps consistent across devices.

## Commands and results

| Command | Result |
| --- | --- |
| `$env:TZ = 'UTC'; npx vitest run src/features/schedules/schedule-enrollment-deferral-review.test.tsx` | Passed at tested commit `7c3d5fd627fb9566ab330abe0adb395d35b2dd4b`; 1 file, 3 tests passed. |
| `git diff --cached --check` | Passed for the staged one-line source change before commit. |
| GitHub push-triggered Web CI, run `38013964117` | Passed on exact head `7c3d5fd627fb9566ab330abe0adb395d35b2dd4b`; formatting, lint, typecheck, API checks, unit tests, coverage, build, and Playwright steps passed. |
| GitHub pull-request Web CI, run `38013966611` | Passed on exact head `7c3d5fd627fb9566ab330abe0adb395d35b2dd4b`; same verification workflow passed. |
| GitHub PR checks | Notion task sync passed. Backend CI was not triggered by this web-only commit. |

No backend or database source changed. No provider calls or deployment were
performed. The PR remains open and unmerged.
