---
id: TEST-105
type: test-run
title: Centered responsive Quick search production build
status: executed
recordedAtUtc: 2026-10-18T10:50:05Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedFocusedSourceManifestSha256: 2ca8158eb43417192622918524835eebdb4518fa7f941061676d4ef9104fd090
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Centered responsive Quick search production build

## Execution identity

The build ran on branch `feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted changes. The
focused manifest covers `web/src/components/layout/quick-navigation.tsx` and
hashes its relative path and file bytes, separated by zero bytes, producing
`2ca8158eb43417192622918524835eebdb4518fa7f941061676d4ef9104fd090`.

The web package used Node `v22.23.3` and npm `10.9.9`, within its declared Node
`>=22 <23` range.

## Change under verification

Quick search is now centered within the viewport. Its result area can show more
role-available destinations on desktop and retains a bounded internal scroll
region on smaller screens. Search and navigation behavior are unchanged.

## Command and result

| Command | Result |
| --- | --- |
| `npm run build` (from `web/`) | Exit code 0. `tsc -b` passed; Vite 8.1.3 transformed 2,224 modules and produced the production bundle. |

## Not verified

- Updated browser screenshots, visual geometry at desktop/mobile sizes,
  keyboard/mouse interactions, full web/Playwright suites, deployed acceptance,
  and GitHub CI.
- No Azure endpoint, database, provider, commit, push, or deployment was used.
