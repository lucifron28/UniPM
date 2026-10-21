---
id: TEST-132
type: test-run
title: Mobile acknowledgement UX integration
status: executed
recordedAtUtc: 2026-10-21T05:35:33Z
testedCommit: 0cb6aea68cd5d0840cdebbf96cdf01f0a00ae285
sourceBranch: feature/mobile-acknowledgement-integration
evidenceLevel: locally-executed
---

# Mobile acknowledgement UX integration

The owner requested reviewing, correcting, and merging the remaining mobile
acknowledgement branch into main and staging. Its two unique commits provide
acknowledgement presentation/signature changes and a password visibility
toggle. A direct merge conflicted in the acknowledgement page and its tests.

Current main's versions of those two files exactly matched the parent of the
earlier corrected redesign at `39fa90cffbcd0c6df4df2a266035c915b0ca5404`.
The resolution used that corrected variant, rather than the older incoming
variant, to retain the signatory shortcut, bounded single-pointer signature
capture, page scroll locking, Undo/Clear behavior, and strict PNG assertions.
The earlier undo was an owner scope correction; the current request explicitly
authorizes integrating the UX. Automatic approval initially rejected restoring
the files, then accepted the resolution after the baseline comparison and
current authorization were supplied. No approval bypass was used.

The merged UI includes a compact summary and counts, highlighted findings,
expandable inspection details, concise signatory fields, a clear confirmation,
and a read-only receipt. The password toggle merged without a conflict and was
formatted with its existing widget test. No backend contract, role policy,
database, seed, or whole-form lifecycle behavior changed.

The previously requested deployed-backend Flutter launcher at `fb50333` is also
included. It delegates to the existing Android launcher with the deployed HTTPS
address and optional device selection. Its earlier Windows PowerShell check
used Flutter/ADB stand-ins; it did not launch a real app.

## Executed verification

From `mobile/`, against the exact tested commit above:

```powershell
flutter test --no-pub test/preventive_maintenance_form_acknowledgement_test.dart
```

Result: exit 0, 11 passed, no failures or skips, one invocation. Checks include
360x640 long-form navigation, gesture bounds and secondary pointers, scroll
release, Undo/Clear, exported ink pixels, request/confirmation behavior, error
recovery, and duplicate acknowledgement prevention. Raw output is retained in
ignored `artifacts/mobile-ack-integration/acknowledgement-tests.log`.

`dart format` ran once on the four integrated Dart source/test files. Two were
already formatted; the login source/test were formatted. The staged diff check
passed. No full suite, backend/Web test, APK build, Kotlin build, provider call,
database operation, or Azure update ran. No native Android/Kotlin files changed.
The password-toggle test was merged but not executed locally because the single
focused test-file limit was retained. Automatic CI and physical-device acceptance
are separate from these focused widget results.
