---
id: TEST-158
type: test-run
title: Offline storage failure and account isolation recovery
status: executed
recordedAtUtc: 2026-10-25
testedCommit: e5d739c09aa054a002e4a7736160269326217848
additionalTestedCommits:
  - 3b895e3970a9565776aa41b8fd3ff833b94870f5
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Offline storage failure recovery

The Phase 8 audit found an urgent data-loss defect. Logout cleared queued work
in `finally` after persistence failed, although the screen told the user to stay
signed in and retry. This correction was brought forward before Phase 5.

Commits `1100ab6` and `d25e5ce` separate the retained cache owner from the
currently authorized account. Failed explicit logout retains queued work for
retry. Forced session exit immediately locks access, stops subsequent sync
requests and blocks another account until the old owner's work is secured.
An unloaded corrupt cache remains untouched on disk and can release its empty
memory scope. The router exposes an actionable failure rather than old-account
content. No alternative synchronization architecture was introduced.

Commit `ce3af8e` strengthens the failure test: a newly queued row exists only in
memory when both its save and logout persistence fail. Retry and reopening must
recover both queued writes. `e5d739c` corrects the synthetic remote's online
state for the final account-B read. `3b895e3` removes a redundant null-aware
operator reported by analysis, without changing repository behavior.

Commands ran separately from `mobile`, with Flutter 3.44.5 and Dart 3.12.2:

| Command | Tested source | Result |
|---|---|---|
| `flutter test test/offline_inspection_repository_test.dart --concurrency=2` | `e5d739c09aa054a002e4a7736160269326217848` | Exit 0; 24 passed |
| `flutter test test/mobile_foundation_test.dart --concurrency=2` | `3b895e3970a9565776aa41b8fd3ff833b94870f5` | Exit 0; 10 passed |
| `flutter analyze` | `3b895e3970a9565776aa41b8fd3ff833b94870f5` | Exit 0; no issues |

The repository tests cover failing writes, forced account switching during a
held remote operation, queued retry, unreadable stored bytes and scope release.
Storage and network failures are injected test doubles. Session widget tests
passed; there is no dedicated new router-gating widget case. The main chat
reviewed the actual repository/router changes.

Logs remain ignored under `artifacts/remediation/clients/phase8-*`. Earlier
failed fixture and analyzer attempts remain retained. No physical device, real
disk-full condition, OS termination or signed release was tested. Work that
cannot be persisted cannot survive device/process loss; the correction retains
it in memory and reports the failure while the process remains available.
