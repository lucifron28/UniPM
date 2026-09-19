---
id: IMP-031
type: implementation
title: Flutter Android release-boundary hardening
status: reviewed
recordedAtUtc: 2026-09-04T06:49:40Z
sourceBranch: chore/mobile-release-hardening
evidenceLevel: locally-executed
testedCommit: 4a78936731de5b341d1608f039b1b6ce5db60638
buildVerificationCommit: 4a78936731de5b341d1608f039b1b6ce5db60638
buildVerificationStatus: passed
---

# Flutter Android Release-Boundary Hardening

## Objective

Harden the mobile release boundary without inventing a production API host,
signing identity, or deployment claim.

## Source Identity

- Tested implementation commit:
  `4a78936731de5b341d1608f039b1b6ce5db60638`
- Source branch: `chore/mobile-release-hardening`
- Integration base: `validation/pmis-only-gsd` at merged UX commit `903a78b`

## Implementation Summary

- Release-mode API configuration accepts only an HTTPS URL. Debug mode still
  permits the documented HTTP development route.
- API configuration rejects unsupported URL schemes in every build mode.
- The main Android manifest explicitly denies cleartext traffic; the existing
  debug-only manifest overlay remains the sole cleartext exception.
- Release signing reads the standard untracked `android/key.properties` file
  when project-owned signing material is supplied and never falls back to the
  debug keystore.
- README guidance documents the approved HTTPS release configuration and the
  external signing boundary.
- Added focused tests for debug HTTP, release HTTPS enforcement, and URL
  scheme validation.

No production endpoint, signing key, keystore, embedded secret, notification,
offline, persistent-session, attachment, alert, or device-specific behavior
was invented or committed.

## Verification Scope

`TEST-037` records the full mobile regression, release APK packaging, merged
manifest inspection, and unsigned-artifact result. The release APK is a
packaging artifact only until project-owned signing material and physical
device/live-backend validation are supplied.
