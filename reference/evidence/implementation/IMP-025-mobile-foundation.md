---
id: IMP-025
type: implementation
title: Flutter mobile authentication foundation
status: reviewed
recordedAtUtc: 2026-07-28T18:08:50Z
sourceBranch: feat/mobile-foundation
evidenceLevel: source-inspected
testedCommit: 6e729061bb5eb58b9c91c874a0dd90b86ad001ee
---

# Flutter Mobile Authentication Foundation

## Objective

Create the Android-first Flutter application foundation for skilled-worker
field access without implementing preventive-maintenance workflows.

## Source Identity

- Tested implementation commit:
  `6e729061bb5eb58b9c91c874a0dd90b86ad001ee`
- Source branch: `feat/mobile-foundation`
- Starting main commit after PR #47:
  `4085fc27c4865551a90162ba6f41dbbe3bb8adfc`
- Merged source PR: `#47`

## Implementation Summary

- Adds the ordinary Flutter Android/iOS project structure under `mobile/`.
- Adds configurable API base URL support through
  `--dart-define=UNIPM_API_BASE_URL=<url>`.
- Uses the existing login, current-user, refresh, and logout contracts.
- Keeps access tokens in memory and stores only refresh-cookie session material
  through `flutter_secure_storage`.
- Sends the in-memory bearer token to `/api/v1/auth/me` while keeping login,
  refresh, and logout as cookie/session endpoints.
- Adds one bounded refresh and replay for ordinary 401 responses, followed by
  local access-token and secure-session clearing after a replayed terminal 401
  or logout.
- Adds startup restoration, login, authenticated home shell, logout, and clear
  unsupported-role states.
- Allows only Inspector and GSD roles through the client navigation boundary.
- Keeps backend authorization authoritative and adds no backend changes.

## Dependencies

- `http` for API requests.
- `flutter_secure_storage` for platform-secure refresh-cookie storage.

## Scope Boundary

No preventive-maintenance forms, inspection entry, submission, acknowledgement,
signature capture, QR scanning, offline sync, background task, notification,
handoff, RMRF, WMS, OEM, retrieval, embedding, generation, database, migration,
OpenAPI, or web behavior was added.

## Verification Scope

TEST-031 records the final Flutter analyzer and focused transport/widget-test
results. No emulator integration test was run.
