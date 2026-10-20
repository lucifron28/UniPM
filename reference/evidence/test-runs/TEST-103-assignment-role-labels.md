---
id: TEST-103
type: test-run
title: Supervisor and Inspector batch assignment workflow labels
status: executed
recordedAtUtc: 2026-10-18T10:40:58Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedFocusedSourceManifestSha256: 40589441091575e3bc5854ab23a7c67940909e83aa4c84aaeb525cfbd4313ee0
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Supervisor and Inspector batch assignment workflow labels

## Execution identity

The test ran on branch `feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted changes. The
focused source manifest covers `web/src/features/schedules/schedule-batch-assignment.tsx`
and `web/src/features/schedules/schedule-workflow.test.tsx`. It hashes each
relative path and file bytes, separated by zero bytes, producing
`40589441091575e3bc5854ab23a7c67940909e83aa4c84aaeb525cfbd4313ee0`.

The test used Node `v22.23.3` and npm `10.9.9`, within the web package's
declared Node `>=22 <23` range.

## Change under verification

The batch assignment summary and Inspector assignment picker now use the
consistent role term `Inspector` instead of `Skilled worker`. Assignment
eligibility, API requests, and workflow behavior are unchanged.

## Command and result

| Command | Result |
| --- | --- |
| `npm run test:run -- src/features/schedules/schedule-workflow.test.tsx -t 'lets GSD assign the Supervisor stage|lets only the assigned Supervisor choose an Inspector'` (from `web/`) | Exit code 0. **2 passed; 19 unrelated tests skipped** in 25.71 seconds. |
| `git diff --check` | Passed. Git emitted only existing line-ending conversion notices for the OpenAPI snapshot and generated route tree. |

## Not verified

- Fresh browser screenshots, responsive layout, live API assignment handoff,
  deployed acceptance, the full web suite, and GitHub CI.
- No staging account, Azure endpoint, database, or provider was used. No live
  assignment or record write occurred.
- No commit, push, or deployment occurred.
