---
id: TEST-172
type: test-run
title: Schedule batch side panels
status: executed
recordedAtUtc: 2026-10-27T08:32:21Z
testedCommit: fde2bbc310338d3ef36a2df2ebe3f9c8fa01a0c8
sourceBranch: feature/dashboard-chart-overview
evidenceLevel: locally-executed
---

# Schedule batch side panels

Batch history now opens once in a native modal drawer instead of duplicated
inline desktop/mobile content. Planning batch schedule review uses the same
right-aligned panel with paginated records. The existing assignment form uses
a matching right-side layout. Panels have a visible Back to batches action
and native Escape/focus handling. API and authorization behavior are unchanged.

The working source subsequently committed as `fde2bbc` was checked with:

- `tsc -b --pretty false`: passed before final filter-close and existing test
  expectation adjustments. No production type changes followed that check.
- `vitest run src/features/schedules/schedule-workflow.test.tsx --maxWorkers=2`:
  first run 23 passed and 2 failed due to previously duplicated inline content
  and an unnamed dialog selector now matching nested dialogs. Existing tests
  were corrected to select the reassignment dialog by name, expect one detail
  link, and close/reopen the drawer around filter changes. One focused rerun
  passed all 25 tests, exit 0.
- Scoped formatting and `git diff --check`: passed.

Built-in browser verification was unavailable because local browser use was
disabled in Work settings. No screenshot, desktop visual acceptance, full suite,
new test cases, backend/database mutation, push or merge was performed.
