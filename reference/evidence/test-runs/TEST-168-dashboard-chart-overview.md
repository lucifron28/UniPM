---
id: TEST-168
type: test-run
title: Compact dashboard chart overview
status: executed
recordedAtUtc: 2026-10-27T05:56:24Z
testedCommit: 67810ef9fc116c68be84bea3d0d3785fb1d3848f
sourceBranch: feature/dashboard-chart-overview
evidenceLevel: locally-executed
---

# Compact dashboard overview

The owner requested the approved dashboard redesign in a branch in the original
checkout, without another worktree. The selected changes keep two overview
charts, move other comparisons behind a selector, collapse data and calculation
notes, show values directly, and use a fixed percentage scale.

## Changes

- Department progress and exclusive PM outcomes are the default charts. Older
  responses without batch progress fall back to scheduled/inspected workload.
- The comparison selector shows one additional chart at a time. It includes
  compliance, condition, workload and available assignment-period views.
- Scope appears once. Native disclosure controls retain calculation notes and
  exact-value tables without showing them all at once.
- Single-series charts use direct value labels. Percentage bars and lines use
  0 to 100; 50 percent no longer fills the available width when it is the
  largest returned value. Underlying backend values remain unchanged.
- The shared chart's compact mode applies to the dashboard. Existing NLA data
  tables remain expanded. Print CSS exposes the dashboard's collapsed data.

## Checks

| Check                                                                            | Result              | Scope                                                                                                                                                                                                                    |
| -------------------------------------------------------------------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Existing `pm-period-dashboard.test.tsx`, Vitest                                  | 10 passed, exit 0   | Existing dashboard rendering and workflow regression; executed on the final application working changes later committed as `67810ef`                                                                                     |
| `tsc -b --pretty false`                                                          | Passed at `67810ef` | Final committed application source; an earlier implementation run also passed before the final scaling edits                                                                                                             |
| Browser inspection of actual presentation component using fictional preview data | Passed              | Two initial charts; exact-value disclosure expands/collapses; selecting Condition findings adds just that chart and clearing it restores the overview; 50 percent visibly occupies half the bar at narrow viewport width |
| Scoped formatting and whitespace review                                          | Passed              | The three changed application files                                                                                                                                                                                      |

Browser inspection used a temporary ignored HTML preview importing the actual
`PmPeriodDashboardPresentation` component. It used fictional department/count
values and no database, authentication credentials or external provider.
The original attempt to run a standalone automated browser script was rejected
by automatic command review. No such script executed; inspection continued
through the supported browser tool.

Screenshot: `artifacts/dashboard-chart-overview/overview.png`.

The existing regression test file was not modified. No full test suite, live
backend/database workflow or physical-device run was performed. Print behavior
has source-level CSS review only; a print-dialog or PDF run was not performed.
The original untracked `tmp/` directory and the preserved old-edit stash were
not changed. No backend/API/schema changes are included.
