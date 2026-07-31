---
id: IMP-025
type: implementation
title: Flutter mobile authentication foundation
status: reviewed
recordedAtUtc: 2026-07-31T12:26:19Z
sourceBranch: feat/mobile-foundation
evidenceLevel: locally-executed
testedCommit: 07b678e207f59693252c16492c772ff9a6af8d4d
buildVerificationCommit: 07b678e207f59693252c16492c772ff9a6af8d4d
buildVerificationStatus: passed
---

# Flutter Mobile Authentication Foundation

## Objective

Create the Android-first Flutter application foundation for skilled-worker
field access without implementing preventive-maintenance workflows.

## Source Identity

- Tested implementation commit:
  `07b678e207f59693252c16492c772ff9a6af8d4d`
- Source branch: `feat/mobile-foundation`
- Starting main commit after PR #47:
  `4085fc27c4865551a90162ba6f41dbbe3bb8adfc`
- Merged source PR: `#47`

## Implementation Summary

- Adds the ordinary Flutter Android/iOS project structure under `mobile/`.
- Adds configurable API base URL support through
  `--dart-define=UNIPM_API_BASE_URL=<url>`.
- Uses the existing login, current-user, and logout contracts.
- Keeps the access token in memory only; no mobile refresh-token persistence,
  cookie capture, or manual Cookie header handling is implemented.
- Starts signed out after app startup or restart and requires a fresh login.
- Clears the memory-only session after a protected 401 without refresh or
  replay.
- Adds login, authenticated home shell, logout, and bounded unsupported-role
  states.
- Allows only Inspector and GSD roles through the client navigation boundary.
- Keeps backend authorization authoritative and adds no backend changes.
- Keeps the debug-only Android cleartext-traffic override for local HTTP
  development; the main and release manifests remain without a cleartext
  opt-in.

## Dependencies

- `http` for API requests.

## Scope Boundary

No preventive-maintenance forms, inspection entry, submission, acknowledgement,
signature capture, QR scanning, offline sync, background task, notification,
handoff, RMRF, WMS, OEM, retrieval, embedding, generation, database, migration,
OpenAPI, or web behavior was added.

## Verification Scope

TEST-031 records seven focused memory-only authentication tests and a
successful debug Android APK build. No emulator integration test, live backend
verification, analyzer run, release build, full repository suite, or production
deployment verification was run.

