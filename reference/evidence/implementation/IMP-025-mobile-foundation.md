---
id: IMP-025
type: implementation
title: Flutter mobile authentication foundation
status: blocked
recordedAtUtc: 2026-07-31T13:31:17Z
sourceBranch: feat/mobile-foundation
evidenceLevel: locally-executed
testedCommit: 1d5c178f63cf2d210fda711420b876e745cb3f71
buildVerificationStatus: not-run
---

# Flutter Mobile Authentication Foundation

## Objective

Create the Android-first Flutter application foundation for skilled-worker
field access without implementing preventive-maintenance workflows.

## Source Identity

- Latest corrected implementation commit:
  `1d5c178f63cf2d210fda711420b876e745cb3f71`
- Source branch: `feat/mobile-foundation`
- Starting main commit after PR #47:
  `4085fc27c4865551a90162ba6f41dbbe3bb8adfc`
- Merged source PR: `#47`

## Implementation Summary

- Uses the existing login, current-user, and logout contracts.
- Keeps access tokens in memory only; no refresh-token persistence, cookie
  capture, or manual Cookie header handling is implemented.
- Creates a fresh native HTTP client for each request, closes it after the
  response, and disables API redirects. Injected clients remain available for
  focused tests.
- Starts signed out after app startup or restart and requires a fresh login.
- Clears the memory-only session after a protected 401 without refresh or
  replay, using the bounded expired-session message.
- Preserves Inspector and GSD as the supported mobile roles.
- Keeps the debug-only Android cleartext-traffic override for local HTTP
  development; the main and release manifests remain without a cleartext
  opt-in.

## Verification Scope

The native loopback transport test timed out on its initial run and on the one
permitted rerun after explicit server-listener cleanup. The requested debug APK
build was not run against this unverified state. No emulator or live-backend
verification is claimed.

