---
id: TEST-100
type: test-run
title: Searchable assignee picker verification
status: executed
recordedAtUtc: 2026-10-18T10:12:33Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedWorkingTreeSourceManifestSha256: a798a26fcb2c6d677fc72932fa71a2db9f1565526b679a137634e81c965eb31c
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Searchable assignee picker verification

## Execution identity

The focused test and build ran on branch `feature/staging-workflow-followups`,
based on HEAD `d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted
changes. The source-manifest hash fingerprints all 32 changed, added, or
deleted paths under `web/` at execution time. It hashes each path and its file
bytes; deleted paths use an explicit marker.

The shell used Node `v24.15.0` and npm `11.12.1`. The web package declares
Node `>=22 <23`, so the build ran outside the supported runtime range.

## Changes under verification

Form review and both batch-assignment roles now use one type-to-suggest field
instead of a search box plus a separate account dropdown. The datalist shows up
to ten matching labels at a time. Duplicate assignment names receive a short
account-ID suffix, and the selected field value maps back to the account ID
used by the existing API.

## Commands and results

| Command | Result |
| --- | --- |
| `npx prettier --write src/components/ui/searchable-option-input.tsx src/features/schedules/schedule-batch-assignment.tsx src/features/preventive-maintenance-forms/form-registry.tsx src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx src/features/schedules/schedule-workflow.test.tsx` | Completed successfully. |
| `npm run test:run -- src/features/schedules/schedule-workflow.test.tsx -t "lets GSD assign the Supervisor stage for the full department, category, and cycle batch"` | Passed 1/1; 20 unrelated tests skipped. Typing the selected account name still submits its ID and preserves the existing batch scope. |
| `npm run build` | Exit code 0. TypeScript project checks passed; Vite transformed 2,224 modules and produced the production bundle. Node 24 is outside the package's declared version range. |
| `git diff --check` | Passed after the build; Git emitted line-ending conversion notices for the OpenAPI snapshot and generated route tree. |

## Not verified

- The Form review-specific supervisor-filter test was updated but not executed.
- Suggestions are limited in the rendered datalist, but existing API endpoints
  still return all active accounts for the relevant role. Server-side account
  search and a bounded response remain open if staff counts grow.
- Playwright, fresh desktop screenshots, full web/backend suites, supported
  Node 22 build, CI, and deployed acceptance were not run.
- No Azure connection, database write, commit, push, or deployment occurred.
