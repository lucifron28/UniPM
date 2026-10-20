---
id: TEST-126
type: test-run
title: Mobile acknowledgement signature and review UI
status: executed
recordedAtUtc: 2026-10-20T21:28:35Z
testedCommit: e14a49f317108afda745300ff466a795afbd1f48
sourceBranch: main
evidenceLevel: locally-executed
---

# Mobile acknowledgement signature and review UI

The tested source is the base commit above plus the acknowledgement page and
its focused test patch. Unrelated working-tree changes are excluded.

## Change and review

Signature input previously clamped outside movements to the surface edge and
continued the stroke. The revised pad checks coordinates against its painted
RenderBox, ends drawing on exit, and retains the pointer identity until release
or cancellation. Secondary pointers cannot take over an exited gesture.
The surface has explicit clipping, a light signing background in both themes,
Undo and Clear, and a separate visual logo and placeholder. The PNG encoder
uses stroke data only and the bounded surface dimensions. Export width is
limited to stay within the existing backend PNG byte limit.

The page uses a compact identity summary, individual expandable inspections,
visible non-operational remarks and recommendations, concise signatory inputs,
and a theme-aware read-only acknowledgement receipt. Whole-form validation,
confirmation, permissions, and backend contracts retain their existing meaning.

## Seed and supplied-form review

Source inspection of DevelopmentDemoSeeder and CpmpDemoHistorySeeder found
synthetic mixed-condition batches, lifecycle states, synthetic signatories,
and a one-pixel placeholder signature. Those fixtures can exercise presentation
but cannot establish real signature capture. No seed data was changed or run.

The reviewer rendered and inspected the first page of each supplied document:
FE_3rd_2026.pdf, emergency-lights-2nd-sem-25_26.pdf,
water-drinking-station-3rd-2026.pdf, and fire-alarm-2nd-sem-25_26.pdf.
These are scanned documents. Text extraction did not provide a usable field map.
This was a layout sample review, not a review of every page or a data import.
No real names, signatures, inventory entries, or maintenance history were copied
into application source or fixtures. Rendered pages remain in ignored artifacts.

The water-station sample has acknowledgements per row, unlike UniPM's confirmed
whole-form lifecycle. This UI change does not resolve that workflow difference.

## Verification

Formatting completed for the two changed Dart files. An initial sandboxed
invocation stalled in Flutter's SDK lock loop before loading tests and was
stopped. The owner approved one retry outside the sandbox and one in-scope
correction/rerun. That retry reached 1 passing and 5 failing tests, then stalled
during image decoding in fake async and was interrupted. This is an incomplete
run, not a completed suite result. Retained output is
`artifacts/mobile-ack-review/ack-tests-retry.log`.

The run exposed a colored inspection container hiding the ListTile Material
layer, a count chip overflow at larger text, and scrolling/image-decoding test
harness issues. These were corrected. The approved rerun completed with exit 1,
9 passed and 2 failed. The failures concern an outdated confirmation-copy
assertion and PNG pixel-coordinate sampling after the signing gesture. Output
is retained at `artifacts/mobile-ack-review/ack-tests-rerun.log`.

The owner then approved completion beyond the retry limit. The final focused
run passed all 11 tests with exit 0. Its output is retained at
`artifacts/mobile-ack-review/ack-tests-final.log`. The pixel test now captures
the expected local coordinate before drawing, verifies the stored stroke point,
and samples the exported pixel at that coordinate. The earlier pixel failure
was caused by computing the coordinate after the page position changed.
The confirmation assertion now matches the existing full dialog text.

Passing coverage includes a 360×640 long form with 24 synthetic inspections,
eight non-operational findings, and 1.25 text scaling; pointer bounds,
secondary-pointer rejection and re-entry behavior; scroll release; Undo/Clear
and empty-signature prevention; surface-sized PNG pixels without the UI-only
layers; acknowledgement request fields, confirmation, error handling, and the
read-only success receipt. Physical painting behavior was not established.

Commands, from `mobile/`:

```powershell
dart format lib/features/preventive_maintenance/preventive_maintenance_acknowledgement_page.dart test/preventive_maintenance_form_acknowledgement_test.dart
flutter test test/preventive_maintenance_form_acknowledgement_test.dart
```
No backend, Web, full Flutter suite, provider, database, Azure, or deployment
operation is part of this check.

## Limits

Widget tests do not establish the reported behavior on a physical Android device.
Manual checks should cover fast gestures across each edge, cancellation, a
second finger, keyboard scrolling, dark mode, and large system text.
No APK was built or released by this task.
