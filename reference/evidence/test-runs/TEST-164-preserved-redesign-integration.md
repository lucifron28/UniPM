---
id: TEST-164
type: test-run
title: Preserved redesign integration and NLA scope corrections
status: executed
recordedAtUtc: 2026-10-26
testedCommit: 545b531b32f709df001257e9612fc5a0a677bc72
sourceBranch: feature/nla-redesign-integration
evidenceLevel: locally-executed
---

# Redesign integration verification

The tested source is `545b531b32f709df001257e9612fc5a0a677bc72`, on top
of the remediation baseline `00e5ddc0bf441fcc7e89ece6a931e47e3ee40768`.
Closing documentation does not change its executable source. This record
covers focused redesign checks. TEST-163 retains the previous full regression
at its own revision; its full-suite counts are not new redesign results.

## Corrections

The original three-file working patch was imported using a three-way apply
onto the remediation baseline. The original checkout and its untracked PDF
page images remain unchanged. Mobile changes affect only highlighted-card
borders and spacing. Web changes reorganize report review and preview,
explain scheduled-cycle scope and retain explicit consent when an inspection
date cannot be used. They preserve backend validation and calculation.

The review found and corrected these defects:

- A falling-behind default overwrote explicit sorting. Existing `orderBy`
  or `top` now prevents insertion of the default missed-count sort.
- A friendly missed-count sentence hid other aggregate thresholds. It now
  appears only for the sole strict `missedCount > 0` filter. Combined criteria
  remain visible in both review and preview.
- A pure text function used a `use` prefix, causing a React hook lint error.
  Renaming the helper fixed the error without introducing hook behavior.
- The redesigned Back action indexed an array without checking the result.
  Configured-month filtering also failed to narrow null entries. Explicit
  guards resolve these TypeScript errors.
- Source-record drilldown status was inside a panel hidden during preview.
  The existing browser assertion caught this regression. The status now sits
  outside that panel; the visibility assertion remains unchanged.

The added regressions check preserved explicit sorting, preserved explicit
ranking, and all aggregate criteria in review and preview. Their initial
driver clicked Generate before catalog readiness; they now wait for the
existing enabled condition. Payload and scope assertions were retained.

## Final commands

Web commands ran in `web/` under Node 22.23.4 with Vitest's process-based
`forks` pool and two-worker limit. All times below are UTC on October 26.

| Command | UTC interval | Actual result |
|---|---|---|
| `npm run e2e -- --workers=2 e2e/pm-analytics-v3.spec.ts` | 03:43:31–03:43:38 | Exit 0, two passed, zero failed/skipped |
| `npm run test:run -- --maxWorkers=2 src/features/reports/pm-analytics-v3.test.tsx` | 03:43:43–03:43:52 | Exit 0, 24 passed, zero failed/skipped |
| `npm exec eslint -- --max-warnings 0 src/features/reports/pm-analytics-v3.tsx` | 03:43:58–03:44:05 | Exit 0, changed component passed |
| `npm run build` | 03:44:10–03:44:22 | Exit 0, TypeScript and production build passed |

Browser execution used process-only `CI=true`, port 5197 and
`VITE_API_BASE_URL=http://127.0.0.1:5199`. The port was initially free,
server reuse was disabled and the spec mocked API routes. No real backend
account, institutional database or AI provider was contacted. The narrow
viewport PM test checks preview, confirmation, result counts, contributing
source records and inspection links. The inventory test checks its plan has
no PM period. No timeout or workflow assertion was weakened.

The imported mobile file passed
`dart format --output=none --set-exit-if-changed lib/features/preventive_maintenance/preventive_maintenance_page.dart`
before its styling commit, with one file checked and zero changes. This
format check is not mobile runtime or physical-device acceptance. No mobile
or backend tests were rerun because this task changed only client
presentation and NLA review behavior.

## Retained failed attempts

| Source | Check | Actual result and correction |
|---|---|---|
| `e8bb1dd` | `npm run lint` | Exit 1, one hook-name error in the pure wording helper; corrected in `3a0548e`. |
| `3a0548e` | `npm run build` | Exit 2, unchecked index and nullable month entries; corrected in `1183331`. |
| `3a0548e` | Focused NLA file | 21 passed, three failed because new drivers clicked the disabled Generate control; corrected readiness waits in `1183331`. |
| `1183331` | Focused browser spec | One passed, one failed because the drilldown notice was hidden; corrected the UI in `545b531`. |

At `1183331`, the build, 24-case NLA file and changed-file ESLint had passed
before the browser revealed the notice regression. Full ESLint had passed
at `3a0548e`; it is distinct from subsequent changed-file checks.
No previous failure has been erased or presented as a pass.

## Artifacts and limits

Raw outputs remain ignored under `artifacts/redesign/clients/`. The final
browser and unit logs are `playwright-nla-v3-spec-after-notice-fix.log` and
`web-nla-vitest-after-notice-fix.log`; lint and build logs are
`web-eslint-drilldown-notice.log` and `web-production-build-drilldown-notice.log`.
Earlier outputs retain the failed source SHAs. The first browser failure
screenshot and context are preserved under
`artifacts/redesign/clients/playwright-first-failure/`.

No remote CI run, full-suite rerun, deployment, new live-model evaluation,
native SQL migration, signed Android release or physical-device acceptance
was performed. The backend, migrations, API contracts and offline storage
implementation are unchanged from the remediation branch.
