---
id: TEST-099
type: test-run
title: Web production build after assignment queue update
status: executed
recordedAtUtc: 2026-10-18T10:05:08Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedWorkingTreeSourceManifestSha256: c8d1613f65cff5b3aae819e07c0f9ca69e1e95597f7b77a4481a093da0cb6c2d
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Web production build after assignment queue update

## Execution identity

The build ran on branch `feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted changes. The
source-manifest hash fingerprints all 31 changed, added, or deleted paths under
`web/` at execution time. Each path and its file bytes were included in the
manifest hash; deleted paths used an explicit marker.

The shell used Node `v24.15.0` and npm `11.12.1`. `web/package.json` declares
Node `>=22 <23`, so this successful build ran outside the supported Node
version range and does not replace a build on Node 22.

## Command and result

| Command | Result |
| --- | --- |
| `npm run build` | Exit code 0. The script ran `npm run typecheck` followed by `vite build`; Vite transformed 2,223 modules and produced the production bundle. |
| `git diff --check` | Passed after the build. Git emitted line-ending conversion notices for the OpenAPI snapshot and generated route tree. |

The shell printed a non-blocking Starship session-log access-denied warning
before the command. The build itself completed successfully.

## Not verified

- Build and typecheck on the supported Node 22 runtime.
- Playwright or fresh screenshots. Browser automation is disabled in the
  current Codex Work settings.
- Full web unit tests, full backend tests, CI, deployed browser acceptance, or
  large-catalog performance.
- No Azure connection, database write, commit, push, or deployment occurred.
