---
id: TEST-045
type: test-run
title: Mobile CPMP presentation and preserved field workflow verification
status: executed
recordedAtUtc: 2026-09-30T19:08:03Z
testedCommit: e87a2290e283ab265523375523c862b335442032
sourceBranch: fix/cpmp-month-end-deadlines-compliance
evidenceLevel: locally-executed
---

# Mobile CPMP presentation and preserved field workflow verification

Windows execution used Flutter 3.44.5 and Dart 3.12.2. No AI provider was called.
The final focused test run and scoped formatting check executed clean commit
`e87a2290e283ab265523375523c862b335442032`. The analyzer passed on the completed presentation source committed as
`418e4849bcbfa870838c0e57ee440f8e074af338`; the subsequent change was one test
assertion, with no production source changes.

| Check | Result |
|---|---|
| `dart format --output=none --set-exit-if-changed` on all 12 changed Dart files | Passed, zero changes |
| `flutter analyze` | Passed, no issues |
| `flutter test --no-pub --reporter expanded` on nine focused files | 103 passed, zero failures |
| `git diff --check` | Passed |
| Whole-mobile read-only formatting check on 59 files | Failed on seven unchanged baseline files |

Focused files were `pm_cycle_presentation_test.dart`,
`scanned_asset_pm_presentation_test.dart`, `field_worker_workflow_test.dart`,
`mobile_field_workflow_ux_test.dart`, `batch_progress_calculation_test.dart`,
`inspection_location_test.dart`, `preventive_maintenance_form_draft_test.dart`,
`preventive_maintenance_form_acknowledgement_test.dart`, and
`damaged_qr_manual_entry_e2e_test.dart`, all under `mobile/test/`.

Coverage includes month/year labels, separate month-end labels, leap-year
February, 30- and 31-day months, invalid cycles and year zero, UTC calendar
fields, canonical November cycles with +14:00/-12:00 timestamp offsets,
historical timestamp disagreement, selector cycle/status, unchanged progress
fractions, assignment/visibility, Draft reuse/resume, row saves, location,
submission gating, manual lookup and acknowledgement. Source inspection found
no `Scheduled for` or compliance label in `mobile/lib/`.

The initial run on `418e484` passed 102 tests and failed one broad text finder:
`textContaining('Due')` matched both the Due status and new Due date label.
Commit `e87a229` makes the assignment assertion use the exact status text. The
same nine-file run then passed all 103 tests. Filtering code was not changed.

Unchanged formatting baseline files are `lib/features/assets/asset_repository.dart`,
`lib/features/auth/login_page.dart`,
`lib/features/preventive_maintenance/preventive_maintenance_acknowledgement_page.dart`,
`lib/features/qr_scanner/qr_scanner_page.dart`,
`test/damaged_qr_manual_entry_e2e_test.dart`,
`test/preventive_maintenance_form_acknowledgement_test.dart`, and
`test/qr_scanner_test.dart`. The read-only check did not edit them.

Raw logs remain ignored under `artifacts/cpmp/`: `mobile-format.log`,
`mobile-format-scoped.log`, `mobile-analyze.log`,
`mobile-focused-tests.log`, and `mobile-focused-tests-final.log`.

Physical-device timezone changes, Android/iOS builds and physical-device
acceptance were not executed. Offset and calendar tests establish deterministic
presentation behavior, not a physical-device rehearsal. Offline sync and IIS
remain outside scope. Backend/web verification remains [TEST-044](TEST-044-cpmp-scheduling-compliance.md);
no backend, web, database or backend-test files changed after its green CI commit.
