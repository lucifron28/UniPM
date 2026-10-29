---
id: TEST-183
type: test-run
title: Mobile countdown urgency ordering correction
status: executed
recordedAtUtc: 2026-10-29T03:26:18Z
testedCommit: c2fff0aaa1a44e4c31ba1ee0d6d59a7cebff6bf9
sourceBranch: feature/fix-mobile-countdown
evidenceLevel: locally-executed
---

# Mobile countdown urgency ordering correction

The countdown branch placed PM-cycle dates ahead of status priority. The existing
home-screen regression test then displayed Ongoing/Due tasks where it expected
Overdue tasks. Restore the existing status-first comparator and its ascending
PM-cycle tie-breaker. Retain the Asia/Manila civil-date countdown and badge.
Apply Dart formatting to the two files rejected by Flutter CI. No assertions,
fixtures, backend schedules or Inspector assignment-period rules were changed.

The same targeted command that failed on a1797f1248a9f7ded0e8c432506307fcaa848c86
passed on c2fff0a after correction:

```text
flutter test --reporter expanded test/home_page_return_ux_test.dart
```

Working directory: mobile/. Exit code zero, nine tests passed. This includes
orders assigned tasks by urgency then cycle and keeps assignments. No test file
was changed and no full local suite was run. Raw output is ignored under
artifacts/merge-review/mobile-home-fixed.log. Full PR Flutter CI is separate;
its results are available on PR #109. No physical-device verification was done.
