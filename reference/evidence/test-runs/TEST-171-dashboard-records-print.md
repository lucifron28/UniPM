---
id: TEST-171
type: test-run
title: Dashboard records and detailed report
status: executed
recordedAtUtc: 2026-10-27T08:02:07Z
testedCommit: fd70ea4d5b3a684004525df26124c2013dc076c2
sourceBranch: feature/dashboard-chart-overview
evidenceLevel: locally-executed
---

# Dashboard records and detailed report

Backend commit `1736853` adds current inspector identity to dashboard rows,
retains first accepted schedule completion for assignment timing, and exposes
unassigned summary counts. Web commit `fd70ea4` adds the records route, count
links, client filters, pagination, and separate summary/detail print actions.

## Executed checks

- Backend `dotnet build server/server.csproj --no-restore -o artifacts/records-verification-api -p:UseAppHost=false -v minimal` passed with zero warnings/errors.
- Existing targeted Vitest `src/features/reports/pm-period-dashboard.test.tsx --maxWorkers=2` passed 10 tests. Existing assertions were updated for changed metric labels. This run preceded final assignment-timing drilldown filters and print CSS adjustments.
- Final `tsc -b --pretty false` passed after the assignment-timing filter changes. Earlier type checks found exact-optional and test-query option defects that were corrected. No full suite or E2E suite was executed.
- Built-in browser Playwright inspection used a hidden 1280 by 720 desktop viewport and the actual local synthetic dataset through a separately built API. The period had 36 records, 21 completed, 15 outstanding, 9 overdue, and 6 unassigned. The unassigned filter returned 6 rows. Page two displayed 11 rows while the print DOM contained all 36 filtered rows. Scope totals remained unchanged by client filters.
- Source review found a print CSS rule that hid the report ancestor and desktop shell overflow that could clip printing. Both were corrected. A desktop screenshot also showed narrow controls and offscreen table columns; filters and table widths were adjusted. Final width adjustments and timestamp formatting were type-checked, not browser-rechecked.
- Clicking the built-in browser print action timed out. Subsequent browser access was disabled in Work settings. No PDF or native print preview was verified. Print pagination, repeated headers and final print visual quality remain unverified.
- One Luna Max executor implemented web source. It hit its usage limit; the orchestrator completed review and corrections. No second executor was started. Initial native desktop inspection ended when the owner required built-in browser verification only; remaining browser checks used the built-in tab.

Screenshot from desktop review before the final table-width correction:
`artifacts/dashboard-records-desktop/records-page2.png`.

The old user `tmp/` and unrelated local files were not changed. No database
migration, seed reset, AI-provider call, push or merge was performed in this task.
Backend changes require restarting the normal API to load its new response field.
