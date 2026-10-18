---
id: TEST-102
type: test-run
title: Supported Node 22 web production build
status: executed
recordedAtUtc: 2026-10-18T10:35:28Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedWorkingTreeSourceManifestSha256: 1a27d6aecbfd81cecfb87ab6b0b887091d6ecd80b6f12d0e2fd93727816f7a1c
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Supported Node 22 web production build

## Execution identity

The build ran on `feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted changes. The
working-tree manifest SHA-256 covers all 34 changed, added, or deleted paths
under `web/`. It hashes each relative path and file bytes with zero-byte
separators; deleted paths use an explicit marker. The digest is
`1a27d6aecbfd81cecfb87ab6b0b887091d6ecd80b6f12d0e2fd93727816f7a1c`.

The build used Node `v22.23.3` and npm `10.9.9`, within the web package's
declared Node `>=22 <23` range.

## Command and result

| Command | Result |
| --- | --- |
| `npm run build` (from `web/`) | Exit code 0. The script completed `tsc -b` and the Vite production build; Vite transformed 2,224 modules and produced the bundle. |

## Not verified

- Full web unit tests, Playwright, browser screenshots, deployed acceptance,
  full backend tests, and GitHub CI.
- No Azure connection or database operation, commit, push, or deployment
  occurred.

This record is a follow-up to TEST-101. It closes the supported Node 22 build
gap without changing or rewriting the earlier Node 24 result.
