---
id: IMP-025
type: implementation
title: Flutter mobile authentication foundation
status: reviewed
recordedAtUtc: 2026-07-31T14:54:46Z
sourceBranch: feat/mobile-foundation
evidenceLevel: locally-executed
testedCommit: b93eb9b48d7235c337d1854240615a0290534934
buildVerificationCommit: b93eb9b48d7235c337d1854240615a0290534934
buildVerificationStatus: passed
---

# Flutter Mobile Authentication Foundation

## Objective

Create the Android-first Flutter application foundation for skilled-worker
field access without implementing preventive-maintenance workflows.

## Source Identity

- Tested implementation commit:
  `b93eb9b48d7235c337d1854240615a0290534934`
- Source branch: `feat/mobile-foundation`
- Starting main commit after PR #47:
  `4085fc27c4865551a90162ba6f41dbbe3bb8adfc`
- Merged source PR: `#47`

## Implementation Summary

- Uses the existing login, current-user, and logout contracts.
- Keeps access tokens in memory only; no refresh-token persistence, cookie
  capture, or manual Cookie header handling is implemented.
- Creates and closes a fresh native HTTP client per request, disables API
  redirects, and retains injected clients for focused tests.
- Starts signed out after app startup or restart and requires a fresh login.
- Clears the memory-only session after a protected 401 without refresh or
  replay, displays `Your session expired. Please sign in again.`, and returns
  to sign-in.
- Preserves `Invalid email or password.` for direct login 401 responses.
- Preserves Inspector and GSD as the supported mobile roles.
- Keeps the debug-only Android cleartext-traffic override for local HTTP
  development; the main and release manifests remain without a cleartext
  opt-in.

## Verification Scope

TEST-031 records eight focused memory-only authentication tests and a
successful debug Android APK build. No emulator integration test, live backend
verification, release build, full repository suite, or production deployment
verification was run.

